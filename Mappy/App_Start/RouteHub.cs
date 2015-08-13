using Mappy.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mappy
{
    public class RouteHub : Hub
    {
        public RouteHub()
        {
        }

        public static IHubContext Hub()
        {
            return GlobalHost.ConnectionManager.GetHubContext<RouteHub>();
        }

        public static void Send(IHubContext hub, string userId, float lat, float lon)
        {
            hub.Clients.All.newPoint(userId, lat, lon);
        }

        public void Send(string userId, float lat, float lon)
        {
            Clients.All.newPoint(userId, lat, lon);
        }
    }

    public static class RoutePointSourceFactory
    {
        public static async Task StartAsync(string pointSourceName)
        {
            if (string.IsNullOrWhiteSpace(pointSourceName)) throw new ArgumentNullException("pointSourceName");
            switch (pointSourceName.ToLowerInvariant())
            {
                case "eventhub":
                    {
                        await new RouteEHReader().StartAsync();
                    }
                    break;

                case "random":
                    {
                        await new RouteCannedData().StartAsync();
                    }
                    break;

                case "tablestorage":
                    {
                        new RouteReader().Start();
                    }
                    break;

                default:
                    throw new ArgumentException("Unknown point source: " + pointSourceName);
            }
        }
    }

    public class RouteEHReader
    {
        public RouteEHReader()
        {
            this.EventHubConnectionKey = "EventHubConnectionString";
            this.OffsetStorageConnectionKey = "StroeerDashConnectionString";
            this.EventHubName = AzureUtilities.FromConfiguration("EventHubName");
            this.ConsumerGroupName = AzureUtilities.FromConfiguration("ConsumerGroupName") ?? "$Default";
        }

        public async Task StartAsync()
        {
            Trace.TraceInformation("Connecting to {0}/{1}/{2}, storing in {3}", this.EventHubConnectionKey, this.EventHubName, this.ConsumerGroupName, this.OffsetStorageConnectionKey);

            var factory = new RoutePointProcessorFactory(item =>
            {
                Trace.TraceInformation("From EH: {0} @ ({1}, {2})", item.UserID, item.Latitude, item.Longitude);
                RouteHub.Send(RouteHub.Hub(), item.UserID, (float)item.Latitude, (float)item.Longitude);
            });
            await AzureUtilities.AttachProcessorForHub("stroeerdash", this.EventHubName, this.ConsumerGroupName, this.EventHubConnectionKey, this.OffsetStorageConnectionKey, factory);
        }

        private string EventHubConnectionKey { get; set; }

        private string OffsetStorageConnectionKey { get; set; }

        private string EventHubName { get; set; }

        private string ConsumerGroupName { get; set; }
    }

    public class RouteReader
    {
        public RouteReader()
        {
            this.RouteItemTable = AzureUtilities.GetRoutePointsTable();
            this.LastCheck = DateTime.UtcNow.AddHours(-24);
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
                        RouteHub.Send(RouteHub.Hub(), item.UserID, (float)item.Latitude, (float)item.Longitude);
                    }
                    this.LastCheck = this.LastCheck.AddSeconds(quanta);
                    if (this.LastCheck > DateTime.UtcNow) this.LastCheck = DateTime.UtcNow;
                }
            });
        }

        private CloudTable RouteItemTable { get; set; }

        private DateTime LastCheck { get; set; }
    }

    public class RouteCannedData
    {
        public RouteCannedData(int seed = 17, int millisBetweenPoints = 250, int numUsers = 10, double maxRangeLatLong = .5)
        {
            this._random = new Random(seed);
            this._timeBetweenPoints = TimeSpan.FromMilliseconds(millisBetweenPoints);
            this._numUsers = numUsers;
            this._maxRange = maxRangeLatLong;
            this.Cancel = new CancellationToken();
        }

        public CancellationToken Cancel;

        private Random _random;
        private TimeSpan _timeBetweenPoints;
        private int _numUsers;
        private double _maxRange;
        private double _centerLat = MvcApplication.SeattleLatitude;
        private double _centerLon = MvcApplication.SeattleLongitude;
        private Dictionary<int, RoutePoint> _previousPoint = new Dictionary<int, RoutePoint>();

        public async Task StartAsync()
        {
            while(!this.Cancel.IsCancellationRequested)
            {
                var point = GeneratePoint();
                RouteHub.Send(RouteHub.Hub(), point.UserID, (float)point.Latitude, (float)point.Longitude);
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
