"""Validate real six-player PvE, replica passivity, authoritative hits/death and late join."""
import argparse
import pathlib
import re
import math

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('root', nargs='?', default='Builds/MultiplayerPveValidation')
parser.add_argument('--scenarios', nargs='+', choices=['combat', 'disconnect'], default=['combat', 'disconnect'])
args = parser.parse_args()
root = pathlib.Path(args.root)
checks = 0

def check(ok, label):
    global checks
    if not ok:
        raise AssertionError(label)
    checks += 1

pattern = re.compile(r'\[PveState\] index=(\d+) id=\[Id:(\d+)\] server=(True|False) hp=([\d.]+) max=([\d.]+) dead=(true|false) state=(\w+) target=\[Id:(\d+|None)\] attack=(\d+) tick=(\d+) dt=([\d.]+) simulation=(\d+) agent=(True|False) view=(True|False) x=([-\d.]+) z=([-\d.]+)', re.I)
for scenario in args.scenarios:
    peers = ['host', 'client1'] if scenario == 'disconnect' else ['host', 'client1', 'client2', 'client3', 'client4', 'client5', 'late']
    logs = {peer: (root / scenario / (peer + '.log')).read_text(encoding='utf-8', errors='replace') for peer in peers}
    records = {}
    for peer, log in logs.items():
        check('[PveCheck] FAIL' not in log, f'{scenario}/{peer}: no failed assertions')
        check(not re.search(r'\b\w*Exception:|Assertion failed|StartGame Failed', log), f'{scenario}/{peer}: no exceptions')
        rows = [m.groups() for m in pattern.finditer(log)]
        check(bool(rows), f'{scenario}/{peer}: received state')
        records[peer] = rows
        check({int(r[0]) for r in rows} == {0, 1, 2}, f'{scenario}/{peer}: all enemy types')
        check(all(0 <= float(r[3]) <= float(r[4]) and (float(r[3]) == 0) == (r[5].lower() == 'true') for r in rows), f'{scenario}/{peer}: coherent health/death')
        for index, hp in [(0, 60), (1, 180), (2, 85)]:
            check(all(float(r[4]) == hp for r in rows if int(r[0]) == index), f'{scenario}/{peer}/{index}: definition health')
        if peer != 'host':
            check(all(int(r[11]) == 0 and r[12] == 'False' for r in rows), f'{scenario}/{peer}: no AI ticks/agent on client')
            check('[PveDeath]' not in log, f'{scenario}/{peer}: no locally decided death event')
            for index in range(3):
                check(f'[PveCheck] PASS replica-guards index={index}' in log, f'{scenario}/{peer}/{index}: guarded direct client mutation')
        if scenario == 'combat':
            check('[PveComplete] all-dead count=3' in log, f'{peer}: sees authoritative corpses')
            if peer != 'late':
                check('[PveSession] players=6 enemies=3' in log, f'{peer}: six-player session')
    ids = {r[0]: r[1] for r in records['host']}
    player_ids = set(re.findall(r'\[Multiplayer\] spawned participant=\d+ object=\[Id:(\d+)\]', logs['host']))
    check(all(r[7] == 'None' or r[7] in player_ids for r in records['host']), f'{scenario}: server targets only registered players')
    ticks = {(r[0], r[8]): r[9] for r in records['host'] if int(r[8]) > 0}
    for peer, rows in records.items():
        check(all(ids[r[0]] == r[1] for r in rows), f'{scenario}/{peer}: same enemy identities')
        check(all(ticks[(r[0], r[8])] == r[9] for r in rows if (r[0], r[8]) in ticks), f'{scenario}/{peer}: same accepted attack ticks')
    if scenario == 'combat':
        hits = re.findall(r'\[PveHit\] kind=enemy index=(\d+) victim=\[Id:(\d+)\] sequence=(\d+) amount=([\d.]+)', logs['host'])
        check({int(h[0]) for h in hits} == {0, 1, 2}, 'all profiles deal real server damage')
        check(len(hits) == len({h[:3] for h in hits}), 'one hit per enemy swing/victim')
        damage = {0: 8, 1: 20, 2: 12}
        check(all(0 < float(h[3]) <= damage[int(h[0])] for h in hits), 'damage bounded by server definitions (last hit may be clamped)')
        player_hits = re.findall(r'\[PveHit\] kind=player index=(\d+) source=\[Id:(\d+)\] sequence=(\d+) amount=([\d.]+)', logs['host'])
        check({int(h[0]) for h in player_hits} == {0, 1, 2}, 'players can damage every enemy type')
        check(len(player_hits) == len({h[:3] for h in player_hits}), 'no duplicated player damage on an enemy')
        for index in range(3):
            positions = [(float(r[14]), float(r[15])) for r in records['host'] if int(r[0]) == index]
            check(max(math.dist(positions[0], p) for p in positions) > 1, f'{index}: real server navigation moves more than one meter')
            check(sum(float(h[3]) for h in player_hits if int(h[0]) == index) == [60, 180, 85][index], f'{index}: exactly authoritative health removed')
            dead_ticks = {r[11] for r in records['host'] if int(r[0]) == index and r[5].lower() == 'true'}
            check(len(dead_ticks) == 1, f'{index}: decision ticks stop on death')
            check(logs['host'].count(f'[PveDeath] index={index} count=1 server=True') == 1, f'{index}: server death exactly once')
            check(any(f'index={index} ' in line and 'view=True' in line for peer in peers[1:-1] for line in logs[peer].splitlines()), f'{index}: remote windup visible')
            seq = sorted({(int(r[8]), int(r[9])) for r in records['host'] if int(r[0]) == index and int(r[8]) > 0})
            cooldown = [1, 1.8, 1.2][index]
            dt = float(records['host'][0][10])
            check(all((b[1]-a[1])*dt + .002 >= (b[0]-a[0])*cooldown for a,b in zip(seq, seq[1:])), f'{index}: authoritative attack cadence')
        check(all(r[5].lower() == 'true' and r[6] == 'Dead' and r[13] == 'False' for r in records['late']), 'late join: corpses, no replayed attacks')
    else:
        check('[PveDisconnect] targeted owner leaving' in logs['client1'], 'client disconnects after authoritative targeting')
        check('Left owner=[Player:2] count=1/6' in logs['host'], 'server releases disconnected owner')
        check('[PveTransition] index=1 state=Return' in logs['host'] and '[PveReturn] index=1 arrived=True' in logs['host'], 'enemy releases target and returns home')
print(f'PASS: {checks} authoritative PvE checks across {", ".join(args.scenarios)}.')
