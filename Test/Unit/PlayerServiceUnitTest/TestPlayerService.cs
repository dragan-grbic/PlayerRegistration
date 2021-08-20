using Narde.Interfaces;
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

        private IReliableStateManagerReplica2 CreateStateManagerReplica(StatefulServiceContext ctx, TransactedConcurrentDictionary<Uri, IReliableState> states)
        {
            return new MockReliableStateManager(states);
        }

        [Fact]
        public async void TestServiceState_Dictionary()
        {
            var context = MockStatefulServiceContextFactory.Default;
            var stateManager = new MockReliableStateManager();
            var service = new PlayerService(context, stateManager);

            const string playerName = "test";

            //create state
            var playerGuid = await service.AddPlayer(playerName);
            
            //get state
            var dictionary = await stateManager.TryGetAsync<IReliableDictionary<Guid, string>>(PlayerServiceConstants.StateManagerDictionaryName);
            using (var tx = stateManager.CreateTransaction())
            {
                var actual = (await dictionary.Value.TryGetValueAsync(tx, Guid.Parse(playerGuid))).Value;
                await tx.CommitAsync();

                Assert.Equal(playerName, actual);
            }
        }

        [Fact]
        public async void TestServiceState_DictionaryAddAndDelete()
        {
            var context = MockStatefulServiceContextFactory.Default;
            var stateManager = new MockReliableStateManager();
            var service = new PlayerService(context, stateManager);

            string[] playerNames = new[] { "Player1", "Player2", "Player3" };

            //create state
            await Task.WhenAll(playerNames.Select(n => service.AddPlayer(n)));

            //get players
            var players = (await service.GetPlayers()).ToList();

            Assert.Equal(playerNames.Count(), players.ToArray().Count());

            List<Guid> playerGuids = new List<Guid>();

            foreach (var p in players)
            {
                playerGuids.Add(p.UUID);
            }

            //get state
            var dictionary = await stateManager.TryGetAsync<IReliableDictionary<Guid, string>>(PlayerServiceConstants.StateManagerDictionaryName);
            using (var tx = stateManager.CreateTransaction())
            {
                var actual = (await dictionary.Value.TryGetValueAsync(tx, playerGuids[0])).Value;
                await tx.CommitAsync();

                Assert.Equal(players[0].Name, actual);
            }

            // delete player 1
            var player1name = await service.DeletePlayer(playerGuids[0].ToString());

            Assert.Equal(player1name, players[0].Name);

            var playersAfterDelete = await service.GetPlayers();

            Assert.Equal(playerNames.Count() - 1, playersAfterDelete.ToArray().Count());

            Assert.DoesNotContain(playersAfterDelete, p => p.UUID == playerGuids[0]);

        }

        [Fact]
        public async void TestServiceState_PromoteActivSecondary()
        {
            string[] playerNames = new[] { "Player1", "Player2", "Player3" };

            var context = MockStatefulServiceContextFactory.Default;
            //var stateManagerList = new List<MockReliableStateManager>();

            var stateManager = new MockReliableStateManager();

            Func<StatefulServiceContext, TransactedConcurrentDictionary<Uri, IReliableState>, IReliableStateManagerReplica2> createStateManagerReplica = (StatefulServiceContext ctx, TransactedConcurrentDictionary<Uri, IReliableState> states) =>
            {
                //stateManagerList.Add(new MockReliableStateManager());
                //return stateManagerList.Last();
                return stateManager;
            };

            var replicaSet = new MockStatefulServiceReplicaSet<PlayerService>(CreatePlayerService, createStateManagerReplica);

            await replicaSet.AddReplicaAsync(ReplicaRole.Primary, 1);
            await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 2);
            await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 3);

            //insert data
            var guids = new List<string>();
            foreach (var p in playerNames)
            { 
                guids.Add(await replicaSet.Primary.ServiceInstance.AddPlayer(p));
            }
            
            //promote secondary
            await replicaSet.PromoteActiveSecondaryToPrimaryAsync(2);

            //get data
            var players = (await replicaSet.Primary.ServiceInstance.GetPlayers()).ToList();

            //check data
            Assert.Equal(playerNames.Count(), players.Count);
            Assert.Contains(guids, g => g == players[0].UUID.ToString());
            Assert.Contains(playerNames, n => n == players[1].Name);

            //verify the data was saved against the reliable dictionary
            //var stateManager = stateManagerList[0];
            var dictionary = await stateManager.GetOrAddAsync<IReliableDictionary<Guid, string>>(PlayerServiceConstants.StateManagerDictionaryName);
            using (var tx = stateManager.CreateTransaction())
            {
                var payload = await dictionary.TryGetValueAsync(tx, Guid.Parse(guids[0]));
                Assert.True(payload.HasValue);
                Assert.Equal(playerNames[0], payload.Value);
            }
        }
    }
}
