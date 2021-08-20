using System;
using System.Collections.Generic;
using System.Fabric;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Narde.CommonTypes;
using Narde.Interfaces;

[assembly: InternalsVisibleTo("PlayerServiceUnitTest")]
namespace PlayerService
{
    internal static class PlayerServiceConstants
    {
        public const string StateManagerDictionaryName = "dict_players";
    }
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class PlayerService : StatefulService, IPlayerService
    {
        

        public PlayerService(StatefulServiceContext context)
            : base(context)
        { }

        public PlayerService(StatefulServiceContext context, IReliableStateManagerReplica reliableStateManagerReplica)
            : base(context, reliableStateManagerReplica)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }

        private Task<IReliableDictionary2<Guid, string>> GetPlayerDict()
        {
            return StateManager.GetOrAddAsync<IReliableDictionary2<Guid, string>>(PlayerServiceConstants.StateManagerDictionaryName);
        }

        public async Task<string> AddPlayer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Player name must be provided.");
            }

            var dictPlayers = await GetPlayerDict();
            var uuid = Guid.NewGuid();
            using (var tx = StateManager.CreateTransaction())
            {
                
                await dictPlayers.AddAsync(tx, uuid, name);
                await tx.CommitAsync();
                ServiceEventSource.Current.ServiceMessage(Context, "Created user {0} with name {1}", uuid.ToString(), name);
            }
            return uuid.ToString();
        }

        public async Task<string> DeletePlayer(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                throw new ArgumentException("Player unique id must be provided.");
            }

            var dictPlayers = await GetPlayerDict();

            using (var tx = StateManager.CreateTransaction())
            {
                var _uuid = Guid.Parse(uuid);
                var result = await dictPlayers.TryRemoveAsync(tx, _uuid);
                await tx.CommitAsync();

                if (!result.HasValue)
                {
                    throw new ArgumentException("Player with given UUID not found.");
                }
                ServiceEventSource.Current.ServiceMessage(Context, "Deleted user {0} with name {1}", uuid, result.Value);
                return result.Value;

            }
        }

        public async Task<IEnumerable<PlayerData>> GetPlayersOnline()
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<PlayerData>> GetPlayers()
        {
            List<PlayerData> playerData = new List<PlayerData>();
            var dictPlayers = await GetPlayerDict();
            using (var tx = StateManager.CreateTransaction())
            {
                Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<Guid, string>> players
                    = (await dictPlayers.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

                //await tx.CommitAsync();

                while (await players.MoveNextAsync(CancellationToken.None))
                {
                    ServiceEventSource.Current.ServiceMessage(Context, "Fetched user {0} with name {1}", players.Current.Key, players.Current.Value);
                    playerData.Add(new PlayerData(players.Current.Key, players.Current.Value));
                }

                await tx.CommitAsync();

            }

            return playerData;
            
        }
    }
}
