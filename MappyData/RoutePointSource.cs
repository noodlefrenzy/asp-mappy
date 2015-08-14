using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MappyData
{
    public static class RoutePointSourceFactory
    {
        public static async Task StartAsync(string pointSourceName, Action<IRoutePoint> onNewPoint)
        {
            if (string.IsNullOrWhiteSpace(pointSourceName)) throw new ArgumentNullException("pointSourceName");
            switch (pointSourceName.ToLowerInvariant())
            {
                case "eventhub":
                    {
                        await new EventHubRoutePointSource(onNewPoint).StartAsync();
                    }
                    break;

                case "random":
                    {
                        await new RandomRoutePointSource(onNewPoint).StartAsync();
                    }
                    break;

                case "tablestorage":
                    {
                        new TableStorageRoutePointSource(onNewPoint).Start();
                    }
                    break;

                default:
                    throw new ArgumentException("Unknown point source: " + pointSourceName);
            }
        }
    }

    public class EventHubRoutePointSource
    {
        public EventHubRoutePointSource(Action<IRoutePoint> onNewPoint)
        {
            this._onPoint = onNewPoint;
        }

        public async Task StartAsync()
        {
            var ehConnStr = AzureUtilities.ServiceBusConnectionString(
                AzureUtilities.FromConfiguration("MappyServiceBusNamespace"),
                AzureUtilities.FromConfiguration("MappyEventHubSASName"),
                AzureUtilities.FromConfiguration("MappyEventHubSASKey"));
            var storageConnStr = AzureUtilities.StorageConnectionString(
                AzureUtilities.FromConfiguration("MappyStorageName"),
                AzureUtilities.FromConfiguration("MappyStorageKey"));
            var eventHubName = AzureUtilities.FromConfiguration("MappyEventHubName");
            var consumerGroup = AzureUtilities.FromConfiguration("MappyConsumerGroupName") ?? "$Default";

            Trace.TraceInformation("Connecting to {0}/{1}/{2}, storing in {3}", ehConnStr, eventHubName, consumerGroup, storageConnStr);

            var factory = new RoutePointProcessorFactory(item =>
            {
                Trace.TraceInformation("From EH: {0} @ ({1}, {2})", item.UserID, item.Latitude, item.Longitude);
                this._onPoint(item);
            });
            await AzureUtilities.AttachProcessorForHub("mappy", ehConnStr, storageConnStr, eventHubName, consumerGroup, factory);
        }

        private Action<IRoutePoint> _onPoint;
    }

    public class TableStorageRoutePointSource
    {
        public TableStorageRoutePointSource(Action<IRoutePoint> onNewPoint)
        {
            this.RouteItemTable = AzureUtilities.GetRoutePointsTable();
            this.LastCheck = DateTime.UtcNow.AddHours(-24);
            this._onPoint = onNewPoint;
        }

        public void Start()
        {
            var quanta = 10000;
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    foreach (var item in AzureUtilities.GetRoutePointsFromTo(this.RouteItemTable, this.LastCheck, this.LastCheck.AddSeconds(quanta)))
                    {
                        Trace.TraceInformation("Sending point ({0}, {1}, {2})", item.UserID, item.Latitude, item.Longitude);
                        this._onPoint(item);
                    }
                    this.LastCheck = this.LastCheck.AddSeconds(quanta);
                    if (this.LastCheck > DateTime.UtcNow) this.LastCheck = DateTime.UtcNow;
                }
            });
        }

        private CloudTable RouteItemTable { get; set; }

        private DateTime LastCheck { get; set; }

        private Action<IRoutePoint> _onPoint;
    }

    public class RandomRoutePointSource
    {
        public RandomRoutePointSource(Action<IRoutePoint> onNewPoint, int seed = 17, int millisBetweenPoints = 250, int numUsers = 10, double maxRangeLatLong = .5)
        {
            this._random = new Random(seed);
            this._timeBetweenPoints = TimeSpan.FromMilliseconds(millisBetweenPoints);
            this._numUsers = numUsers;
            this._maxRange = maxRangeLatLong;
            this.Cancel = new CancellationToken();
            this._onPoint = onNewPoint;
        }

        public CancellationToken Cancel;

        private Random _random;
        private TimeSpan _timeBetweenPoints;
        private int _numUsers;
        private double _maxRange;
        private double _centerLat = MappyData.Constants.SeattleLatitude;
        private double _centerLon = MappyData.Constants.SeattleLongitude;
        private Dictionary<int, RoutePoint> _previousPoint = new Dictionary<int, RoutePoint>();
        private Action<IRoutePoint> _onPoint;

        public async Task StartAsync()
        {
            while (!this.Cancel.IsCancellationRequested)
            {
                var point = GeneratePoint();
                this._onPoint(point);
                await Task.Delay(_timeBetweenPoints);
            }
        }

        private RoutePoint GeneratePoint()
        {
            var user = _random.Next(1, _numUsers);
            RoutePoint prev, cur;
            if (_previousPoint.TryGetValue(user, out prev))
            {
                cur = new RoutePoint()
                {
                    Latitude = NextCoord(prev.Latitude, _maxRange / 500.0),
                    Longitude = NextCoord(prev.Longitude, _maxRange / 500.0),
                    UserID = user.ToString(),
                    MeasurementTime = DateTime.UtcNow
                };
            }
            else
            {
                cur = new RoutePoint()
                {
                    Latitude = NextCoord(_centerLat, _maxRange),
                    Longitude = NextCoord(_centerLon, _maxRange),
                    UserID = user.ToString(),
                    MeasurementTime = DateTime.UtcNow
                };
            }

            _previousPoint[user] = cur;
            Trace.TraceInformation("Generated {0}", cur);
            return cur;
        }

        private double NextCoord(double center, double maxRange)
        {
            return center + (_random.NextDouble() * maxRange) - (maxRange / 2.0);
        }

        private class RoutePoint : IRoutePoint
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public DateTime MeasurementTime { get; set; }
            public string UserID { get; set; }

            public override string ToString()
            {
                return string.Format("Pt({0} @ {1},{2} @ {3}", UserID, Latitude, Longitude, MeasurementTime.ToString("o"));
            }
        }
    }
}
