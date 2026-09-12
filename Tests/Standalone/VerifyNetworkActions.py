"""Check real peer action timelines, duplicate-hit protection and server dodge windows."""
import pathlib
import re
import argparse

available_scenarios = ['host_hits', 'client_hits', 'friendly', 'disconnect', 'abrupt', 'dodge']
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('root', nargs='?', default='Builds/MultiplayerActionValidation')
parser.add_argument('--scenarios', nargs='+', choices=available_scenarios, default=available_scenarios)
args = parser.parse_args()
root = pathlib.Path(args.root)
pattern = re.compile(r'\[ActionProbe\] owner=\[Player:(\d+)\] attack=(\d+) attackTick=(\d+) attacking=(true|false) dodge=(\d+) dodgeTick=(\d+) dodging=(true|false) x=([-\d.]+) z=([-\d.]+) dt=([\d.]+)', re.I)
checks = 0

def check(ok, message):
    global checks
    if not ok:
        raise AssertionError(message)
    checks += 1

for scenario in args.scenarios:
    logs = {p.stem: p.read_text(encoding='utf-8', errors='replace') for p in (root / scenario).glob('*.log')}
    check('host' in logs and 'client1' in logs, scenario + ': both peers')
    records = {}
    for peer, log in logs.items():
        rows = []
        for m in pattern.finditer(log):
            v = m.groups()
            rows.append(dict(owner=int(v[0]), attack=int(v[1]), attack_tick=int(v[2]), attacking=v[3].lower() == 'true',
                dodge=int(v[4]), dodge_tick=int(v[5]), dodging=v[6].lower() == 'true', x=float(v[7]), z=float(v[8]), dt=float(v[9])))
        check(bool(rows), f'{scenario}/{peer}: timelines received')
        check(not re.search(r'^(?:\w+\.)*\w*Exception:|^Assertion failed', log, re.M), f'{scenario}/{peer}: no runtime exception')
        check(all(not (r['attacking'] and r['dodging']) for r in rows), f'{scenario}/{peer}: mutually exclusive actions')
        check(all(abs(r['x']) <= 18.001 and abs(r['z']) <= 18.001 for r in rows), f'{scenario}/{peer}: authoritative bounds')
        records[peer] = rows
    for action, minimum in [('attack', 0.9), ('dodge', 0.7)]:
        timelines = {}
        for r in records['host']:
            if r[action]:
                timelines[(r['owner'], r[action])] = r[action + '_tick']
        for owner in {r['owner'] for r in records['host']}:
            events = sorted((seq, tick) for (who, seq), tick in timelines.items() if who == owner)
            for (seq1, tick1), (seq2, tick2) in zip(events, events[1:]):
                check((tick2 - tick1) * records['host'][0]['dt'] + 0.002 >= (seq2 - seq1) * minimum,
                    f'{scenario}: {action} cadence/recovery for owner {owner}')
        for peer, rows in records.items():
            if peer == 'host':
                continue
            matched = [r for r in rows if (r['owner'], r[action]) in timelines]
            if timelines:
                check(bool(matched), f'{scenario}/{peer}: {action} replicated')
                check(all(timelines[(r['owner'], r[action])] == r[action + '_tick'] for r in matched),
                    f'{scenario}/{peer}: identical accepted {action} start ticks')
    hits = re.findall(r'\[ActionHit\] source=\[Id:(\d+)\] victim=\[Id:(\d+)\] sequence=(\d+) amount=([\d.]+)', logs['host'])
    keys = [h[:3] for h in hits]
    check(len(keys) == len(set(keys)), scenario + ': at most one damage per swing and victim')
    check(all(float(h[3]) == 25 for h in hits), scenario + ': server-configured damage only')
    if scenario in ('host_hits', 'client_hits'):
        check(len(hits) == 4, scenario + ': exactly four hits to kill')
        attacker = 1 if scenario == 'host_hits' else 2
        remote = 'client1' if attacker == 1 else 'host'
        check(f'[ActionView] owner=[Player:{attacker}] swing=True' in logs[remote],
            scenario + ': remote presentation shows accepted swing')
    if scenario == 'friendly':
        check(not hits, 'friendly: no legal allied hits')
    if scenario == 'dodge':
        check('pass=False' not in logs['host'], 'dodge: no failed invulnerability checks')
        for phase in ['before', 'inside', 'after']:
            check(f'[DodgeWindow] phase={phase} pass=True' in logs['host'], 'dodge: damage policy ' + phase)
        for peer, rows in records.items():
            check(max(r['dodge'] for r in rows if r['owner'] == 2) >= 6, f'dodge/{peer}: repeated owning-client dodge accepted')
        check('[ActionView] owner=[Player:2] swing=False roll=True' in logs['host'],
            'dodge: remote host presentation shows client roll')
        times = sorted({r['dodge_tick'] for r in records['host'] if r['owner'] == 2 and r['dodge']})
        check(any((b - a) * records['host'][0]['dt'] > 1.0 for a, b in zip(times, times[1:])),
            'dodge: held spam waits for stamina regeneration')
print(f'PASS: {checks} network action checks across {len(args.scenarios)} scenarios: {", ".join(args.scenarios)}.')
