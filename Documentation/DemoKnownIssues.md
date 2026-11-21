# Demo known issues

- Multiplayer requires six players and a live Photon connection. An empty Quick Play queue can create a lobby that waits for more players. Arrange a six-player group for a scheduled playtest.
- Reconnect depends on the original host and a live slot reservation. After host loss or reservation expiry, create or join a new room. Private room codes resolve within the configured EU region.
- Profile totals are stored locally. They are unsuitable as proof of competitive results.
- Earlier playtests observed AI stalls near fortress routes and intermittent pathing around flank exit corners. Capture a screenshot, map position and target when reporting a recurrence.
- Peak combat on low-end GPUs and six-player WAN soak are still unqualified. Record hardware, resolution, region, build version and the player log when reporting a problem.

See [Demo release checklist](DemoReleaseChecklist.md) for the remaining release gates and steps to reproduce the intended flow.

The candidate still displays the Playtest Alpha menu label. The DX11 smoke run also logged stripped or unsupported optional depth-of-field and Panini shader passes. The main menu and Settings rendered in that run.
