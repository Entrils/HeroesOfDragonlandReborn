param([string]$Executable = 'Builds/GameplayHud/DragonlandHud.exe')
$ErrorActionPreference = 'Stop'
$respawnExe = (Resolve-Path -LiteralPath $Executable).Path
$respawnRoot = Join-Path (Get-Location) ('Builds/RespawnAcceptance-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $respawnRoot | Out-Null
$respawnRoom = 'hodr-respawn-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$respawnPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function StartPeer([string]$mode) {
    $log = Join-Path $respawnRoot ($mode + '.log')
    $peer = Start-Process -FilePath $respawnExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$respawnRoom,
        '-hodrRespawn','-hodrHud','-hodrQuit','150','-logFile',('"' + $log + '"'))
    $respawnPeers.Add($peer)
}
function WaitLog([string]$mode, [string]$pattern) {
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    while ([DateTime]::UtcNow -lt $deadline) {
        foreach ($log in Get-ChildItem -LiteralPath $respawnRoot -Filter '*.log') {
            if (Select-String -LiteralPath $log.FullName -Pattern '\[RespawnCheck\] FAIL|Exception:|Start failed:' -Quiet) { throw "Respawn acceptance failed: $($log.FullName)" }
        }
        $path = Join-Path $respawnRoot ($mode + '.log')
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $mode / $pattern. Logs: $respawnRoot"
}
try {
    StartPeer 'host'
    WaitLog 'host' 'Joined P1'
    StartPeer 'client'
    WaitLog 'host' '\[RespawnComplete\] server=True'
    WaitLog 'client' '\[RespawnComplete\] server=False'
    WaitLog 'host' '\[HudComplete\] server=True'
    WaitLog 'client' '\[HudComplete\] server=False'
    Write-Output "PASS: local HUD respawn countdown, resource events, results and two server/client respawn cycles. Logs: $respawnRoot"
} finally {
    foreach ($peer in $respawnPeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}


