using MappyData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Mappy.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            this.ViewBag.CenterLatitude = MappyData.Constants.SeattleLatitude;
            this.ViewBag.CenterLongitude = MappyData.Constants.SeattleLongitude;
            this.ViewBag.MapsId = AzureUtilities.FromConfiguration("OpenStreetMapId");
            this.ViewBag.MapsAccessToken = AzureUtilities.FromConfiguration("OpenStreetMapAccessToken");
            return View();
        }
    }
}
