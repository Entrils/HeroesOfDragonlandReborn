# Demo performance budget

These are candidate targets. Validate on Windows x64 with an RTX 4060 Laptop-class GPU or equivalent, 16 GB RAM, DX11, 1920x1080 at 100% render scale. Sample a rendered host and client in a six-human match. Include lobby, mid-map camp combat, a crowded fortress push, King victory and return to the lobby. Record hardware, quality settings, player count and region with each capture.

| Metric | Target | Measurement |
| --- | --- | --- |
| Frame time | 16.7 ms p95 per gameplay window; 33.3 ms p99 | Presented frame interval. Measure main, render and GPU time separately to locate bottlenecks. |
| Draw calls | 1,200 p95 per frame | Unity Rendering Profiler draw calls including shadows. |
| Triangles | 2 million p95 per frame | Unity Rendering Profiler triangles including shadow passes. |
| Memory | 3 GiB peak private bytes per player process; growth under 100 MiB after three warm rounds and lobby reset | Record peak during play and compare the same lobby state after each round. |
| GC allocations | 8 KiB/frame mean in steady gameplay; no GC pause over 4 ms | Unity Profiler allocation and GC markers. Track loading separately. |
| Network traffic | Host up to 128 KiB/s outbound and 64 KiB/s inbound; client up to 32 KiB/s in either direction | Fusion transport bytes per second, 10-second rolling averages in a six-player match. Report RTT and packet loss with traffic. |
| Simultaneous AI | Up to 48 active authoritative agents as a profiling target, with 2 ms p95 aggregate AI CPU time per host frame | Count live neutral enemies, bosses, Kings and any development bots in the stress fixture. Record the observed peak. This target does not enforce a runtime spawn cap. |

The earlier 720p DX11 baseline used two processes and four bots. It measured 4.26 to 5.03 ms GPU time, about 39 to 40 KB allocated per frame and approximately 5.3 to 6.6 KB/s outbound host traffic. The allocation baseline exceeds the proposed target and needs a fresh profile to identify the dominant allocator. Draw calls, triangles, peak memory and full-roster performance still need current captures. Optimize measured hotspots before changing assets or reducing gameplay density. See [Alpha known issues](KnownIssues.md) for the earlier capture context.
