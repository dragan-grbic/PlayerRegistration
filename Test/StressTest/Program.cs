using Microsoft.ServiceFabric.Services.Remoting.Client;
using PlayerRegistration.CommonTypes;
using PlayerRegistration.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StressTest
{
    class Program
    {
        private static readonly string DefaultOrchestartorUri = "fabric:/PlayerRegistration/PlayerOrchestrator";
        private static readonly Uri playerServiceUri = new Uri(DefaultOrchestartorUri);

        static void CreateAndGetPlayers(int threadNo, int playerCount)
        {
            Console.WriteLine("Start thread {0}", threadNo);
            int thread_id = System.Threading.Thread.CurrentThread.ManagedThreadId;
            string base_name = String.Format("player{0}-{1}-", threadNo, thread_id);

            string[] playerNames = Enumerable.Range(1, playerCount).Select(n => base_name + n.ToString()).ToArray();

            Random rand = new Random();

            List<string> guids = new List<string>();
            foreach (var n in playerNames)
            {
                try
                {
                    guids.Add(AddPlayer(n));
                    Thread.Sleep(100);
                }
                catch (Exception)
                {
                }
            }

            Console.WriteLine("{0} Players added", guids.Count);

            Thread.Sleep(rand.Next(5000));

            IEnumerable<PlayerRegistration.CommonTypes.PlayerData> players = GetPlayers();


            Console.WriteLine("Got player list with {0} names.", players.Count());

            Thread.Sleep(rand.Next(5000));

            foreach (var g in guids)
            {
                try
                {
                    var deleted = DeletePlayer(g);

                }
                catch (Exception)
                {
                }
            }
            Console.WriteLine("Deleted.");
        }

        private static object DeletePlayer(string g)
        {
            var playerService = ServiceProxy.Create<IPlayerService>(playerServiceUri);
            return playerService.DeletePlayer(g).Result;
        }

        private static IEnumerable<PlayerData> GetPlayers()
        {
            IPlayerService playerService = ServiceProxy.Create<IPlayerService>(playerServiceUri);
            do
            {
                try
                {
                    return playerService.GetPlayers().Result;
                }
                catch (Exception)
                {
                }
            } while (true);
        }

        private static string AddPlayer(string n)
        {
            IPlayerService playerService = ServiceProxy.Create<IPlayerService>(playerServiceUri);
            var addTask = playerService.AddPlayer(n);
            addTask.Wait();
            if (addTask.IsCompletedSuccessfully)
            {
                return addTask.Result;
            }
            else
            {
                Console.WriteLine(addTask.Exception.ToString());
                throw addTask.Exception;
            }
        }

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: \tStressTest.exe <threadCount> <registrationsPerThread>");
                return;
            }

            int thread_count = int.Parse(args[0]);
            int count = int.Parse(args[1]);

            List<Thread> threads = new List<Thread>();


            for (int i = 0; i < thread_count; i++)
            {
                threads.Add(new Thread((object o) =>
                {
                    int _i = (int)o;
                    CreateAndGetPlayers(_i, count);
                }));
            }


            for (int i = 0; i < thread_count; i++)
            {
                threads[i].Start(i + 1);
            }

            Parallel.ForEach(threads, t => t.Join());

            IPlayerService playerService = ServiceProxy.Create<IPlayerService>(playerServiceUri);

            IEnumerable<PlayerRegistration.CommonTypes.PlayerData> players = null;
            do
            {
                try
                {
                    players = playerService.GetPlayers().Result;
                }
                catch (Exception)
                {
                }
            } while (players == null);

            Console.WriteLine("At exit there is {0} users.", players.Count());

            Console.WriteLine("Done.");
        }
    }
}
