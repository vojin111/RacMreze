using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaksiCentar.Modeli;




namespace TaksiCentar.Server
{


    internal class Program
    {
        const int TCP_PORT = 5000;
        const int UDP_PORT = 6000;
        const double BRZINA_KM_H = 30.0; // za ETA simulaciju

        static List<TaksiVozilo> vozila = new List<TaksiVozilo>();
        static List<KlijentZahtev> zahtevi = new List<KlijentZahtev>();
        static List<Zadatak> aktivniZadaci = new List<Zadatak>();

        static TcpListener tcpListener;
        static UdpClient udpListener;

        static object clientLock = new object();
        static Dictionary<Guid, StreamWriter> klijentWriters = new Dictionary<Guid, StreamWriter>();
        static Dictionary<Guid, TcpClient> klijentConnections = new Dictionary<Guid, TcpClient>();

        static Dictionary<Guid, IPEndPoint> voziloEndpoints = new Dictionary<Guid, IPEndPoint>();
        


        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("SERVER START (Zadatak 5)");

           

            tcpListener = new TcpListener(IPAddress.Any, TCP_PORT);
            tcpListener.Start();

            udpListener = new UdpClient(UDP_PORT);

            DateTime lastUi = DateTime.MinValue;

            while (true)
            {
                // --- MULTIPLEKSIRANJE (jedan ciklus obrađuje i TCP i UDP) ---
                ObradiTcpAkoIma();
                ObradiUdpAkoIma();

                // UI refresh na ~300ms
                if ((DateTime.Now - lastUi).TotalMilliseconds > 300)
                {
                    Prikaz();
                    lastUi = DateTime.Now;
                }

                Thread.Sleep(30); // mali "tick"
            }
        }

        static void ObradiTcpAkoIma()
        {
            if (!tcpListener.Pending())
                return;

            TcpClient client = tcpListener.AcceptTcpClient();
            Task.Run(() => ObradiKlijenta(client));
        }

        static void ObradiUdpAkoIma()
        {
            // UdpClient nema Pending, ali ima Available preko underlying socket-a
            if (udpListener.Client.Available <= 0)
                return;

            IPEndPoint remote = null;
            byte[] data = udpListener.Receive(ref remote);

            // vozila šalju VoziloUpdate (BinaryFormatter)
            try
            {
                VoziloUpdate upd = Serializer.FromBytes<VoziloUpdate>(data);
                Console.WriteLine("UDP UPDATE stigao: Vozilo=" + upd.VoziloId + " Status=" + upd.Status);

                // zapamti endpoint vozila (port je port na kojem vozilo prima zadatke)
                if (remote is IPEndPoint rep)
                {
                    voziloEndpoints[upd.VoziloId] = rep;
                }

                TaksiVozilo v = vozila.FirstOrDefault(x => x.Id == upd.VoziloId);
                if (v == null)
                {
                    // novo vozilo (dinamički dodaj)
                    v = new TaksiVozilo { Id = upd.VoziloId, Port = upd.Port };
                    vozila.Add(v);
                }

                v.X = upd.X;
                v.Y = upd.Y;
                v.Status = upd.Status;
                v.PredjenaKm = upd.PredjenaKm;
                v.Zarada = upd.Zarada;
                v.Port = upd.Port;

                if (upd.ZadatakId.HasValue)
                {
                    Zadatak z = aktivniZadaci.FirstOrDefault(x => x.Id == upd.ZadatakId.Value);
                    if (z != null)
                    {
                        z.StatusZadatka = upd.ZavrsetakVoznje ? "Zavrsen" : "Aktivan";
                        // ovde držimo razdaljinu u zadatku kao poslednje prijavljeno ukupno
                        z.PredjenaRazdaljina = upd.PredjenaKm;
                    }

                    if (upd.ZavrsetakVoznje)
                    {
                        // zatvori zadatak i oslobodi vozilo
                        if (z != null)
                        {
                            z.StatusZadatka = "Zavrsen";
                            aktivniZadaci.Remove(z);
                        }

                        if (v != null) v.Status = "Slobodno";

                        // nađi klijentski zahtev i završi ga (u ovoj verziji mapiramo po “prvom prihvaćenom”)
                        KlijentZahtev kz = zahtevi.FirstOrDefault(x => x.Id == z.KlijentId);
                        if (kz != null) kz.Status = "Zavrseno";

                        if (v != null) v.BrojMusterija += 1;
                    }
                }

                // Pošalji update odgovarajućem klijentu (po zadatku)
                if (upd.ZadatakId.HasValue)
                {
                    Zadatak z = aktivniZadaci.FirstOrDefault(x => x.Id == upd.ZadatakId.Value);
                    if (z != null)
                    {
                        StreamWriter w = null;
                        lock (clientLock)
                        {
                            klijentWriters.TryGetValue(z.KlijentId, out w);
                        }

                        if (w != null)
                        {
                            // ETA zavisi od faze: Odlazak -> do starta, Voznja -> do kraja
                            double tx = (upd.Status == "Odlazak") ? z.PocX : z.KrajX;
                            double ty = (upd.Status == "Odlazak") ? z.PocY : z.KrajY;

                            double dist = Dist(upd.X, upd.Y, tx, ty);
                            double etaMin = (dist / BRZINA_KM_H) * 60.0;

                            w.WriteLine($"POS|x={upd.X:0.0}|y={upd.Y:0.0}|status={upd.Status}|ETAmin={etaMin:0.0}");
                            if (upd.ZavrsetakVoznje)
                            {
                                w.WriteLine("ZAVRSENO");
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ako bi stigla neka stara poruka/string, ignoriši da ne ruši server
            }

            
        }

        static async Task ObradiKlijenta(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[4096];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0) return;

                string req = Encoding.UTF8.GetString(buffer, 0, read).Trim();

                Console.WriteLine("RAW TCP: [" + req + "]");

                // format: REQ|poc|kraj|sx|sy|ex|ey
                string[] parts = req.Split('|');
                if (parts.Length < 7 || parts[0] != "REQ")
                {
                    byte[] bad = Encoding.UTF8.GetBytes("ERR: Bad request\n");
                    await stream.WriteAsync(bad, 0, bad.Length);
                    return;
                }

                string poc = parts[1];
                string kraj = parts[2];
                double sx = double.Parse(parts[3]);
                double sy = double.Parse(parts[4]);
                double ex = double.Parse(parts[5]);
                double ey = double.Parse(parts[6]);

                KlijentZahtev kz = new KlijentZahtev
                {
                    PocetnaTacka = poc,
                    KrajnjaTacka = kraj,
                    X = sx,
                    Y = sy,
                    KrajX = ex,
                    KrajY = ey,
                    Status = "Ceka"
                };
                zahtevi.Add(kz);

                TaksiVozilo vozilo = NadjiNajblizeSlobodnoVozilo(sx, sy);
                if (vozilo == null)
                {
                    byte[] none = Encoding.UTF8.GetBytes("NEMA_SLOBODNIH\n");
                    await stream.WriteAsync(none, 0, none.Length);
                    return;
                }

                // ETA = dist / brzina
                double dist = Dist(vozilo.X, vozilo.Y, sx, sy);
                double etaH = dist / BRZINA_KM_H;
                double etaMin = etaH * 60.0;
                kz.EtaMin = Math.Round(etaMin, 1);

                // napravi zadatak
                Zadatak z = new Zadatak
                {
                    KlijentId = kz.Id,
                    VoziloId = vozilo.Id,
                    PocetnaTacka = kz.PocetnaTacka,
                    KrajnjaTacka = kz.KrajnjaTacka,
                    PocX = kz.X,
                    PocY = kz.Y,
                    KrajX = kz.KrajX,
                    KrajY = kz.KrajY,
                    StatusZadatka = "Aktivan",
                    PredjenaRazdaljina = 0
                };
                aktivniZadaci.Add(z);

                // writer bez BOM (da ne bude "﻿REQ" problema)
                StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.AutoFlush = true;

                lock (clientLock)
                {
                    klijentWriters[kz.Id] = writer;
                    klijentConnections[kz.Id] = client;
                }

                // prva poruka klijentu (Samo jednom!)
                writer.WriteLine($"PRIHVACENO|Vozilo={vozilo.Id}|Port={vozilo.Port}|ETAmin={kz.EtaMin}");

                // ažuriraj status
                vozilo.Status = "Odlazak";
                kz.Status = "Prihvaceno";

                // pošalji zadatak vozilu preko UDP (serijalizovan Zadatak)
                byte[] data = Serializer.ToBytes(z);
                using (UdpClient udp = new UdpClient())
                {
                    IPEndPoint voziloEP;
                    if (!voziloEndpoints.TryGetValue(vozilo.Id, out voziloEP))
                    {
                        // fallback: ako jos nije stigao update, salji na port upisan u vozilo
                        voziloEP = new IPEndPoint(IPAddress.Loopback, vozilo.Port);
                    }
                    udp.Send(data, data.Length, voziloEP);
                }

                // IMPORTANT:
                // Ne izlazi iz metode dok klijent treba da prima POS poruke.
                // Ovaj using blok drži konekciju otvorenom.
                // (Klijent će prekinuti kad dobije ZAVRSENO)
                while (true)
                {
                    await Task.Delay(500);
                    if (kz.Status == "Zavrseno") break;
                }

                // cleanup klijent konekcije
                lock (clientLock)
                {
                    klijentWriters.Remove(kz.Id);
                    klijentConnections.Remove(kz.Id);
                }

            }
        }

        static TaksiVozilo NadjiNajblizeSlobodnoVozilo(double x, double y)
        {
            TaksiVozilo best = null;
            double bestDist = double.MaxValue;

            foreach (var v in vozila)
            {
                if (v.Status != "Slobodno") continue;
                double d = Dist(v.X, v.Y, x, y);
                if (d < bestDist) { bestDist = d; best = v; }
            }

            return best;
        }

        static double Dist(double x1, double y1, double x2, double y2)
            => Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));

        static void Prikaz()
        {
            Console.Clear();

            Console.WriteLine("=== TAKSI CENTAR (Zadatak 5) ===");
            Console.WriteLine("Vozila:");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("{0,-36} | {1,8} | {2,8} | {3,-10} | {4,8} | {5,10}",
                "ID", "X", "Y", "Status", "Km", "Zarada");
            Console.WriteLine("--------------------------------------------------------------------------------");

            foreach (var v in vozila)
            {
                Console.WriteLine("{0,-36} | {1,8:0.0} | {2,8:0.0} | {3,-10} | {4,8:0.0} | {5,10:0.00}",
                    v.Id, v.X, v.Y, v.Status, v.PredjenaKm, v.Zarada);
            }

            Console.WriteLine();
            Console.WriteLine("Zahtevi klijenata:");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("{0,-36} | {1,-12} | {2,-12} | {3,-10} | {4,6}",
                "ID", "Pocetna", "Krajnja", "Status", "ETA");
            Console.WriteLine("--------------------------------------------------------------------------------");

            foreach (var z in zahtevi.Skip(Math.Max(0, zahtevi.Count - 15)))
            {
                Console.WriteLine("{0,-36} | {1,-12} | {2,-12} | {3,-10} | {4,6:0.0}",
                    z.Id, z.PocetnaTacka, z.KrajnjaTacka, z.Status, z.EtaMin);
            }

            Console.WriteLine();
            Console.WriteLine("Aktivni zadaci:");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("{0,-36} | {1,-8} | {2,-8} | {3,-8}",
                "ZadatakID", "Klijent", "Vozilo", "Status");
            Console.WriteLine("--------------------------------------------------------------------------------");

            foreach (var a in aktivniZadaci)
            {
                Console.WriteLine("{0,-36} | {1,-8} | {2,-8} | {3,-8}",
                    a.Id, a.KlijentId.ToString().Substring(0, 8), a.VoziloId.ToString().Substring(0, 8), a.StatusZadatka);
            }

            Console.WriteLine();
            Console.WriteLine("CTRL+C za izlaz.");
        }
    }
}