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

namespace PlayerService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    //TODO: Remove IPlayerService interface once PlayerOrchestrator is done.
    public sealed class PlayerService : StatefulService, IPlayerServiceInternal
    {

        public const string StateManagerDictionaryName = "dict_players";

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
            return StateManager.GetOrAddAsync<IReliableDictionary2<Guid, string>>(StateManagerDictionaryName);
        }

        public async Task<IEnumerable<PlayerData>> GetPlayersOnline()
        {
            //TODO: Replace with real implementation
            return await GetPlayers();
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

        public async Task<Guid> AddPlayer(Guid guid, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Player name must be provided.");
            }

            var dictPlayers = await GetPlayerDict();

            using (var tx = StateManager.CreateTransaction())
            {

                await dictPlayers.AddAsync(tx, guid, name);
                await tx.CommitAsync();
                ServiceEventSource.Current.ServiceMessage(Context, "Created user {0} with name {1}", guid.ToString(), name);
            }
            return guid;
        }

        public async Task<string> DeletePlayer(Guid guid)
        {
            if (Guid.Empty == guid)
            {
                throw new ArgumentException("Player unique id must be provided and cannot be empty.");
            }

            var dictPlayers = await GetPlayerDict();

            using var tx = StateManager.CreateTransaction();
            var result = await dictPlayers.TryRemoveAsync(tx, guid);
            await tx.CommitAsync();

            if (!result.HasValue)
            {
                throw new ArgumentException("Player with given UUID not found.");
            }
            ServiceEventSource.Current.ServiceMessage(Context, "Deleted user {0} with name {1}", guid, result.Value);
            return result.Value;
        }
    }
}
