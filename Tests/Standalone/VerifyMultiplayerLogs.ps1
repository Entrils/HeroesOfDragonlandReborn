param([string]$LogDirectory = 'Builds/MultiplayerValidation')
$ErrorActionPreference = 'Stop'
$checks = 0
function Check([bool]$condition, [string]$description) {
    if (-not $condition) { throw "FAIL: $description" }
    $script:checks++
}
$hostLog = Get-Content (Join-Path $LogDirectory 'host.log') -Raw
$spawnPattern = '\[Multiplayer\] spawned participant=(\d+) object=\[Id:(\d+)\] inputOwner=\[Player:(\d+)\] team=(Team[AB]) local=(True|False)'
$expected = @([regex]::Matches($hostLog, $spawnPattern) | Where-Object { [int]$_.Groups[1].Value -le 6 })
Check ($expected.Count -eq 6) 'host spawned six initial players'
foreach ($group in 1..3) {
    Check (@($expected | ForEach-Object { $_.Groups[$group].Value } | Select-Object -Unique).Count -eq 6) "unique identity field $group"
}
foreach ($name in @('host','client1','client2','client3','client4','client5')) {
    $log = Get-Content (Join-Path $LogDirectory "$name.log") -Raw
    Check ($log.Contains('players=6 A=3 B=3')) "$name sees 3v3"
    $spawns = @([regex]::Matches($log, $spawnPattern) | Where-Object { [int]$_.Groups[1].Value -le 6 })
    Check ($spawns.Count -eq 6) "$name sees six initial objects"
    Check (@($spawns | Where-Object { $_.Groups[5].Value -eq 'True' }).Count -eq 1) "$name owns exactly one initial player"
    foreach ($entry in $expected) {
        $identity = 'participant=' + $entry.Groups[1].Value + ' object=[Id:' + $entry.Groups[2].Value + '] inputOwner=[Player:' + $entry.Groups[3].Value + '] team=' + $entry.Groups[4].Value
        Check ($log.Contains($identity)) "$name agrees on $identity"
    }
    Check ($log.Contains('owner=[Player:2] id=[Id:1028] participant=2 team=TeamB position=18.000,1.000,-4.000')) "$name receives moved client position"
    Check ($log.Contains('owner=[Player:1] id=[Id:1025] participant=1 team=TeamA position=-8.000,1.000,-4.000')) "$name keeps host stationary"
}
$excess = Get-Content (Join-Path $LogDirectory 'excess.log') -Raw
Check ($excess.Contains('Start failed: GameIsFull')) 'seventh connection rejected'
Check ($hostLog.Contains('Left owner=[Player:2] count=5/6')) 'graceful disconnect releases slot'
$replacement = Get-Content (Join-Path $LogDirectory 'replacement.log') -Raw
Check ($replacement.Contains('participant=7 object=[Id:1037] inputOwner=[Player:7] team=TeamB local=True')) 'replacement gets fresh identity and vacant team'
Check ($replacement.Contains('players=6 A=3 B=3')) 'replacement restores 3v3'
Check (-not $replacement.Contains('owner=[Player:2]')) 'late join does not receive departed object'
$server = Get-Content (Join-Path $LogDirectory 'server.log') -Raw
Check ($server.Contains('server=True players=0 A=0 B=0')) 'dedicated server starts without avatar'
Check ($server.Contains('server=True players=2 A=1 B=1')) 'dedicated server admits both clients'
Check ($server.Contains('Left owner=[Player:2] count=1/6')) 'terminated client releases server slot'
Check ($server.Contains('server=True players=1 A=0 B=1')) 'terminated client removed from server roster'
$survivor = Get-Content (Join-Path $LogDirectory 'server-client2.log') -Raw
Check ($survivor.Contains('server=False players=1 A=0 B=1')) 'surviving client observes cleanup'
Write-Output "PASS: $checks recorded network checks. Fixture-specific IDs; use with the documented 2026-09-09 run."
