"""Verify a completed RunMultiplayerLoot.ps1 run; never accept partial logs."""
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "Builds/MultiplayerLootAcceptance")
logs = {name: (root / (name + ".log")).read_text(encoding="utf-8-sig")
        for name in ("host", "client", "late")}
checks = 0

def check(condition, label):
    global checks
    if not condition:
        raise AssertionError(label)
    checks += 1

for name, log in logs.items():
    check(not re.search(r"\[LootCheck\] FAIL|Exception:|Start failed:", log), name + " no failures")

for label in ("opposing-teams", "server-distance-rejection", "unregistered-rejection",
              "server-obstruction-rejection", "dead-player-rejection", "full-inventory-atomic-rejection", "server-roll-retained",
              "team-b-can-win", "winner-receives-exact-retained-roll", "both-repeat-requests-rejected",
              "reentrant-competitor-rejected", "team-a-can-win", "remote-rpc-winner",
              "post-rpc-double-open-rejected"):
    check("[LootCheck] PASS " + label in logs["host"], label)
for label in ("client-direct-mutation-rejected", "client-item-add-rejected", "remote-rpc-submitted"):
    check("[LootCheck] PASS " + label in logs["client"], label)
check("[LootResponse] accepted=True" in logs["client"], "remote successful response")
check("[LootComplete] server" in logs["host"], "server completion")
check("[LootComplete] client" in logs["client"], "owner inventory replication")
pattern = r"\[LootState\] server=(True|False) id=(\[Id:\d+\]) open=[Tt]rue mirror=True winner=(\[Player:\d+\]) items=(\d+)"
states = {}
for name, log in logs.items():
    matches = re.findall(pattern, log)
    check(bool(matches), name + " replicated open presentation")
    states[name] = matches[-1]
check(states["host"][1:3] == states["client"][1:3] == states["late"][1:3], "same chest and winner on all peers")
check(states["host"][3] == "0" and states["late"][3] == "0", "loser and late joiner receive no loot")
check(int(states["client"][3]) > 0, "remote winner receives loot")
print(f"PASS: {checks} multiplayer loot checks")
