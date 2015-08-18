# asp-mappy

Example of wiring up SignalR to connect an EventHub receiving GPS data to a Leaflet map rendering it.

# Introduction

Simple codebase wiring up geo-data from Event Hubs through SignalR to a Leaflet.js front-end. The Index.cshtml contains the lion's share of the JS code for dealing with Leaflet layers (for the paths, markers, and heatmaps). `RouteHub.cs` is a simple SignalR hub (see [this article on hubs](http://www.asp.net/signalr/overview/guide-to-the-api/hubs-api-guide-javascript-client) for details). `AzureUtilities.cs` contains code for connecting to the various pieces (service bus, event hubs, table storage), as well as the `IEventProcessor` implementation.

See [my post on my blog](http://www.mikelanzetta.com/2015/08/real-time-mapping-with-signalr-and-event-hubs/) for details.

# Other Software

This code relies on Leaflet.js and SignalR through reference, and includes the following directly:

* Chroma: Used to randomize the color of paths with prettier colors than I could do myself. It's a fantastic library for working with colors and color-spaces, highly recommended.
* Leaflet-heat: Used to generate heatmaps on the leaflet map for identifying "hot spots" in walking traffic. Incredibly easy to use.

I've included licenses for both of the above within the source tree at the same level at which I'm using them.