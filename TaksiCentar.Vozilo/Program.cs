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
        const int VEHICLE_PORT = 7001;     // prima Zadatak
        const int UDP_SERVER_PORT = 6000;  // šalje VoziloUpdate serveru

        // “identitet” vozila (da server zna koje je)
        static Guid VoziloId = Guid.NewGuid();

        // stanje vozila
        static double X = 0;
        static double Y = 0;
        static double PredjenaKm = 0;
        static decimal Zarada = 0;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("VOZILO START (Zadatak 5)");
            Console.WriteLine("VoziloId: " + VoziloId);
            Console.WriteLine("[UDP] Vozilo sluša zadatke na portu " + VEHICLE_PORT);

            // pošalji inicijalni update da server “registruje” vozilo
            PosaljiUpdate("Slobodno", null, false);

            UdpClient receiver = new UdpClient(VEHICLE_PORT);

            while (true)
            {
                UdpReceiveResult result = await receiver.ReceiveAsync();

                // primi zadatak (serijalizovan)
                Zadatak zadatak = Serializer.FromBytes<Zadatak>(result.Buffer);

                Console.WriteLine("Primljen zadatak: " + zadatak.Id);

                // 1) Odlazak ka klijentu (simulacija 3 koraka)
                for (int i = 0; i < 3; i++)
                {
                    PomeriSeMalo();
                    PosaljiUpdate("Odlazak", zadatak.Id, false);
                    Thread.Sleep(1000);
                }

                // 2) Vožnja (simulacija 5 koraka)
                for (int i = 0; i < 5; i++)
                {
                    PomeriSeMalo();
                    PredjenaKm += 1.2; // simulacija km
                    Zarada += 150;     // simulacija zarade

                    PosaljiUpdate("Voznja", zadatak.Id, false);
                    Thread.Sleep(1000);
                }

                // 3) Završetak
                PosaljiUpdate("Slobodno", zadatak.Id, true);
                Console.WriteLine("Zadatak završen: " + zadatak.Id);
            }
        }

        static void PomeriSeMalo()
        {
            // prosta simulacija kretanja
            X += 1;
            Y += 0.5;
        }

        static void PosaljiUpdate(string status, Guid? zadatakId, bool zavrsetak)
        {
            VoziloUpdate upd = new VoziloUpdate
            {
                VoziloId = VoziloId,
                X = X,
                Y = Y,
                Status = status,
                PredjenaKm = PredjenaKm,
                Zarada = Zarada,
                ZadatakId = zadatakId,
                ZavrsetakVoznje = zavrsetak
            };

            byte[] data = Serializer.ToBytes(upd);

            IPEndPoint server = new IPEndPoint(IPAddress.Loopback, UDP_SERVER_PORT);
            using (UdpClient udp = new UdpClient())
            {
                udp.Send(data, data.Length, server);
            }
        }
    }
}