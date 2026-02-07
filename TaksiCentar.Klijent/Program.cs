using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace TaksiCentar.Klijent
{
    internal class Program
    {
        const string SERVER_IP = "127.0.0.1";
        const int TCP_PORT = 5000;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("KLIJENT START");

            Console.Write("Unesi početnu tačku: ");
            string start = Console.ReadLine();

            Console.Write("Unesi krajnju tačku: ");
            string end = Console.ReadLine();

            double x = 5;
            double y = 5;

            string request = "REQ|" + start + "|" + end + "|" + x + "|" + y;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(SERVER_IP, TCP_PORT);

                    using (NetworkStream stream = client.GetStream())
                    {
                        // UTF8 bez BOM (da server ne dobije "﻿REQ")
                        UTF8Encoding utf8NoBom = new UTF8Encoding(false);

                        using (StreamWriter writer = new StreamWriter(stream, utf8NoBom))
                        using (StreamReader reader = new StreamReader(stream, utf8NoBom))
                        {
                            writer.AutoFlush = true;

                            writer.WriteLine(request);
                            Console.WriteLine("Poslat zahtev. Čekam update-e...");

                            while (true)
                            {
                                string line = reader.ReadLine();
                                if (line == null) break;

                                Console.WriteLine("OD SERVERA: " + line);

                                if (line.StartsWith("ZAVRSENO"))
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greška: " + ex.Message);
            }

            Console.WriteLine("ENTER za izlaz...");
            Console.ReadLine();
        }
    }
}