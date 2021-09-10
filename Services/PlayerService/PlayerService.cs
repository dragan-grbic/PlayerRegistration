using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using PlayerRegistration.CommonTypes;
using PlayerRegistration.Interfaces;

namespace PlayerService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    //TODO: Remove IPlayerService interface once PlayerOrchestrator is done.
    public sealed class PlayerService : StatefulService, IPlayerServiceInternal
    {

        public const string StateManagerDictionaryName = "dict_players";

        private IReliableDictionary2<string, string> _playerDict = null;

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

        private async Task<IReliableDictionary2<string, string>> GetPlayerDictAsync()
        {
            if (_playerDict == null)
            {
                _playerDict = await StateManager.GetOrAddAsync<IReliableDictionary2<string, string>>(StateManagerDictionaryName);
            }
            return _playerDict;
        }

        public async Task<IEnumerable<PlayerData>> GetPlayers()
        {
            List<PlayerData> playerData = new List<PlayerData>();
            var dictPlayers = await GetPlayerDictAsync();
            using (var tx = StateManager.CreateTransaction())
            {
                Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<string, string>> players
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

        public async Task<string> AddPlayer(string key, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Player name must be provided.");
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The key must be provided.");
            }

            var dictPlayers = await GetPlayerDictAsync();

            using (var tx = StateManager.CreateTransaction())
            {

                var savedName = await dictPlayers.TryGetValueAsync(tx, key);
                await tx.CommitAsync();

                if (savedName.HasValue && savedName.Value != name)
                {
                    throw new ArgumentException(String.Format("Duplicate key {0}, names [new: {1}] and [was: {2}] on partition {3}. ", key, name, savedName.Value, Partition.PartitionInfo.Id));
                }
                else if (savedName.HasValue && savedName.Value == name)
                {
                    throw new ArgumentException(String.Format("User {0} is already present in this dictionary with key {1}. ", name, key));
                }

            }

            using (var tx = StateManager.CreateTransaction())
            {

                await dictPlayers.AddAsync(tx, key, name); //.ContinueWith(_ =>  tx.CommitAsync());
                await tx.CommitAsync();
                ServiceEventSource.Current.ServiceMessage(Context, "Created user {0} with name {1}", key, name);
            }
            return key;
        }

        public async Task<string> DeletePlayer(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Player unique id must be provided and cannot be empty.");
            }

            var dictPlayers = await GetPlayerDictAsync();

            using var tx = StateManager.CreateTransaction();
            var result = await dictPlayers.TryRemoveAsync(tx, key);
            await tx.CommitAsync();

            if (!result.HasValue)
            {
                throw new ArgumentException("Player with given UUID not found.");
            }
            ServiceEventSource.Current.ServiceMessage(Context, "Deleted user {0} with name {1}", key, result.Value);
            return result.Value;
        }

    }
}
