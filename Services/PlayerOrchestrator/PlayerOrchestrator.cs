using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Narde.CommonTypes;
using Narde.Interfaces;

namespace PlayerOrchestrator
{
    internal static class Constants
    {
        public const int partitionCount = 8;
    }
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    public sealed class PlayerOrchestrator : StatelessService, IPlayerService
    {
        private readonly IServiceProxyFactory _serviceProxyFactory;
        private readonly Uri _serviceUri = new Uri(IPlayerServiceInternal.DefaultServiceUri);
        public PlayerOrchestrator(StatelessServiceContext context)
            : base(context)
        {
            _serviceProxyFactory = new ServiceProxyFactory();
        }

        public PlayerOrchestrator(StatelessServiceContext context, string serviceUri, IServiceProxyFactory proxyFactory)
            : base(context)
        {
            _serviceUri = new Uri(serviceUri);
            _serviceProxyFactory = proxyFactory;
        }

        IPlayerServiceInternal GetServiceProxy(int partitionKey)
        {
            return _serviceProxyFactory.CreateServiceProxy<IPlayerServiceInternal>(
                _serviceUri, 
                new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(partitionKey));
        }

        int GetPartitionKey(Guid uuid)
        {
            return (BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & 0x7fff) % Constants.partitionCount;
        }

        public async Task<string> AddPlayer(string name)
        {
            var uuid = Guid.NewGuid();
            ServiceEventSource.Current.ServiceMessage(Context, "Adding player {0} to partition {1}", name, GetPartitionKey(uuid));
            IPlayerServiceInternal proxy = GetServiceProxy(GetPartitionKey(uuid));
           
            return (await proxy.AddPlayer(uuid, name)).ToString();
        }

        public async Task<string> DeletePlayer(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                throw new ArgumentException("Player unique id must be provided and cannot be empty.");
            }
            Guid guid = Guid.Parse(uuid);
            ServiceEventSource.Current.ServiceMessage(Context, "Delete player {0} from partition {1}", uuid, GetPartitionKey(guid));
            IPlayerServiceInternal proxy = GetServiceProxy(GetPartitionKey(guid));

            return await proxy.DeletePlayer(guid);
        }
        public async Task<IEnumerable<PlayerData>> GetPlayers()
        {
            int partition = new Random().Next(Constants.partitionCount);
            return await GetServiceProxy(partition).GetPlayers();
        }

        public Task<IEnumerable<PlayerData>> GetPlayersOnline()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }

    }
}
