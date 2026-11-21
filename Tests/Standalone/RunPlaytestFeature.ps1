param(
    [Parameter(Mandatory)] [ValidateSet('Defense','Magic','Boss','Fortress','TeamPing')] [string]$Feature,
    [string]$Executable = 'Builds/PlaytestAlpha/Dragonland.exe'
)
$ErrorActionPreference = 'Stop'
$profiles = @{
    Defense = @('-hodrDefenseCheck', 'DefenseNetwork')
    Magic = @('-hodrMagicCheck', 'MagicNetwork')
    Boss = @('-hodrBossCheck', 'BossNetwork')
    Fortress = @('-hodrAssaultCheck', 'AssaultNetwork')
    TeamPing = @('-hodrPingCheck', 'TeamPing')
}
$flag, $tag = $profiles[$Feature]
$player = (Resolve-Path -LiteralPath $Executable).Path
$id = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$directory = New-Item -ItemType Directory -Path "Builds/Playtest-$Feature-$id"
$room = "hodr-alpha-$id"
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$deadline = [DateTime]::UtcNow.AddSeconds(150)
$peerCount = if ($Feature -eq 'TeamPing') { 3 } else { 2 }

function Start-FeaturePeer([string]$role, [string]$label = $role) {
    $log = Join-Path $directory.FullName "$label.log"
    $processes.Add((Start-Process -FilePath $player -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-hodrMode', $role, '-hodrRoom', $room,
        '-hodrPlaytestReady', "$peerCount", $flag, '-logFile', ('"' + $log + '"'))))
}

function Wait-FeatureLog([string]$role, [string]$pattern) {
    while ([DateTime]::UtcNow -lt $deadline) {
        foreach ($log in Get-ChildItem -LiteralPath $directory.FullName -Filter '*.log') {
            if (Select-String -LiteralPath $log.FullName -Pattern "\[$tag\] FAIL|Exception:|Start failed:|Spawn failed:" -Quiet) {
                throw "$Feature failed. See $($log.FullName)"
            }
        }
        $path = Join-Path $directory.FullName "$role.log"
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "$Feature timed out waiting for $role. Reports: $($directory.FullName)"
}

try {
    Start-FeaturePeer 'host'
    Wait-FeatureLog 'host' '\[MatchLifecycle\] round=0 state=Lobby'
    Start-FeaturePeer 'client'
    if ($Feature -eq 'TeamPing') {
        Wait-FeatureLog 'client' '\[NetworkState\] Connected'
        Start-FeaturePeer 'client' 'client2'
    }
    Wait-FeatureLog 'host' "\[$tag\] PASS .*server=True"
    Wait-FeatureLog 'client' "\[$tag\] PASS .*server=False"
    if ($Feature -eq 'TeamPing') {
        Wait-FeatureLog 'client2' "\[$tag\] PASS .*server=False"
    }
    Write-Output "$Feature passed on all $peerCount peers. Reports: $($directory.FullName)"
} finally {
    foreach ($peer in $processes) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
