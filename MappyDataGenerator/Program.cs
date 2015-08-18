using MappyData;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MappyDataGenerator
{
    /// <summary>
    /// Simple program to write randomly-generated user paths to EventHub for testing the client.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            WriteToEventHub();
        }

        static void WriteToEventHub()
        {
            var ehConnStr = AzureUtilities.ServiceBusConnectionString(
                AzureUtilities.FromConfiguration("MappyServiceBusNamespace"),
                AzureUtilities.FromConfiguration("MappyEventHubSASName"),
                AzureUtilities.FromConfiguration("MappyEventHubSASKey"));
            var eventHubName = AzureUtilities.FromConfiguration("MappyEventHubName");

            var eventHubClient =
                EventHubClient.CreateFromConnectionString(ehConnStr, eventHubName);

            new RandomRoutePointSource(pt =>
            {
                Console.WriteLine("Sending {0}", pt);
                var data = new EventData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pt)));
                data.PartitionKey = pt.UserID;
                eventHubClient.Send(data);
            }).StartAsync().Wait();
        }
    }
}
