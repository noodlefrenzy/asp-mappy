using MappyData;
using Microsoft.WindowsAzure;
using System.Diagnostics;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Mappy
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Fire and forget
            RoutePointSourceFactory.StartAsync(AzureUtilities.FromConfiguration("RoutePointSource"), 
                pt => RouteHub.Send(RouteHub.Hub(), pt.UserID, (float)pt.Latitude, (float)pt.Longitude));
        }
    }
}
