using System;
using System.Threading.Tasks;
using SNPPlib;

namespace SNPPHost
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Task.Run(async () => await MainAsyncServer()).Wait();
        }

        private static async Task MainAsyncServer()
        {
            using (var server = new SnppServer())
            {
                //Not a standard command.
                server.AddCommand("TEST", async (id, arg) =>
                {
                    return await Task.FromResult("This is a test command.");
                });

                server.AddCommand("DATA", async (id, arg) =>
                {
                    return await Task.FromResult("This is a test command.");
                });

                var tokenSource = await server.Listen();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();

                tokenSource.Cancel();
            }
        }
    }
}