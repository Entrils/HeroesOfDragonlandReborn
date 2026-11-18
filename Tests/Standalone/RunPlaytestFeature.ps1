param(
    [Parameter(Mandatory)] [ValidateSet('Defense','Magic','Boss','Fortress')] [string]$Feature,
    [string]$Executable = 'Builds/PlaytestAlpha/Dragonland.exe'
)
$ErrorActionPreference = 'Stop'
$profiles = @{
    Defense = @('-hodrDefenseCheck', 'DefenseNetwork')
    Magic = @('-hodrMagicCheck', 'MagicNetwork')
    Boss = @('-hodrBossCheck', 'BossNetwork')
    Fortress = @('-hodrAssaultCheck', 'AssaultNetwork')
}
$flag, $tag = $profiles[$Feature]
$player = (Resolve-Path -LiteralPath $Executable).Path
$id = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$directory = New-Item -ItemType Directory -Path "Builds/Playtest-$Feature-$id"
$room = "hodr-alpha-$id"
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$deadline = [DateTime]::UtcNow.AddSeconds(150)

function Start-FeaturePeer([string]$role) {
    $log = Join-Path $directory.FullName "$role.log"
    $processes.Add((Start-Process -FilePath $player -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-hodrMode', $role, '-hodrRoom', $room,
        '-hodrPlaytestReady', '2', $flag, '-logFile', ('"' + $log + '"'))))
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
    Wait-FeatureLog 'host' "\[$tag\] PASS .*server=True"
    Wait-FeatureLog 'client' "\[$tag\] PASS .*server=False"
    Write-Output "$Feature passed on host and client. Reports: $($directory.FullName)"
} finally {
    foreach ($peer in $processes) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
