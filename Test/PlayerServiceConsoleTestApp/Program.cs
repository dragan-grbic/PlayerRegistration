using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Narde.CommonTypes;
using Narde.Interfaces;

namespace PlayerServiceConsoleTestApp
{
    class Program
    {
        private static readonly string DefaultOrchestartorUri = "fabric:/Narde/PlayerOrchestrator";
        private static readonly Uri playerServiceUri = new Uri(DefaultOrchestartorUri);
        static async Task Main(string[] args)
        {

            if (args.Length != 2)
            {
                Console.WriteLine("Usage: \tTestApp.exe <prefix> <count>");
                return;
            }

            //Console.ReadKey();

            string prefix = args[0];
            int count = int.Parse(args[1]);

            string[] playerNames = Enumerable.Range(2, count - 1).Select(n => prefix + n.ToString()).ToArray();
            IPlayerService playerService = ServiceProxy.Create<IPlayerService>(playerServiceUri);

            var player1guid = await playerService.AddPlayer(prefix + "1");

            Console.WriteLine(prefix + "1 added with uuid {0}", player1guid);
            await Task.WhenAll(playerNames.Select(n => {
                Console.WriteLine("Adding player {0}", n);
                return playerService.AddPlayer(n);
            }));

            Console.WriteLine("Players added");

            var players = await playerService.GetPlayers();

            foreach(var p in players)
            {
                if (!String.IsNullOrEmpty(p.UUID.ToString()) && !String.IsNullOrEmpty(p.Name))
                {
                    Console.WriteLine("Player {0} with name {1}", p.UUID.ToString(), p.Name);
                }
                else
                {
                    Console.WriteLine("Null value returned.");
                }
            }

            var player1name = await playerService.DeletePlayer(player1guid);

            Console.WriteLine("Deleted " + player1guid + " name: " + player1name);

            Console.WriteLine("Players after deletion:");

            players = await playerService.GetPlayers();

            foreach (var p in players)
            {
                if (!String.IsNullOrEmpty(p.UUID.ToString()) && !String.IsNullOrEmpty(p.Name))
                {
                    Console.WriteLine("Player {0} with name {1}", p.UUID.ToString(), p.Name);
                }
                else
                {
                    Console.WriteLine("Null value returned.");
                }
            }

        }
    }
}
