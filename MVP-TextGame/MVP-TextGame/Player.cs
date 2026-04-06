using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    public class Player
    {
        public string Name { get; set; }
        
        public TcpClient Client { get; set; }
        public StreamReader Reader { get; set; }
        public StreamWriter Writer { get; set; }

        public Player(string name, TcpClient client, StreamReader reader, StreamWriter writer)
        {
            Name = name;
            Client = client;
            Reader = reader;
            Writer = writer;
        }
    }
}
