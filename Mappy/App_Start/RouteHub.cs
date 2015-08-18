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
    /// <summary>
    /// SignalR Hub for RoutePoint data. http://www.asp.net/signalr/overview/guide-to-the-api/hubs-api-guide-javascript-client for details.
    /// </summary>
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

}
