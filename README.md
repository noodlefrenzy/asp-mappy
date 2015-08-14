# asp-mappy

Example of wiring up SignalR to connect an EventHub receiving GPS data to a Leaflet map rendering it.

# Other Software

This code relies on Leaflet.js and SignalR through reference, and includes the following directly:

* Chroma: Used to randomize the color of paths with prettier colors than I could do myself. It's a fantastic library for working with colors and color-spaces, highly recommended.
* Leaflet-heat: Used to generate heatmaps on the leaflet map for identifying "hot spots" in walking traffic. Incredibly easy to use.

I've included licenses for both of the above within the source tree at the same level at which I'm using them.