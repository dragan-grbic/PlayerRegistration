using PlayerRegistration.Interfaces;
using ServiceFabric.Mocks;
using System;
using Xunit;

namespace PlayerOrchestratorUnitTest
{
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using PlayerOrchestrator;
    using PlayerService;
    using System.Fabric;

    public class TestPlayerOrchestrator
    {
        public const string MockServiceUri = "fabric:/Mock/PlayerRegistration/PlayerService";

        PlayerOrchestrator CreatePlayerOrchestrator(IReliableStateManagerReplica stateManager)
        {
            var orchestratorContext = MockStatelessServiceContextFactory.Default;
            var serviceContext = MockStatefulServiceContextFactory.Default;
            
            MockServiceProxyFactory mockServiceProxyFactory = new MockServiceProxyFactory();
            mockServiceProxyFactory.RegisterService(new Uri(MockServiceUri), new PlayerService(serviceContext, stateManager));

            return new PlayerOrchestrator(orchestratorContext, MockServiceUri, mockServiceProxyFactory);
        }

        [Fact]
        public async void TestServiceState_Dictionary()
        {
            var stateManager = new MockReliableStateManager();
            var service = CreatePlayerOrchestrator(stateManager);

            const string playerName = "test";

            //create state
            var playerKey = await service.AddPlayer(playerName);

            //get state
            var dictionary = await stateManager.TryGetAsync<IReliableDictionary<string, string>>(PlayerService.StateManagerDictionaryName);
            using var tx = stateManager.CreateTransaction();
            var actual = (await dictionary.Value.TryGetValueAsync(tx, playerKey)).Value;
            await tx.CommitAsync();

            Assert.Equal(playerName, actual);
        }

    }
}
