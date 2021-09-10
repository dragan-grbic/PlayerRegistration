using PlayerRegistration.Interfaces;
using ServiceFabric.Mocks;
using System;
using Xunit;
using Microsoft.ServiceFabric.Data.Collections;

namespace PlayerServiceUnitTest
{
    using Microsoft.ServiceFabric.Data;
    using PlayerService;
    using ServiceFabric.Mocks.ReliableCollections;
    using ServiceFabric.Mocks.ReplicaSet;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Linq;
    using System.Threading.Tasks;

    public class TestPlayerService
    {
        PlayerService CreatePlayerService(StatefulServiceContext context, IReliableStateManagerReplica stateManager)
        {
            return new PlayerService(context, stateManager);
        }

        [Fact]
        public async void TestServiceState_Dictionary()
        {
            var context = MockStatefulServiceContextFactory.Default;
            var stateManager = new MockReliableStateManager();
            var service = new PlayerService(context, stateManager);

            const string playerName = "test";

            var creationKey = Guid.NewGuid().ToString();
            //create state
            var playerKey = await service.AddPlayer(creationKey, playerName);
            
            //get state
            var dictionary = await stateManager.TryGetAsync<IReliableDictionary<string, string>>(PlayerService.StateManagerDictionaryName);
            using var tx = stateManager.CreateTransaction();
            var actual = (await dictionary.Value.TryGetValueAsync(tx, playerKey)).Value;
            await tx.CommitAsync();

            Assert.Equal(playerName, actual);
        }

        [Fact]
        public async void TestServiceState_DictionaryAddAndDelete()
        {
            var context = MockStatefulServiceContextFactory.Default;
            var stateManager = new MockReliableStateManager();
            var service = new PlayerService(context, stateManager);

            string[] playerNames = new[]{ "Player1", "Player2", "Player3" };

            //create state
            await Task.WhenAll(playerNames.Select(n => service.AddPlayer(Guid.NewGuid().ToString(), n)));

            //get players
            var players = (await service.GetPlayers()).ToList();

            Assert.Equal(playerNames.Count(), players.ToArray().Count());

            List<string> playerKeys = new List<string>();

            foreach (var p in players)
            {
                playerKeys.Add(p.Key);
            }

            //get state
            var dictionary = await stateManager.TryGetAsync<IReliableDictionary<string, string>>(PlayerService.StateManagerDictionaryName);
            using (var tx = stateManager.CreateTransaction())
            {
                var actual = (await dictionary.Value.TryGetValueAsync(tx, playerKeys[0])).Value;
                await tx.CommitAsync();

                Assert.Equal(players[0].Name, actual);
            }

            // delete player 1
            var player1name = await service.DeletePlayer(playerKeys[0]);

            Assert.Equal(player1name, players[0].Name);

            var playersAfterDelete = await service.GetPlayers();

            Assert.Equal(playerNames.Count() - 1, playersAfterDelete.ToArray().Count());

            Assert.DoesNotContain(playersAfterDelete, p => p.Key == playerKeys[0]);

        }

        [Fact]
        public async void TestServiceState_PromoteActivSecondary()
        {
            string[] playerNames = new[] { "Player1", "Player2", "Player3" };

            var context = MockStatefulServiceContextFactory.Default;

            var stateManager = new MockReliableStateManager();

            IReliableStateManagerReplica2 createStateManagerReplica(StatefulServiceContext ctx, TransactedConcurrentDictionary<Uri, IReliableState> states)
            {
                return stateManager;
            }

            var replicaSet = new MockStatefulServiceReplicaSet<PlayerService>(CreatePlayerService, createStateManagerReplica);

            await replicaSet.AddReplicaAsync(ReplicaRole.Primary, 1);
            await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 2);
            await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 3);

            //insert data
            var keys = new List<string>();
            foreach (var p in playerNames)
            {
                keys.Add(await replicaSet.Primary.ServiceInstance.AddPlayer(Guid.NewGuid().ToString(), p));
            }
            
            //promote secondary
            await replicaSet.PromoteActiveSecondaryToPrimaryAsync(2);

            //get data
            var players = (await replicaSet.Primary.ServiceInstance.GetPlayers()).ToList();

            //check data
            Assert.Equal(playerNames.Count(), players.Count);
            Assert.Contains(keys, g => g == players[0].Key);
            Assert.Contains(playerNames, n => n == players[1].Name);

            //verify the data was saved against the reliable dictionary
            var dictionary = await stateManager.GetOrAddAsync<IReliableDictionary<string, string>>(PlayerService.StateManagerDictionaryName);
            using var tx = stateManager.CreateTransaction();
            var payload = await dictionary.TryGetValueAsync(tx, keys[0]);
            Assert.True(payload.HasValue);
            Assert.Equal(playerNames[0], payload.Value);
        }
    }
}
