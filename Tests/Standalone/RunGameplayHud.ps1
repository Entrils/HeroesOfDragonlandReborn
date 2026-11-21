param([string]$Executable = 'Builds/GameplayHud/DragonlandHud.exe', [switch]$VerifyProfile)
$ErrorActionPreference = 'Stop'
$matchExe = (Resolve-Path -LiteralPath $Executable).Path
$matchRoot = Join-Path (Get-Location) ('Builds/LifecycleAcceptance-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $matchRoot | Out-Null
$matchRoom = 'hodr-cycle-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$matchPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$profiles = @{ host = [Guid]::NewGuid().ToString('N'); client = [Guid]::NewGuid().ToString('N') }
function StartPeer([string]$mode) {
    $log = Join-Path $matchRoot ($mode + '.log')
    $arguments = @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$matchRoom,
        '-hodrPlaytestReady','2','-hodrLifecycle','-hodrHud','-logFile',('"' + $log + '"'))
    if ($VerifyProfile) { $arguments += @('-hodrProfileTest',$profiles[$mode],'-hodrProfileSeed') }
    $matchPeers.Add((Start-Process -FilePath $matchExe -WindowStyle Hidden -PassThru -ArgumentList $arguments))
}
function WaitLog([string]$mode, [string]$pattern) {
    $deadline = [DateTime]::UtcNow.AddSeconds(100)
    while ([DateTime]::UtcNow -lt $deadline) {
        foreach ($log in Get-ChildItem -LiteralPath $matchRoot -Filter '*.log') {
            if (Select-String -LiteralPath $log.FullName -Pattern '\[(LifecycleCheck|ProfileCheck)\] FAIL|Exception:|Start failed:|Spawn failed:' -Quiet) { throw "Lifecycle acceptance failed: $($log.FullName)" }
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
    if ($VerifyProfile) {
        foreach ($mode in @('host','client')) { WaitLog $mode '\[ProfileCheck\] Recorded matches=2' }
        foreach ($peer in $matchPeers) {
            if (!$peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        }
        foreach ($mode in @('host','client')) {
            $log = Join-Path $matchRoot ("reload-$mode.log")
            $matchPeers.Add((Start-Process -FilePath $matchExe -WindowStyle Hidden -PassThru -ArgumentList @(
                '-batchmode','-nographics','-hodrProfileTest',$profiles[$mode],'-hodrProfileVerify','-logFile',('"' + $log + '"'))))
            WaitLog "reload-$mode" '\[ProfileCheck\] PASS'
        }
        Write-Output "PASS: host and client profiles reloaded after restart and rejected a duplicate result."
    }
    Write-Output "PASS: HUD ownership, resources, XP, target events, results and rebind across two match rounds. Logs: $matchRoot"
} finally {
    foreach ($peer in $matchPeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}


