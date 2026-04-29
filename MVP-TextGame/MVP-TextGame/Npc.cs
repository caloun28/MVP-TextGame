namespace MVP_TextGame
{
    public class Npc
    {
        public string Name { get; set; }
        public string Dialogue { get; set; }

        public bool IsEnemy { get; set; } = false;
        public bool IsBoss { get; set; } = false;

        public int HP { get; set; } = 100;
        public int Attack { get; set; } = 5;
    }
}