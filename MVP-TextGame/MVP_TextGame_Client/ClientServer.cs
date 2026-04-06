using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MVP_TextGame_Client
{
    internal class ClientServer
    {
        private string _ipAddress;
        private int _port;

        public ClientServer(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        public async Task StartAsync()
        {

            Console.WriteLine($"Connecting to {_ipAddress}:{_port}...");
            using TcpClient client = new TcpClient();

            try
            {
                await client.ConnectAsync(_ipAddress, _port);
                Console.WriteLine("Connected to server!");

                using StreamReader reader = new StreamReader(client.GetStream(), Encoding.UTF8);
                using StreamWriter writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };

                Task readTask = Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            string message = await reader.ReadLineAsync();

                            if (message == null)
                            {
                                Console.WriteLine("Server closed the connection.");
                                break;
                            }

                            Console.WriteLine(message);
                        }

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error reading from server: {e.Message}");
                    }
                });

                while (true)
                {
                    string input = Console.ReadLine();
                    if(string.IsNullOrEmpty(input)) continue;

                    await writer.WriteLineAsync(input);

                    if(input.ToLower() == "exit")
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error connecting to server: {e.Message}");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
            }
        }
    }
}
