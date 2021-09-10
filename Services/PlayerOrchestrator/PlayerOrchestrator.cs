using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Fabric;
using System.Fabric.Description;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using PlayerRegistration.CommonTypes;
using PlayerRegistration.Interfaces;

namespace PlayerOrchestrator
{
    internal static class Constants
    {
        public const int partitionCount = 8;
        public const int UpperLoadTreshold = 12000;
        public const int LowerLoadTreshold = 6000;
        public const string ServiceLoadMetricName = "RequestsPerMinute";
        public const int MinInstanceCount = 1;
        public const int MaxInstanceCount = 5;
        public const int ScaleIncrement = 1;
        public static readonly TimeSpan ScaleInterval = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan ReportingInterval = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    public sealed class PlayerOrchestrator : StatelessService, IPlayerService
    {
        private readonly IServiceProxyFactory _serviceProxyFactory;
        private readonly Uri _serviceUri = new Uri(IPlayerServiceInternal.DefaultServiceUri);

        private static int _counter = 0;
        private static int _last_counter = 0;
        private static int _rpm = 0;
        private static readonly object _counter_guard = new object();
        private static DateTime LastReportTime = DateTime.Now;

        public PlayerOrchestrator(StatelessServiceContext context)
            : base(context)
        {
            _serviceProxyFactory = new ServiceProxyFactory();
        }

        // This constructor is used in testing
        public PlayerOrchestrator(StatelessServiceContext context, string serviceUri, IServiceProxyFactory proxyFactory)
            : base(context)
        {
            _serviceUri = new Uri(serviceUri);
            _serviceProxyFactory = proxyFactory;
        }

        IPlayerServiceInternal GetServiceProxy(long partitionKey)
        {
            return _serviceProxyFactory.CreateServiceProxy<IPlayerServiceInternal>(
                _serviceUri,
                new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(partitionKey));
        }

        long GetPartitionKey(string key)
        {
            byte[] hash;
            using (MD5 md5 = MD5.Create())
            {
                md5.Initialize();
                md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                hash = md5.Hash;
            }
            return BitConverter.ToInt64(hash);

        }

        private string GenerateKey()
        {
            return Guid.NewGuid() + "-" + DateTime.UtcNow.Ticks.ToString();
        }

        public async Task<string> AddPlayer(string name)
        {
            var key = GenerateKey();
            IPlayerServiceInternal proxy = GetServiceProxy(GetPartitionKey(key));

            IncreaseScopeCounter();
            var playerKey = await proxy.AddPlayer(key, name);
            return playerKey;
        }

        public async Task<string> DeletePlayer(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                throw new ArgumentException("Player unique id must be provided and cannot be empty.");
            }

            IPlayerServiceInternal proxy = GetServiceProxy(GetPartitionKey(uuid));

            IncreaseScopeCounter();
            return await proxy.DeletePlayer(uuid);
        }
        public async Task<IEnumerable<PlayerData>> GetPlayers()
        {
            List<PlayerData> playerDataList = new List<PlayerData>();

            IncreaseScopeCounter();
            for (int partition = 0; partition < Constants.partitionCount; partition++)
            {
                await GetServiceProxy(partition).GetPlayers().ContinueWith(async d => playerDataList.AddRange(await d));
            }
            return playerDataList;
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            DefineMetricsAndPolicies();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rpm = RPM;
                ServiceEventSource.Current.ServiceMessage(Context, "RPM: {0}, reported at {1}",
                    rpm,
                    DateTime.Now);

                this.Partition.ReportLoad(new List<LoadMetric> { new LoadMetric(Constants.ServiceLoadMetricName, rpm) });

                await Task.Delay(Constants.ReportingInterval, cancellationToken);
            }
        }
        private class ServiceMetric : KeyedCollection<string, ServiceLoadMetricDescription>
        {
            protected override string GetKeyForItem(ServiceLoadMetricDescription item)
            {
                return item.Name;
            }
        }
        private void DefineMetricsAndPolicies()
        {
            var fabricClient = new FabricClient();
            Uri appName = new Uri($"{Context.CodePackageActivationContext.ApplicationName}/PlayerOrchestrator");

            StatelessServiceUpdateDescription serviceUpdateDescription = new StatelessServiceUpdateDescription();
            ServiceMetric metric = new ServiceMetric();

            StatelessServiceLoadMetricDescription serviceLoadMetricDescription = new StatelessServiceLoadMetricDescription
            {
                Name = Constants.ServiceLoadMetricName,
                DefaultLoad = 0,
                Weight = ServiceLoadMetricWeight.High
            };

            if (serviceUpdateDescription.Metrics == null)
            {
                serviceUpdateDescription.Metrics = metric;
            }
            serviceUpdateDescription.Metrics.Add(serviceLoadMetricDescription);

            PartitionInstanceCountScaleMechanism scaleMechanism = new PartitionInstanceCountScaleMechanism
            {
                MinInstanceCount = Constants.MinInstanceCount,
                MaxInstanceCount = Constants.MaxInstanceCount,
                ScaleIncrement = Constants.ScaleIncrement
            };

            AveragePartitionLoadScalingTrigger scalingTrigger = new AveragePartitionLoadScalingTrigger
            {
                MetricName = Constants.ServiceLoadMetricName,
                LowerLoadThreshold = Constants.LowerLoadTreshold,
                UpperLoadThreshold = Constants.UpperLoadTreshold,
                ScaleInterval = Constants.ScaleInterval
            };

            ScalingPolicyDescription scalingPolicy = new ScalingPolicyDescription(scaleMechanism, scalingTrigger);

            if (serviceUpdateDescription.ScalingPolicies == null)
            {
                serviceUpdateDescription.ScalingPolicies = new List<ScalingPolicyDescription>();
            }
            serviceUpdateDescription.ScalingPolicies.Add(scalingPolicy);

            fabricClient.ServiceManager.UpdateServiceAsync(appName, serviceUpdateDescription);

        }

        /// <summary>
        /// Scope counter is used to calculate requests per minute metrics.
        /// </summary>
        private void IncreaseScopeCounter()
        {
            lock (_counter_guard)
            {
                if (_counter == int.MaxValue)
                    throw new SystemException("Too many concurent requests.");
                _counter++;
            }
        }

        /// <summary>
        /// Calculate metric Requests per minute, based on scope counter and time from last call.
        /// </summary>
        private void CalculateRPM()
        {
            var Now = DateTime.Now;
            if (Now > LastReportTime + Constants.ReportingInterval)
            {
                var timespan = Now - LastReportTime;
                LastReportTime = Now;
                double ticks = _counter - _last_counter;
                _rpm = (int)Math.Floor((ticks * 1000 * 60) / timespan.TotalMilliseconds);
                _counter -= _last_counter;
                _last_counter = _counter;
            }
        }

        private int RPM
        {
            get
            {
                lock (_counter_guard)
                {
                    CalculateRPM();
                    return _rpm;
                }
            }
        }

    }
}
