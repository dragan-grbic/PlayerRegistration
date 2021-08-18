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
        private static readonly Uri playerServiceUri = new Uri("fabric:/Narde/PlayerService");
        static async Task Main(string[] args)
        {
            string[] playerNames = new []{ "Player2", "Player3" };
            IPlayerService playerService 
                = ServiceProxy.Create<IPlayerService>(playerServiceUri, new ServicePartitionKey(1));

            var player1guid = await playerService.AddPlayer("Player1");

            Console.WriteLine("Player1 added with uuid {0}", player1guid);
            await Task.WhenAll(playerNames.Select(n => playerService.AddPlayer(n)));

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
