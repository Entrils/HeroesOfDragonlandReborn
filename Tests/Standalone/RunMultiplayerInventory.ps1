param(
    [string]$Executable = 'Builds/MultiplayerInventory/DragonlandInventory.exe',
    [string]$ValidationRoot = 'Builds/MultiplayerInventoryValidation'
)
$ErrorActionPreference = 'Stop'
$inventoryExe = (Resolve-Path -LiteralPath $Executable).Path
$inventoryDirectory = Join-Path (Get-Location) $ValidationRoot
New-Item -ItemType Directory -Path $inventoryDirectory -Force | Out-Null
$inventoryRoom = 'hodr-m4-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$inventoryPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function LaunchPeer([string]$mode, [string]$name) {
    $log = Join-Path $inventoryDirectory "$name.log"
    Set-Content -LiteralPath $log -Value '' -Encoding UTF8
    $peer = Start-Process -FilePath $inventoryExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$inventoryRoom,
        '-hodrProbe','-hodrInventory','-hodrQuit','150','-logFile',$log)
    $inventoryPeers.Add($peer)
}
function WaitLog([string]$name, [string]$pattern, [int]$seconds = 60) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    $path = Join-Path $inventoryDirectory "$name.log"
    while ([DateTime]::UtcNow -lt $deadline) {
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern '\[InventoryCheck\] FAIL' -Quiet)) {
            throw "Inventory assertion failed in $name"
        }
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $name / $pattern"
}
try {
    LaunchPeer 'host' 'host'
    WaitLog 'host' 'Joined P1'
    LaunchPeer 'client' 'client1'
    WaitLog 'host' 'Joined P2'
    WaitLog 'host' '\[InventoryProbe\] COMPLETE participant=1'
    WaitLog 'client1' '\[InventoryProbe\] COMPLETE participant=2'
    LaunchPeer 'client' 'late'
    WaitLog 'host' 'Joined P3'
    WaitLog 'late' '\[InventoryState\] participant=1 .*private=False hidden=True .*main=True chest=True'
    WaitLog 'late' '\[InventoryState\] participant=2 .*private=False hidden=True .*main=True chest=True'
    Start-Sleep -Seconds 2
    Write-Output "Recorded authoritative inventory/equipment and late join in $inventoryDirectory"
} finally {
    foreach ($peer in $inventoryPeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
