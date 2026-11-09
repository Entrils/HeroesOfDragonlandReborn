param([string]$Executable = 'Builds/Battlefield/DragonlandBattlefield.exe')
$ErrorActionPreference = 'Stop'
$battleExecutable = (Resolve-Path -LiteralPath $Executable).Path
$battleRoot = Join-Path (Get-Location) ('Builds/BattlefieldAcceptance-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $battleRoot | Out-Null
$battleRoom = 'hodr-battle-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$battlePeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function StartBattlePeer([string]$label, [string]$mode) {
    $battleLog = Join-Path $battleRoot ($label + '.log')
    $battlePeers.Add((Start-Process -FilePath $battleExecutable -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$battleRoom,'-hodrBattlefield','-logFile',('"' + $battleLog + '"'))))
}
function WaitBattleLog([string]$label, [string]$pattern) {
    $battleDeadline = [DateTime]::UtcNow.AddSeconds(130)
    while ([DateTime]::UtcNow -lt $battleDeadline) {
        foreach ($battleLog in Get-ChildItem -LiteralPath $battleRoot -Filter '*.log') {
            if (Select-String -LiteralPath $battleLog.FullName -Pattern '\[BattlefieldCheck\] FAIL|Exception:|Start failed:|Spawn failed:|Camp initialization failed|not on.*NavMesh' -Quiet) {
                throw "Battlefield acceptance failed: $($battleLog.FullName)"
            }
        }
        $battlePath = Join-Path $battleRoot ($label + '.log')
        if ((Test-Path -LiteralPath $battlePath) -and (Select-String -LiteralPath $battlePath -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $label / $pattern. Logs: $battleRoot"
}
try {
    StartBattlePeer 'host' 'host'
    WaitBattleLog 'host' '\[MatchLifecycle\] round=0 state=Lobby'
    for ($battleIndex = 1; $battleIndex -le 5; $battleIndex++) { StartBattlePeer ('client' + $battleIndex) 'client' }
    WaitBattleLog 'host' '\[BattlefieldComplete\]'
    for ($battleIndex = 1; $battleIndex -le 5; $battleIndex++) { WaitBattleLog ('client' + $battleIndex) '\[BattlefieldComplete\]' }
    Write-Output "PASS: actual 3v3, eight active camps and delayed center, reward tiers, clear owned spawns and all six players walking out of their fortresses. Logs: $battleRoot"
} finally {
    foreach ($battlePeer in $battlePeers) {
        $battlePeer.Refresh()
        if (-not $battlePeer.HasExited) { $battlePeer.Kill(); $battlePeer.WaitForExit(5000) | Out-Null }
        $battlePeer.Dispose()
    }
}
