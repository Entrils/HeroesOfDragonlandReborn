param([string]$Executable = 'Builds/GameplayHud/DragonlandHud.exe')
$ErrorActionPreference = 'Stop'
$matchExe = (Resolve-Path -LiteralPath $Executable).Path
$matchRoot = Join-Path (Get-Location) ('Builds/LifecycleAcceptance-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $matchRoot | Out-Null
$matchRoom = 'hodr-cycle-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$matchPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function StartPeer([string]$mode) {
    $log = Join-Path $matchRoot ($mode + '.log')
    $matchPeers.Add((Start-Process -FilePath $matchExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$matchRoom,
        '-hodrLifecycle','-hodrHud','-logFile',('"' + $log + '"'))))
}
function WaitLog([string]$mode, [string]$pattern) {
    $deadline = [DateTime]::UtcNow.AddSeconds(100)
    while ([DateTime]::UtcNow -lt $deadline) {
        foreach ($log in Get-ChildItem -LiteralPath $matchRoot -Filter '*.log') {
            if (Select-String -LiteralPath $log.FullName -Pattern '\[LifecycleCheck\] FAIL|Exception:|Start failed:|Spawn failed:' -Quiet) { throw "Lifecycle acceptance failed: $($log.FullName)" }
        }
        $path = Join-Path $matchRoot ($mode + '.log')
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $mode / $pattern. Logs: $matchRoot"
}
try {
    StartPeer 'host'
    WaitLog 'host' '\[MatchLifecycle\] round=0 state=Lobby'
    Start-Sleep -Seconds 4
    if (Select-String -LiteralPath (Join-Path $matchRoot 'host.log') -Pattern 'state=Starting|state=Playing' -Quiet) { throw 'One player started a match.' }
    StartPeer 'client'
    WaitLog 'host' '\[LifecycleComplete\] server=True'
    WaitLog 'client' '\[LifecycleComplete\] server=False'
    WaitLog 'host' '\[HudComplete\] server=True'
    WaitLog 'client' '\[HudComplete\] server=False'
    Write-Output "PASS: HUD ownership, resources, XP, target events, results and rebind across two match rounds. Logs: $matchRoot"
} finally {
    foreach ($peer in $matchPeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}


