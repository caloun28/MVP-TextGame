using System.Collections.Generic;

namespace MVP_TextGame.Models
{
    public class Room
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Connections { get; set; }

        public List<Npc> Npcs { get; set; }
    }
}