"""Validate equipped client weapon behavior against authoritative host and replica logs."""
import pathlib
import re
import sys
root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "Builds/WeaponCombatValidation") / "weapon"
checks = 0

def check(value, message):
    global checks
    assert value, message
    checks += 1

ids = set()
for name in ("host", "client1"):
    log = (root / (name + ".log")).read_text(encoding="utf-8", errors="replace")
    check(not re.search(r"^(?:\w+\.)*\w*Exception:|^Assertion failed", log, re.M), name + ": no runtime exception")
    health = [float(value) for value in re.findall(r"\[CombatProbe\].*owner=\[Player:1\].*?hp=([\d.]+)", log)]
    check(bool(health) and health[-1] == 0, name + ": replicated victim death")
    check(65 in health and 30 in health, name + ": weapon base25 plus existing10 bonus deals35")
    check(all(b <= a for a, b in zip(health, health[1:])), name + ": coherent authoritative damage")
    weapons = re.findall(r"\[WeaponProbe\] owner=\[Player:2\] item=([a-f0-9]{32}) archetype=Sword duration=([\d.]+)", log)
    check(bool(weapons), name + ": equipped archetype and stable item ID replicated")
    ids.update(item for item, duration in weapons)
    check(any(abs(float(duration) - .55) < .001 for item, duration in weapons), name + ": server swing duration replicated")
    check("client-mutations-blocked=False" not in log, name + ": client mutation rejected")
    if name == "client1":
        check("client-mutations-blocked=True" in log, "client authority tamper check executed")
    else:
        check("[WeaponCheck] server equipped client sword" in log, "server equipped weapon fixture")
check(len(ids) == 1, "same equipped weapon ID across peers")
print(f"PASS equipped weapon multiplayer combat: {checks} checks")
