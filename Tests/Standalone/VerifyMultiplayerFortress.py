"""Verify completed fortress runs, including real melee damage and late snapshots."""
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "Builds/MultiplayerFortressAcceptance")
teams = sys.argv[2:] or ["A", "B"]
checks = 0

def check(value, label):
    global checks
    if not value:
        raise AssertionError(label)
    checks += 1

for team in teams:
    winner = "Team" + team
    loser = "TeamB" if team == "A" else "TeamA"
    logs = {name: (root / team / (name + ".log")).read_text(encoding="utf-8-sig")
            for name in ("host", "client", "late")}
    expected_health = ("1000", "0") if team == "A" else ("0", "1000")
    for peer, log in logs.items():
        check(not re.search(r"\[FortressCheck\] FAIL|Exception:|Start failed:", log), team + " " + peer + " no failures")
        check("[FortressComplete] server=" in log, peer + " complete")
        states = re.findall(r"\[FortressState\] server=(True|False) state=Finished ended=true winner=(Team[AB]) count=(\d+) kingA=([\d.]+) kingB=([\d.]+) events=(\d+)", log)
        check(bool(states), peer + " received finished snapshot")
        check(all(s[1] == winner and s[2] == "1" and s[3:5] == expected_health for s in states), peer + " immutable winner and King health")
        check(all(s[5] == ("0" if peer == "late" else "1") for s in states), peer + " one event/no late replay")
        for label in ("one-replicated-winner", "end-notification-once", "only-opposing-king-defeated",
                      "repeat-or-client-finish-rejected", "post-end-health-frozen", "post-end-player-combat-blocked",
                      "post-end-xp-blocked", "post-end-direct-xp-blocked", "post-end-inventory-blocked",
                      "post-end-ai-blocked", "post-end-camp-blocked"):
            check("[FortressCheck] PASS " + label in log, peer + " " + label)
        if peer != "host":
            check("[FortressKingHit]" not in log and "[FortressGuardHit]" not in log, peer + " no damage decision events")
        if peer != "late":
            for label in ("server-owned-fortresses", "guard-team-filter", "authoritative-perception",
                          "king-forged-hit-rejected", "living-king-cannot-end-match"):
                check("[FortressCheck] PASS " + label in log, peer + " " + label)
    check("[FortressCheck] PASS client-ai-passive" in logs["client"], "replica AI passive")
    check("[FortressCheck] PASS client-king-state-guard" in logs["client"], "client raw King damage blocked")
    check("[FortressCheck] PASS reentrant-finish-and-damage-blocked" in logs["host"], "reentrant end blocked")
    for kind, expected_damage in (("King", 1000), ("Guard", 85)):
        hits = re.findall(r"\[Fortress" + kind + r"Hit\] team=(Team[AB]) amount=([\d.]+) attacker=(Team[AB])", logs["host"])
        check(bool(hits) and all(h[0] == loser and h[2] == winner for h in hits), kind + " damage only from enemy team")
        check(sum(float(h[1]) for h in hits) == expected_damage, kind + " exactly one health pool depleted")
    after_end = logs["host"].split("state=Finished", 1)[1]
    check("[FortressKingHit]" not in after_end and "[FortressGuardHit]" not in after_end, "no combat damage after finish")
print(f"PASS: {checks} multiplayer fortress checks across {', '.join(teams)}")
