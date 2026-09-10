param(
    [ValidateSet('host_hits','client_hits','friendly','disconnect','abrupt','dodge')]
    [string]$Scenario = 'host_hits',
    [string]$Executable = 'Builds/MultiplayerActions/DragonlandActions.exe',
    [string]$ValidationRoot = 'Builds/MultiplayerActionValidation'
)
$ErrorActionPreference = 'Stop'
$combatExe = (Resolve-Path -LiteralPath $Executable).Path
$combatDirectory = Join-Path (Join-Path (Get-Location) $ValidationRoot) $Scenario
New-Item -ItemType Directory -Path $combatDirectory -Force | Out-Null
$combatRoom = 'hodr-m3-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$combatProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function LaunchPeer([string]$mode, [string]$name) {
    $log = Join-Path $combatDirectory "$name.log"
    # Prevent a previous successful run from satisfying startup conditions.
    Set-Content -LiteralPath $log -Value '' -Encoding UTF8
    $peer = Start-Process -FilePath $combatExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$combatRoom,
        '-hodrProbe','-hodrCombat',$Scenario,'-hodrQuit','110','-logFile',$log)
    $combatProcesses.Add($peer)
    return $peer
}
function WaitLog([string]$name, [string]$pattern, [int]$seconds = 40) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    $path = Join-Path $combatDirectory "$name.log"
    while ([DateTime]::UtcNow -lt $deadline) {
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $name / $pattern"
}
try {
    $null = LaunchPeer 'host' 'host'
    WaitLog 'host' 'Joined P1'
    $client = LaunchPeer 'client' 'client1'
    WaitLog 'host' 'Joined P2'
    if ($Scenario -eq 'friendly') {
        $null = LaunchPeer 'client' 'client2'
        WaitLog 'host' 'Joined P3'
        WaitLog 'host' 'attacks=10' 45
        Start-Sleep -Seconds 2
    } elseif ($Scenario -eq 'dodge') {
        WaitLog 'host' '\[ActionProbe\].*owner=\[Player:2\].*dodge=6 ' 55
        WaitLog 'client1' '\[ActionProbe\].*owner=\[Player:2\].*dodge=6 ' 20
        Start-Sleep -Seconds 2
    } elseif ($Scenario -eq 'disconnect') {
        WaitLog 'client1' 'disconnect-during-combat'
        WaitLog 'host' 'Left owner=\[Player:2\] count=1/6'
        Start-Sleep -Seconds 2
    } elseif ($Scenario -eq 'abrupt') {
        WaitLog 'host' 'owner=\[Player:2\].*hp=75.00'
        $client.Refresh()
        if (-not $client.HasExited) { $client.Kill() }
        WaitLog 'host' 'Left owner=\[Player:2\] count=1/6'
        Start-Sleep -Seconds 2
    } else {
        WaitLog 'host' 'hp=0.00 max=100.00 dead=True' 55
        WaitLog 'client1' 'hp=0.00 max=100.00 dead=True'
        Start-Sleep -Seconds 2
    }
    Write-Output "Recorded $Scenario in $combatDirectory"
} finally {
    foreach ($peer in $combatProcesses) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
