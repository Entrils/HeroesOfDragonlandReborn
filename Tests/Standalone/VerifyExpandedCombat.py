"""Check host/client records from RunMultiplayerCombat.ps1 heavy and block scenarios."""
import pathlib
import re
import sys
root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "Builds/ExpandedCombatValidation")
checks = 0

def check(value, message):
    global checks
    assert value, message
    checks += 1

for scenario in ("heavy", "block"):
    logs = {name: (root / scenario / (name + ".log")).read_text(encoding="utf-8", errors="replace") for name in ("host", "client1")}
    for name, log in logs.items():
        prefix = scenario + "/" + name + ": "
        check("client-mutations-blocked=False" not in log, prefix + "client mutations rejected")
        check(not re.search(r"^(?:\w+\.)*\w*Exception:|^Assertion failed", log, re.M), prefix + "no runtime exceptions")
        health = [float(x) for x in re.findall(r"\[CombatProbe\].*owner=\[Player:2\].*?hp=([\d.]+)", log)]
        check(bool(health) and health[-1] == 0, prefix + "replicated victim death")
        check(all(b <= a for a, b in zip(health, health[1:])), prefix + "monotonic damage")
        defense = re.findall(r"\[DefenseProbe\] owner=\[Player:(\d+)\] heavy=(\w+) block=(\w+) stagger=(\w+) breaks=(\d+)", log)
        check(bool(defense), prefix + "replicated defense snapshots")
        if scenario == "heavy":
            check(any(owner == "1" and heavy.lower() == "true" for owner, heavy, block, stagger, breaks in defense), prefix + "heavy type replicated")
            check(any(owner == "2" and stagger.lower() == "true" for owner, heavy, block, stagger, breaks in defense), prefix + "heavy stagger replicated")
            check(any(abs(h - 55) < .01 for h in health), prefix + "heavy deals 45 damage")
        else:
            check(any(owner == "2" and block.lower() == "true" for owner, heavy, block, stagger, breaks in defense), prefix + "held block replicated")
            check(any(owner == "2" and int(breaks) > 0 for owner, heavy, block, stagger, breaks in defense), prefix + "guard break replicated")
            check(any(abs(h - 93.75) < .01 for h in health), prefix + "blocked light deals 6.25 damage")
    check("client-mutations-blocked=True" in logs["client1"], scenario + ": client authority tamper check executed")
print(f"PASS expanded multiplayer combat: {checks} checks")
