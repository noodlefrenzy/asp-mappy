using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Mappy.Startup))]

namespace Mappy
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Disable auth...
            //ConfigureAuth(app);
            // ... and enable SignalR
            app.MapSignalR();
        }
    }
}
