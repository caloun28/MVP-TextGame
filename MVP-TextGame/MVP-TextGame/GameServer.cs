using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    public class GameServer
    {
        private readonly int _port;

        public GameServer(int port)
        {
            _port = port;
        }

        public async Task StartAsync()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, _port);

            try
            {
                listener.Start();
                Console.WriteLine($"Server spusten na portu {_port}. Cekam na hrace...");

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    Console.WriteLine($"Klient pripojen: {client.Client.RemoteEndPoint}");

                    Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Chyba na serveru: {e.Message}");
            }
        }

        public async Task HandleClientAsync(TcpClient client)
        {
            string endpoint = client.Client.RemoteEndPoint.ToString();

            try
            {
                using (client)
                using (StreamReader reader = new StreamReader(client.GetStream(), Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(client.GetStream(), Encoding.UTF8))
                {
                    writer.AutoFlush = true;

                    await writer.WriteLineAsync("Vitej v opustenem dole");
                    await writer.WriteLineAsync("Zadej [1] pro prihlaseni nebo [2] pro registraci");

                    string choice = await reader.ReadLineAsync();
                    string playerName = "";

                    switch (choice)
                    {
                        case "1":
                            await writer.WriteLineAsync("Zadej jmeno:");
                            playerName = await reader.ReadLineAsync();
                            await writer.WriteLineAsync("Zadej heslo:");
                            string password = await reader.ReadLineAsync();
                            break;

                        case "2":
                            await writer.WriteLineAsync("Zadej nove jmeno:");
                            playerName = await reader.ReadLineAsync();
                            Console.WriteLine($"Hrac {playerName} se uspesne registroval.");
                            break;

                        default:
                            await writer.WriteLineAsync("Neplatna volba. Odpojuji...");
                            return;
                    }

                    await writer.WriteLineAsync($"\nJsi prihlasen jako {playerName}. Pro napovedu zadej 'pomoc'.");
                    bool isPlaying = true;

                    while (isPlaying)
                    {
                        string command = await reader.ReadLineAsync();

                        if (command == "")
                        {
                            break;
                        }

                        Console.WriteLine($"Hrac {playerName} zadal prikaz {command}");

                        switch (command.ToLower())
                        {
                            case "exit":
                                await writer.WriteLineAsync("Odpojovani z herniho sveta...");
                                isPlaying = false;
                                break;

                            default:
                                await writer.WriteLineAsync("Neznamy prikaz. Zadej 'pomoc' pro seznam prikazu.");
                                break;

                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Spojeni s hracem ({endpoint}) bylo preruseno: {ex.Message}");
            }
        }
    }
}
