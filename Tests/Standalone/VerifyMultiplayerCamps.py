"""Acceptance from actual Host/Client logs: camp kills, XP, one chest, late join and authority."""
import argparse
import pathlib
import re

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('root', nargs='?', default='Builds/MultiplayerCampValidation')
parser.add_argument('--scenarios', nargs='+', choices=['combat', 'removed'], default=['combat', 'removed'])
args = parser.parse_args()
checks = 0

def check(value, label):
    global checks
    if not value:
        raise AssertionError(label)
    checks += 1

state_pattern = re.compile(r'\[CampState\] server=(True|False) id=\[Id:(\d+)\] state=(\w+) dead=(\d+) total=(\d+) completions=(\d+) xp=(\d+) rewarded=(\d+) chest=\[Id:(None|\d+)\] visible=(\d+) players=(\d+)')
xp_pattern = re.compile(r'\[CampXP\] participant=(\d+) level=(\d+) xp=(\d+) points=(\d+) revision=(\d+) mirror=(\d+),(\d+),(\d+)')
for scenario in args.scenarios:
    peers = ['host', 'client1'] if scenario == 'removed' else ['host', 'client1', 'client2', 'client3', 'client4', 'client5', 'late']
    logs = {peer: (pathlib.Path(args.root) / scenario / (peer + '.log')).read_text(encoding='utf-8', errors='replace') for peer in peers}
    camp_ids, chest_ids = set(), set()
    for peer, log in logs.items():
        label = f'{scenario}/{peer}'
        check('[CampCheck] FAIL' not in log, label + ': no assertion failures')
        check(not re.search(r'\b\w*Exception:|Assertion failed|StartGame Failed|Camp initialization failed', log), label + ': no runtime errors')
        states = state_pattern.findall(log)
        check(bool(states), label + ': replicated camp received')
        camp_ids.update(row[1] for row in states)
        chest_ids.update(row[8] for row in states if row[8] != 'None')
        check(all(int(r[3]) <= int(r[4]) and int(r[5]) <= 1 and int(r[9]) <= 1 for r in states), label + ': bounded membership, one completion/chest')
        check(all(r[2] != 'Cleared' or r[3] == r[4] == '3' for r in states), label + ': only all-dead can clear')
        xp_rows = xp_pattern.findall(log)
        check(bool(xp_rows), label + ': player progression received')
        if peer != 'host':
            check('[CampXPEvent]' not in log and '[CampDeath]' not in log and '[CampClearEvent]' not in log, label + ': no client reward/death decisions')
            check('[CampCheck] PASS replica-xp-guard' in log, label + ': direct client grants/resets/allocation rejected')
            check(all(r[0] == 'False' for r in states), label + ': client authority')
        if scenario == 'combat':
            check('[CampComplete] cleared' in log, label + ': full chain finished')
            final = states[-1]
            check(final[2:8] == ('Cleared', '3', '3', '1', '100', '6') and final[9] == '1', label + ': replicated completion and full roster reward')
            check('[CampCheck] PASS one-linked-server-chest' in log, label + ': chest belongs to camp')
            if peer != 'host':
                check('[CampCheck] PASS replica-chest-guard' in log, label + ': client chest mutation rejected')
            if peer != 'late':
                check(any(r[10] == '6' for r in states), label + ': real six-player session')
                check('[CampCheck] PASS live-camp-cannot-complete' in log, label + ': live member guard')
            else:
                check(all(r[2] == 'Cleared' for r in states), label + ': late join receives final camp state')
            latest = {int(r[0]): r for r in xp_rows if int(r[4]) > 0}
            for participant, row in latest.items():
                expected = ('1', '0', '0') if participant == 7 else ('2', '0', '1')
                check(row[1:4] == expected and row[5:8] == expected, f'{label}/P{participant}: accepted XP and local mirror')
            check({1, 2, 3, 4, 5}.issubset(latest), label + ': progression of other participants visible')
            if peer == 'late':
                check(7 in latest, label + ': late player has no retroactive reward')
        else:
            check(all(r[2] in ('Pending', 'Active') and r[5:9] == ('0','0','0','None') and r[9] == '0' for r in states), label + ': despawn never clears or spawns rewards')
            check(all(r[1:4] == ('1', '0', '0') for r in xp_rows if int(r[4]) > 0), label + ': no XP without clear')
    check(len(camp_ids) == 1, scenario + ': same camp identity across peers')
    if scenario == 'combat':
        check(len(chest_ids) == 1, 'same chest identity across peers and late join')
        check(logs['host'].count('[CampClearEvent] count=1') == 1, 'one server completion event')
        for index, hp in enumerate([60, 180, 85]):
            check(logs['host'].count(f'[CampDeath] index={index} count=1 server=True') == 1, f'{index}: real server death once')
            hits = re.findall(rf'\[CampHit\] index={index} amount=([\d.]+) player=True', logs['host'])
            check(sum(map(float, hits)) == hp, f'{index}: killed by production player combat')
        for participant in range(1, 7):
            check(logs['host'].count(f'[CampXPEvent] participant={participant} amount=100 server=True') == 1, f'P{participant}: exactly one server XP grant')
        check('[CampXPEvent] participant=7' not in logs['host'], 'late join does not grant old rewards')
        for marker in ['reentrant-completion-rejected', 'repeat-completion-rejected', 'duplicate-xp-rejected']:
            check('[CampCheck] PASS ' + marker in logs['host'], marker)
    else:
        check('[CampComplete] removed' in logs['host'] and '[CampCheck] PASS despawn-is-not-death' in logs['host'], 'removed live member stays unresolved')
print(f'PASS: {checks} network camp checks across {", ".join(args.scenarios)}.')
