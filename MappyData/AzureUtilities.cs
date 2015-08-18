using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MappyData
{
    /// <summary>
    /// A collection of Azure-related utilities, factored out here to let both the ASP.Net site and the CLI tool work with them.
    /// </summary>
    public static class AzureUtilities
    {
        public const string DefaultRoutePointsTable = "routepoints";
        public const string RoutePointsTableKey = "RoutePointsTable";

        // Cache the configuration data.
        private static ConcurrentDictionary<string, string> _ConfigurationEntries = new ConcurrentDictionary<string, string>();
        private static ConcurrentDictionary<string, CloudTable> _Tables = new ConcurrentDictionary<string, CloudTable>();

        /// <summary>
        /// Pulls configuration entries from either the CloudConfigurationManager (app/web.config or the cscfg if deployed) or the environment variable of the same name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The found value, or null.</returns>
        /// <remarks>Side-effect: Stores the result in the dictionary cache.</remarks>
        public static string FromConfiguration(string name)
        {
            return _ConfigurationEntries.GetOrAdd(name, x => CloudConfigurationManager.GetSetting(x) ?? Environment.GetEnvironmentVariable(name));
        }

        /// <summary>
        /// Get/create the Azure Table Storage table for route points.
        /// </summary>
        /// <param name="connectionStringKey"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static CloudTable GetRoutePointsTable(string connectionStringKey = null, string tableName = null)
        {
            tableName = tableName ?? FromConfiguration(RoutePointsTableKey) ?? DefaultRoutePointsTable;
            return _Tables.GetOrAdd(tableName, (name) =>
            {
                Trace.TraceInformation("Route points table: {0}", name);
                var table = GetTableClient(connectionStringKey)
                    .GetTableReference(name);
                table.CreateIfNotExists();

                return table;
            });
        }

        /// <summary>
        /// Get all of the route points within a given timespan from the azure table storage table.
        /// </summary>
        /// <param name="routePointsTable"></param>
        /// <param name="starting"></param>
        /// <param name="ending"></param>
        /// <returns></returns>
        public static IEnumerable<RoutePointTS> GetRoutePointsFromTo(CloudTable routePointsTable,
            DateTime starting, DateTime ending)
        {
            return from e in routePointsTable.CreateQuery<RoutePointTS>()
                   where e.Timestamp > starting
                   && e.Timestamp <= ending
                   select e;
        }

        public static CloudTableClient GetTableClient(string connectionStringOrKey = null)
        {
            var key = AzureUtilities.FromConfiguration(connectionStringOrKey ?? "StorageConnectionString");
            if (key == null)
            {
                // NOTE: In the real world, you'd want to remove this, so you didn't log keys.
                Trace.TraceInformation("Couldn't find '{0}' as setting, assuming it's the actual key.", connectionStringOrKey);
                key = connectionStringOrKey;
            }

            var storageAccount = CloudStorageAccount.Parse(key);

            return storageAccount.CreateCloudTableClient();
        }

        public static string ServiceBusConnectionString(string serviceBusNamespace, string sharedAccessKeyName, string sharedAccessKey)
        {
            return string.Format("Endpoint=sb://{0}.servicebus.windows.net/;SharedAccessKeyName={1};SharedAccessKey={2}",
                serviceBusNamespace,
                sharedAccessKeyName,
                sharedAccessKey);
        }

        public static string StorageConnectionString(string storageName, string storageKey)
        {
            return string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                storageName, storageKey);
        }

        /// <summary>
        /// Attach an event processor host to an event hub.
        /// </summary>
        /// <param name="processorName">Used in blob storage for grouping stored partition offsets into a container.</param>
        /// <param name="serviceBusConnectionString">Full connection string to the service bus containing the hub.</param>
        /// <param name="offsetStorageConnectionString">Full connection string to the storage stamp for storing offsets.</param>
        /// <param name="eventHubName">Name of the event hub.</param>
        /// <param name="consumerGroupName">Name of the consumer group (use $Default if you don't know).</param>
        /// <param name="processorFactory">EventProcessorFactory instance.</param>
        /// <returns></returns>
        public static async Task<EventProcessorHost> AttachProcessorForHub(
            string processorName, 
            string serviceBusConnectionString,
            string offsetStorageConnectionString,
            string eventHubName,
            string consumerGroupName,
            IEventProcessorFactory processorFactory)
        {
            var eventProcessorHost = new EventProcessorHost(processorName, eventHubName, consumerGroupName, serviceBusConnectionString, offsetStorageConnectionString);
            await eventProcessorHost.RegisterEventProcessorFactoryAsync(processorFactory);

            return eventProcessorHost;
        }

        public static T DeserializeMessage<T>(EventData data, JsonSerializerSettings settings = null)
        {
            var dataStr = Encoding.UTF8.GetString(data.GetBytes());
            Trace.TraceInformation("Attempting to deserialize '{0}'", dataStr);
            return settings == null
                ? JsonConvert.DeserializeObject<T>(dataStr)
                : JsonConvert.DeserializeObject<T>(dataStr, settings);
        }
    }

    public class RoutePointProcessorFactory : IEventProcessorFactory
    {
        public RoutePointProcessorFactory(Action<IRoutePoint> onItem)
        {
            this.onItemCB = onItem;
        }

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            return new RoutePointProcessor(this.onItemCB);
        }

        private Action<IRoutePoint> onItemCB;
    }

    /// <summary>
    /// Processor for route points - stores offsets on every hundredth point.
    /// </summary>
    public class RoutePointProcessor : IEventProcessor
    {
        public RoutePointProcessor(Action<IRoutePoint> onItem)
        {
            this.onItemCB = onItem;
        }

        public const int MessagesBetweenCheckpoints = 100;

        private int untilCheckpoint = MessagesBetweenCheckpoints;
        private Action<IRoutePoint> onItemCB;

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Trace.TraceInformation("Closing RouteItemProcessor: {0}", reason);
            return Task.FromResult(false);
        }

        public Task OpenAsync(PartitionContext context)
        {
            Trace.TraceInformation("Opening RouteItemProcessor");
            return Task.FromResult(false);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            foreach (var message in messages)
            {
                var routeItem = AzureUtilities.DeserializeMessage<RoutePointEH>(message);
                try
                {
                    this.onItemCB(routeItem);
                }
                catch (Exception e)
                {
                    Trace.TraceError("Failed to process {0}: {1}", routeItem, e);
                    throw;
                }
                this.untilCheckpoint--;
                if (this.untilCheckpoint == 0)
                {
                    await context.CheckpointAsync();
                    this.untilCheckpoint = MessagesBetweenCheckpoints;
                }
            }
        }
    }
}
