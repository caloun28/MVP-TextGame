using MVP_TextGame.Models;
using System.Collections.Generic;
using System.Linq;

namespace MVP_TextGame
{
    public class GameMap
    {
        public List<Room> Rooms { get; set; }

        public Room GetRoom(string name)
        {
            return Rooms.FirstOrDefault(r => r.Name == name);
        }
    }
}