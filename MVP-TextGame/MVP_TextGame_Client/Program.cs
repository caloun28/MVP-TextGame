namespace MVP_TextGame_Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ClientServer client = new ClientServer("127.0.0.1", 65525);
            await client.StartAsync();

            /// test test
        }
    }
}
