"""Validate records produced by RunMultiplayerCombat.ps1; never infers success from exit codes alone."""
import pathlib
import re
import argparse

available_scenarios = ['host_hits', 'client_hits', 'friendly', 'disconnect', 'abrupt']
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('root', nargs='?', default='Builds/MultiplayerCombatValidation')
parser.add_argument('--scenarios', nargs='+', choices=available_scenarios, default=available_scenarios)
args = parser.parse_args()
root = pathlib.Path(args.root)
pattern = re.compile(r'\[CombatProbe\] local=\[Player:(\d+)\] owner=\[Player:(\d+)\] id=\[Id:(\d+)\] hp=([\d.]+) max=([\d.]+) dead=(true|false) stamina=([\d.]+) mana=([\d.]+) privateMax=([\d.]+) attacks=(\d+)', re.I)
checks = 0
def check(ok, message):
    global checks
    if not ok:
        raise AssertionError(message)
    checks += 1

for scenario in args.scenarios:
    logs = {p.stem: p.read_text(encoding='utf-8', errors='replace') for p in (root / scenario).glob('*.log')}
    check('host' in logs and 'client1' in logs, scenario + ': required logs')
    parsed = {}
    for name, log in logs.items():
        rows = []
        for match in pattern.finditer(log):
            a = match.groups()
            rows.append(dict(local=int(a[0]), owner=int(a[1]), id=int(a[2]), hp=float(a[3]), maximum=float(a[4]), dead=a[5].lower() == 'true', stamina=float(a[6]), mana=float(a[7]), private_max=float(a[8]), attacks=int(a[9])))
        check(bool(rows), f'{scenario}/{name}: received stats')
        check(not re.search(r'^(?:\w+\.)*\w*Exception:|^Assertion failed', log, re.M), f'{scenario}/{name}: no exception header')
        check('client-mutations-blocked=False' not in log, f'{scenario}/{name}: no accepted client mutation')
        check(all(0 <= r['hp'] <= r['maximum'] and r['dead'] == (r['hp'] == 0) for r in rows), f'{scenario}/{name}: coherent health/death')
        check(all(0 <= r['stamina'] <= r['private_max'] and 0 <= r['mana'] <= 100 for r in rows), f'{scenario}/{name}: resources bounded')
        if name != 'host':
            remotes = [r for r in rows if r['owner'] != r['local']]
            check(bool(remotes) and all(r['private_max'] == r['stamina'] == r['mana'] == 0 for r in remotes), f'{scenario}/{name}: private resources hidden')
            check(any(r['owner'] == r['local'] and r['private_max'] == 100 for r in rows), f'{scenario}/{name}: owner resources delivered')
        parsed[name] = rows
    # Stable network identities must match across processes.
    ids = {r['owner']: r['id'] for r in parsed['host']}
    for name, rows in parsed.items():
        check(all(ids.get(r['owner']) == r['id'] for r in rows), f'{scenario}/{name}: network identity agrees')
    if scenario in ('host_hits', 'client_hits'):
        victim = 2 if scenario == 'host_hits' else 1
        attacker = 3 - victim
        for name, rows in parsed.items():
            victim_rows = [r for r in rows if r['owner'] == victim]
            check(victim_rows[-1]['dead'] and victim_rows[-1]['hp'] == 0, f'{scenario}/{name}: replicated death')
            check(all(b['hp'] <= a['hp'] for a, b in zip(victim_rows, victim_rows[1:])), f'{scenario}/{name}: no client healing/resurrection')
            check(all(r['hp'] == 100 for r in rows if r['owner'] == attacker), f'{scenario}/{name}: attacker not self-damaged')
        spending = [r for r in parsed['host'] if r['owner'] == attacker]
        check(any(r['stamina'] < 25 and r['attacks'] >= 4 for r in spending), scenario + ': server enforces resource exhaustion')
        check(max(r['attacks'] for r in spending) >= 4, scenario + ': accepted attack state replicates')
        if scenario == 'host_hits':
            check('client-mutations-blocked=True dead=false' in logs['client1'], 'damaged client cannot mutate Stats')
            check('client-mutations-blocked=True dead=true' in logs['client1'], 'dead client cannot resurrect')
    elif scenario == 'friendly':
        check('client2' in logs, 'friendly: allied third player present')
        for name, rows in parsed.items():
            check(all(r['hp'] == 100 and not r['dead'] for r in rows), f'friendly/{name}: no friendly damage')
        check(max(r['attacks'] for r in parsed['host']) >= 10, 'friendly: repeated real attacks attempted')
        check(any(r['stamina'] < 25 for r in parsed['host']), 'friendly: attack costs still apply')
    else:
        if scenario == 'disconnect':
            # The probe emits this event only after the victim receives damaged
            # authoritative Health. It disconnects immediately, potentially before
            # either peer's next 500 ms stats sample. Do not require that sample.
            check('[CombatProbe] disconnect-during-combat' in logs['client1'],
                  scenario + ': victim received damage before requesting disconnect')
            check(any(r['owner'] == 1 and r['attacks'] > 0 for r in parsed['host']),
                  scenario + ': server accepted combat intent')
        else:
            check(any(r['owner'] == 2 and r['hp'] < 100 for r in parsed['host']), scenario + ': combat occurred before disconnect')
        marker = 'Left owner=[Player:2] count=1/6'
        check(marker in logs['host'], scenario + ': disconnect released slot')
        tail = logs['host'].split(marker, 1)[1]
        check(not re.search(r'\[CombatProbe\].*owner=\[Player:2\]', tail), scenario + ': departed object absent')
        check('[CombatProbe]' in tail, scenario + ': server continues simulation')
print(f'PASS: {checks} multiplayer combat log checks across {len(args.scenarios)} scenarios: {", ".join(args.scenarios)}.')
