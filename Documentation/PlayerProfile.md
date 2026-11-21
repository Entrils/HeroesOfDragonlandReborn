# M67 Player profile

Open **Profile** in the main menu to edit the local display name and see lifetime totals. Audio, mouse sensitivity, graphics options and tutorial status use the same profile. Existing settings and tutorial flags are imported when a local profile is first created. Cosmetic selections have placeholder IDs for appearance and banner.

`ProfileData` contains the local name, settings, tutorial flags, totals, cosmetic identifiers and a compact list of recent result receipts for deduplication. `IProfileStore` is the storage boundary; the development implementation saves one JSON value through Unity PlayerPrefs under `HOD.Profile.v1`. A future backend can implement the same storage boundary with a synchronized cache.

`MatchSessionData` holds the server's transient statistics for one online round. The existing gameplay systems still own level, attributes, inventory and equipment. None of that data enters the profile store or is restored from it.

The server sends each admitted, connected human their own snapshot after a King victory. Totals count completed online rounds. A kill is the last hit on an opposing avatar; deaths count gameplay deaths regardless of source. Camp clears use eligible winning-team contributors. Boss and King kills go to the credited last hitter. Offline training updates the tutorial flag. A player who is disconnected at result delivery has no local receipt for that round.

## Verification

`./Tests/Standalone/RunGameplayHud.ps1 -Executable Builds/M67/Dragonland.exe -VerifyProfile` runs two Photon rounds with separate development profile keys for host and client. It also checks fresh round gameplay state, then restarts both profiles to verify persisted settings, name, tutorial status, cosmetic placeholder, wins/losses and duplicate result handling.

The host and client each restored two completed rounds, one win and one loss. See `Builds/LifecycleAcceptance-47699e8b/`. An additional Unity serialization check restored nonzero kills, deaths, camps, Kings and bosses and confirmed that the profile payload contains no level, attributes, inventory or equipment fields.
