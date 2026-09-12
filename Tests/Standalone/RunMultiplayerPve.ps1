param(
    [ValidateSet('combat','disconnect')][string]$Scenario = 'combat',
    [string]$Executable = 'Builds/MultiplayerPve/DragonlandPve.exe',
    [string]$ValidationRoot = 'Builds/MultiplayerPveValidation'
)
$ErrorActionPreference = 'Stop'
$pveExe = (Resolve-Path -LiteralPath $Executable).Path
$pveDirectory = Join-Path (Join-Path (Get-Location) $ValidationRoot) $Scenario
New-Item -ItemType Directory -Path $pveDirectory -Force | Out-Null
$pveRoom = 'hodr-m5-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$pvePeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function LaunchPeer([string]$mode, [string]$name) {
    $log = Join-Path $pveDirectory "$name.log"
    Set-Content -LiteralPath $log -Value '' -Encoding UTF8
    $peer = Start-Process -FilePath $pveExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$pveRoom,
        '-hodrProbe','-hodrPve',$Scenario,'-hodrQuit','600','-logFile',$log)
    $pvePeers.Add($peer)
    return $peer
}
function WaitLog([string]$name, [string]$pattern, [int]$seconds = 60) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    $path = Join-Path $pveDirectory "$name.log"
    while ([DateTime]::UtcNow -lt $deadline) {
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern '\[PveCheck\] FAIL' -Quiet)) { throw "PvE assertion failed: $name" }
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $name / $pattern"
}
function JoinClient([string]$name, [int]$participant) {
    # Cloud startup may fail before a player object exists. Retry only that case;
    # preserve failed logs and never mask a gameplay assertion or post-join failure.
    $hostPath = Join-Path $pveDirectory 'host.log'
    $clientPath = Join-Path $pveDirectory "$name.log"
    foreach ($attempt in 1..3) {
        $peer = LaunchPeer 'client' $name
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        while ([DateTime]::UtcNow -lt $deadline) {
            if (Select-String -LiteralPath $hostPath -Pattern '\[PveCheck\] FAIL|\[Multiplayer\] Stopped:' -Quiet) { throw 'Host failed while joining client' }
            if (Select-String -LiteralPath $hostPath -Pattern "Joined P$participant " -Quiet) { return $peer }
            if (Select-String -LiteralPath $clientPath -Pattern 'Start failed:|StartGame Failed:' -Quiet) { break }
            Start-Sleep -Milliseconds 500
        }
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        Copy-Item -LiteralPath $clientPath -Destination (Join-Path $pveDirectory "$name-startup-$attempt.log")
        if ($attempt -eq 3) { throw "Client startup failed after three attempts: $name" }
        Write-Output "Retrying cloud startup for $name (previous log preserved)"
        Start-Sleep -Seconds 2
    }
}
try {
    $null = LaunchPeer 'host' 'host'; WaitLog 'host' 'Joined P1'
    if ($Scenario -eq 'combat') {
        $lastClient = $null
        foreach ($index in 1..5) {
            $participant = $index + 1
            $lastClient = JoinClient "client$index" $participant
        }
        WaitLog 'host' '\[PveComplete\] all-dead count=3' 100
        foreach ($index in 1..5) { WaitLog "client$index" '\[PveComplete\] all-dead count=3' 20 }
        $lastClient.Refresh()
        if (-not $lastClient.HasExited) { $lastClient.Kill(); $lastClient.WaitForExit(5000) | Out-Null }
        WaitLog 'host' 'Left owner=\[Player:6\] count=5/6'
        $null = JoinClient 'late' 7
        WaitLog 'late' '\[PveComplete\] all-dead count=3'
    } else {
        $null = JoinClient 'client1' 2
        WaitLog 'client1' '\[PveDisconnect\] targeted owner leaving'
        WaitLog 'host' 'Left owner=\[Player:2\] count=1/6'
        WaitLog 'host' '\[PveReturn\] index=1 arrived=True'
    }
    Start-Sleep -Seconds 2
    Write-Output "Recorded PvE $Scenario in $pveDirectory"
} finally {
    foreach ($peer in $pvePeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
