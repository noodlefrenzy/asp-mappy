using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MappyData
{
    public static class Constants
    {
        public const double SeattleLatitude = 47.5998;
        public const double SeattleLongitude = -122.3346;
    }

    /// <summary>
    /// GPS Point for a user, allows us to build up routes based on GPS snapshots.
    /// </summary>
    public interface IRoutePoint
    {
        double Latitude { get; }
        double Longitude { get; }
        DateTime MeasurementTime { get; }
        string UserID { get; }
    }

    /// <summary>
    /// Instantiation of GPS Point within Table Storage.
    /// </summary>
    public class RoutePointTS : TableEntity, IRoutePoint
    {
        public RoutePointTS() { }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string UserID { get; set; }

        public DateTime MeasurementTime { get; set; }
    }

    /// <summary>
    /// Instantiation of GPS Point from Event Hub.
    /// </summary>
    public class RoutePointEH : IRoutePoint
    {

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string UserID { get; set; }

        public DateTime MeasurementTime { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
