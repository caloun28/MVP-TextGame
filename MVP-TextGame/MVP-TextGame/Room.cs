using System.Collections.Generic;

namespace MVP_TextGame.Models
{
    public class Room
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Connections { get; set; }
        public List<Npc> Npcs { get; set; }
        public string Type { get; set; }
        public bool Blocked { get; set; } = false;
        public string BlockedBy { get; set; }
        public Npc Boss { get; set; }
    }
}