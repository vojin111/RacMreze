using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TaksiCentar.Modeli;

namespace TaksiCentar.Vozilo
{
    internal class Program
    {
        const int DEFAULT_VEHICLE_PORT = 7001;     // svako vozilo svoj port (npr. 7001,7002,7003...)
        const int UDP_SERVER_PORT = 6000;          // server UDP port (prima VoziloUpdate)

        static Guid VoziloId = Guid.NewGuid();

        // stanje vozila
        static double X = 0;
        static double Y = 0;
        static double PredjenaKm = 0;
        static double Zarada = 0;

        static int vehiclePort;
        static UdpClient udp;

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Port vozila: ili args[0] ili unos
            if (args.Length > 0 && int.TryParse(args[0], out int p))
                vehiclePort = p;
            else
            {
                Console.Write("Unesi port vozila (npr. 7001): ");
                string s = Console.ReadLine();
                if (!int.TryParse(s, out vehiclePort))
                    vehiclePort = DEFAULT_VEHICLE_PORT;
            }

            Console.WriteLine("VOZILO START");
            Console.WriteLine("VoziloId: " + VoziloId);
            Console.WriteLine("[UDP] Vozilo sluša zadatke na portu " + vehiclePort);

            // isti soket (isti port) koristimo i za primanje zadatka i slanje update-a
            udp = new UdpClient(vehiclePort);

            // inicijalni update da server "registruje" vozilo (zapamti port)
            PosaljiUpdate("Slobodno", null, false);

            while (true)
            {
                UdpReceiveResult result = await udp.ReceiveAsync();

                // primi zadatak (serijalizovan)
                Zadatak zadatak = Serializer.FromBytes<Zadatak>(result.Buffer);
                Console.WriteLine("Primljen zadatak: " + zadatak.Id);

                // 1) Odlazak ka klijentu (do početne tačke)
                while (Dist(X, Y, zadatak.PocX, zadatak.PocY) > 0.05)
                {
                    double moved = PomeriKa(zadatak.PocX, zadatak.PocY, 0.7);
                    PredjenaKm += moved; // 1 jedinica = 1 km (simulacija)
                    PosaljiUpdate("Odlazak", zadatak.Id, false);
                    Thread.Sleep(800);
                }

                // 2) Vožnja (od početne do krajnje tačke)
                while (Dist(X, Y, zadatak.KrajX, zadatak.KrajY) > 0.05)
                {
                    double moved = PomeriKa(zadatak.KrajX, zadatak.KrajY, 0.9);
                    PredjenaKm += moved;
                    Zarada += moved * 120; // 120 din/km (simulacija)

                    PosaljiUpdate("Voznja", zadatak.Id, false);
                    Thread.Sleep(800);
                }

                // 3) Završetak
                PosaljiUpdate("Slobodno", zadatak.Id, true);
                Console.WriteLine("Zadatak završen: " + zadatak.Id);
            }
        }

        static void PosaljiUpdate(string status, Guid? zadatakId, bool zavrsetak)
        {
            VoziloUpdate upd = new VoziloUpdate
            {
                VoziloId = VoziloId,
                Port = vehiclePort,
                X = X,
                Y = Y,
                Status = status,
                PredjenaKm = PredjenaKm,
                Zarada = (decimal)Zarada,
                ZadatakId = zadatakId,
                ZavrsetakVoznje = zavrsetak
            };

            byte[] data = Serializer.ToBytes(upd);
            IPEndPoint server = new IPEndPoint(IPAddress.Loopback, UDP_SERVER_PORT);

            udp.Send(data, data.Length, server);
        }

        static double PomeriKa(double tx, double ty, double step)
        {
            double dx = tx - X;
            double dy = ty - Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-9) return 0;

            double k = Math.Min(step, d) / d;
            double nx = X + dx * k;
            double ny = Y + dy * k;

            double moved = Math.Sqrt((nx - X) * (nx - X) + (ny - Y) * (ny - Y));
            X = nx;
            Y = ny;
            return moved;
        }

        static double Dist(double ax, double ay, double bx, double by)
        {
            double dx = ax - bx;
            double dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
