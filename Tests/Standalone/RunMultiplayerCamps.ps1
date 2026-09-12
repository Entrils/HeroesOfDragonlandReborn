param(
    [ValidateSet('combat','removed')][string]$Scenario = 'combat',
    [string]$Executable = 'Builds/MultiplayerCamps/DragonlandCamps.exe',
    [string]$ValidationRoot = 'Builds/MultiplayerCampValidation'
)
$ErrorActionPreference = 'Stop'
$campExe = (Resolve-Path -LiteralPath $Executable).Path
$campDirectory = Join-Path (Join-Path (Get-Location) $ValidationRoot) $Scenario
New-Item -ItemType Directory -Path $campDirectory -Force | Out-Null
$campRoom = 'hodr-m6-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$campPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function LaunchPeer([string]$mode, [string]$name) {
    $log = Join-Path $campDirectory "$name.log"
    Set-Content -LiteralPath $log -Value '' -Encoding UTF8
    $peer = Start-Process -FilePath $campExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$campRoom,
        '-hodrProbe','-hodrCamps',$Scenario,'-hodrQuit','600','-logFile',$log)
    $campPeers.Add($peer)
    return $peer
}
function WaitLog([string]$name, [string]$pattern, [int]$seconds = 60) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    $path = Join-Path $campDirectory "$name.log"
    while ([DateTime]::UtcNow -lt $deadline) {
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern '\[CampCheck\] FAIL' -Quiet)) { throw "Camp assertion failed: $name" }
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $name / $pattern"
}
function JoinClient([string]$name, [int]$participant) {
    $hostPath = Join-Path $campDirectory 'host.log'
    $clientPath = Join-Path $campDirectory "$name.log"
    foreach ($attempt in 1..3) {
        $peer = LaunchPeer 'client' $name
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        while ([DateTime]::UtcNow -lt $deadline) {
            if (Select-String -LiteralPath $hostPath -Pattern '\[CampCheck\] FAIL|\[Multiplayer\] Stopped:' -Quiet) { throw 'Host failed while joining client' }
            if (Select-String -LiteralPath $hostPath -Pattern "Joined P$participant " -Quiet) { return $peer }
            if (Select-String -LiteralPath $clientPath -Pattern 'Start failed:|StartGame Failed:' -Quiet) { break }
            Start-Sleep -Milliseconds 500
        }
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        Copy-Item -LiteralPath $clientPath -Destination (Join-Path $campDirectory "$name-startup-$attempt.log")
        if ($attempt -eq 3) { throw "Client startup failed after three attempts: $name" }
        Write-Output "Retrying cloud startup for $name (previous log preserved)"
    }
}
try {
    $null = LaunchPeer 'host' 'host'; WaitLog 'host' 'Joined P1'
    if ($Scenario -eq 'combat') {
        $lastClient = $null
        foreach ($index in 1..5) { $lastClient = JoinClient "client$index" ($index + 1) }
        WaitLog 'host' '\[CampComplete\] cleared' 120
        foreach ($index in 1..5) { WaitLog "client$index" '\[CampComplete\] cleared' 20 }
        $lastClient.Refresh()
        if (-not $lastClient.HasExited) { $lastClient.Kill(); $lastClient.WaitForExit(5000) | Out-Null }
        WaitLog 'host' 'Left owner=\[Player:6\] count=5/6'
        $null = JoinClient 'late' 7
        WaitLog 'late' '\[CampComplete\] cleared'
    } else {
        $null = JoinClient 'client1' 2
        WaitLog 'host' '\[CampComplete\] removed'
    }
    Start-Sleep -Seconds 2
    Write-Output "Recorded camp $Scenario in $campDirectory"
} finally {
    foreach ($peer in $campPeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
