param([string]$Executable = 'Builds/TeamSpawns/DragonlandTeamSpawns.exe')
$ErrorActionPreference = 'Stop'
$spawnExe = (Resolve-Path -LiteralPath $Executable).Path
$spawnRoot = Join-Path (Get-Location) ('Builds/TeamSpawnAcceptance-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $spawnRoot | Out-Null
$spawnRoom = 'hodr-spawns-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$spawnPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function StartPeer([string]$mode, [string]$name) {
    $log = Join-Path $spawnRoot ($name + '.log')
    $spawnPeers.Add((Start-Process -FilePath $spawnExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$spawnRoom,
        '-hodrTeamSpawns','-logFile',('"' + $log + '"'))))
}
function WaitLog([string]$name, [string]$pattern) {
    $deadline = [DateTime]::UtcNow.AddSeconds(100)
    while ([DateTime]::UtcNow -lt $deadline) {
        foreach ($log in Get-ChildItem -LiteralPath $spawnRoot -Filter '*.log') {
            if (Select-String -LiteralPath $log.FullName -Pattern '\[TeamSpawnCheck\] FAIL|Exception:|Start failed:|Spawn failed:' -Quiet) { throw "Team spawn acceptance failed: $($log.FullName)" }
        }
        $path = Join-Path $spawnRoot ($name + '.log')
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $name / $pattern. Logs: $spawnRoot"
}
try {
    StartPeer 'host' 'host'
    WaitLog 'host' 'Joined P1'
    foreach ($index in 1..5) { StartPeer 'client' ('client' + $index) }
    WaitLog 'host' '\[TeamSpawnComplete\] server=True'
    foreach ($index in 1..5) { WaitLog ('client' + $index) '\[TeamSpawnComplete\] server=False' }
    Write-Output "PASS: six peers, team-only spawns, occupancy, queued join, reservations, server authority, protected zone movement. Logs: $spawnRoot"
} finally {
    foreach ($peer in $spawnPeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
