param(
    [string]$Executable = 'Builds/MultiplayerLoot/DragonlandLoot.exe',
    [string]$ValidationRoot = 'Builds/MultiplayerLootAcceptance'
)
$ErrorActionPreference = 'Stop'
$lootExe = (Resolve-Path -LiteralPath $Executable).Path
$lootDirectory = [IO.Path]::GetFullPath((Join-Path (Get-Location) $ValidationRoot))
New-Item -ItemType Directory -Path $lootDirectory -Force | Out-Null
$lootRoom = 'hodr-m7-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$lootPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function LaunchPeer([string]$mode, [string]$name) {
    $log = Join-Path $lootDirectory "$name.log"
    $peer = Start-Process -FilePath $lootExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$lootRoom,
        '-hodrProbe','-hodrLoot','-hodrQuit','240','-logFile',('"' + $log + '"'))
    $lootPeers.Add($peer)
}
function WaitLog([string]$name, [string]$pattern, [int]$seconds = 90) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    $path = Join-Path $lootDirectory "$name.log"
    while ([DateTime]::UtcNow -lt $deadline) {
        foreach ($file in @('host','client','late')) {
            $candidate = Join-Path $lootDirectory "$file.log"
            if ((Test-Path -LiteralPath $candidate) -and (Select-String -LiteralPath $candidate -Pattern '\[LootCheck\] FAIL|Start failed:|Exception:' -Quiet)) { throw "Loot acceptance failed: $candidate" }
        }
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $name / $pattern"
}
try {
    LaunchPeer 'host' 'host'
    WaitLog 'host' 'Joined P1'
    LaunchPeer 'client' 'client'
    WaitLog 'host' '\[LootComplete\] server'
    WaitLog 'client' '\[LootComplete\] client'
    LaunchPeer 'client' 'late'
    WaitLog 'late' '\[LootState\].*open=True mirror=True.*items=0'
    Start-Sleep -Seconds 3
    Write-Output "PASS: server guards, both teams, remote RPC and late join. Logs: $lootDirectory"
} finally {
    foreach ($peer in $lootPeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
