namespace MVP_TextGame
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            GameServer server = new GameServer(65525);
            await server.StartAsync();
        }
    }
}
