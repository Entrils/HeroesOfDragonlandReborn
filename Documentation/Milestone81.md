# Milestone 81 — Ranked 3v3

The online menu has Unranked, Ranked, Private Match, and Custom Lobby tabs. Ranked and Unranked use different Photon queue names and properties. Choose **Ranked > Find ranked match** to enter the ranked pool. All six slots must be ready humans at start. For rating credit, the server must complete the match and still count three humans per team at finish.

The local profile starts at 1000 and applies a fixed +25 for a win or −25 for a loss, with a floor of zero. Provisional status ends after the fifth rated result. The server decides when a result is rated, and the local profile applies its receipt once per result ID and persists rating, matches, wins and losses. The current implementation uses the queue and local rating model as a foundation. Account-backed ratings and rating-based pairing remain future work.

See [queue separation](../Assets/Game/Core/OnlineMatchmaking.cs), [rating eligibility and model](../Assets/Game/Core/RankedRating.cs), [result publication](../Assets/Game/Core/NetworkMatch.cs), and [local profile](../Assets/Game/Core/LocalPlayerProfile.cs).

## Verification

```powershell
./Tests/Standalone/RunRankedMatchmaking.ps1 -Executable $player
```

The harness opens an Unranked room, launches six Ranked player processes in an isolated queue, and checks the first rated round and an ineligible finish. The ranked fixture drives the finish through the King objective. The script then restarts two isolated profiles to verify persistence and duplicate receipt rejection. Logs go to `Builds/RankedAcceptance-*/`. The focused [rating validation](../Tests/EditorCommands/RankedRatingValidation.cs) covers the rating model and eligibility rules.
