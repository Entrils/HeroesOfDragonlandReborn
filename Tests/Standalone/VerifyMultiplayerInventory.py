"""Verify actual owner/private and public equipment records, including a late join."""
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else 'Builds/MultiplayerInventoryValidation')
checks = 0

def check(ok, label):
    global checks
    if not ok:
        raise AssertionError(label)
    checks += 1

pattern = re.compile(r'\[InventoryState\] participant=(\d+) owner=(True|False) private=(True|False) hidden=(True|False) potion=(\d+) sword=(\d+) dragon=(\d+) main=(True|False) chest=(True|False) visuals=(\d+) complete=(True|False)')
for peer in ['host', 'client1', 'late']:
    log = (root / (peer + '.log')).read_text(encoding='utf-8', errors='replace')
    check('[InventoryCheck] FAIL' not in log, peer + ': no assertion failures')
    check(not re.search(r'\b(?:\w+Exception):|Assertion failed|StartGame Failed', log), peer + ': no runtime/startup failures')
    rows = [m.groups() for m in pattern.finditer(log)]
    check(bool(rows), peer + ': snapshots received')
    for who in [1, 2]:
        actor = [r for r in rows if int(r[0]) == who]
        check(bool(actor), f'{peer}/{who}: avatar present')
        check(any(r[7] == r[8] == 'True' for r in actor), f'{peer}/{who}: public equipped sword and armor')
        if peer != 'host' and (peer == 'late' or who == 1):
            check(all(r[2] == 'False' and r[3] == 'True' and r[4:7] == ('0', '0', '0') for r in actor),
                  f'{peer}/{who}: private inventory and wire state absent')
            check(f'PASS participant={who} remote object requests blocked' in log, f'{peer}/{who}: ownership gate')
        else:
            check(any(r[4:7] == ('14', '0', '1') and r[7] == r[8] == 'True' for r in actor),
                  f'{peer}/{who}: final quantity conservation')
        if peer != 'host':
            check(f'PASS participant={who} client mutation paths blocked' in log, f'{peer}/{who}: client cannot grant/remove/equip directly')
    if peer in ['host', 'client1']:
        who = 1 if peer == 'host' else 2
        for step in range(14):
            check(f'PASS participant={who} request={step} ' in log, f'{peer}: validated request {step}')
        check(f'PASS participant={who} conservation modifiers visuals and spam' in log, peer + ': server modifiers/visual hooks/spam')
        check(f'[InventoryProbe] COMPLETE participant={who}' in log, peer + ': completed owner flow')
    if peer == 'host':
        for who in [1, 2]:
            for label in ['unowned sender rejected', 'invalid server add/remove atomic', 'capacity enforced', 'server seed and remove']:
                check(f'PASS participant={who} {label}' in log, f'server/{who}: {label}')
    if peer == 'late':
        check(any(r[0] == '3' and r[1] == r[2] == 'True' and r[4:7] == ('0', '0', '0') for r in rows),
              'late join: own new empty inventory')
print(f'PASS: {checks} authoritative inventory/equipment checks across Host, owner Client and late observer.')
