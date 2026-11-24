param([string]$Executable = 'Builds/M81/Dragonland.exe')
$ErrorActionPreference = 'Stop'
$playerExe = (Resolve-Path -LiteralPath $Executable).Path
$queue = [Guid]::NewGuid().ToString('N')
$logs = New-Item -ItemType Directory -Path "Builds/RankedAcceptance-$($queue.Substring(0,8))"
$peers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$profiles = @{}

function Launch-RankedPeer([string]$label, [string]$mode, [string]$profile = '') {
    $file = Join-Path $logs.FullName "$label.log"
    $arguments = @('-batchmode','-nographics','-job-worker-count','1','-logFile',('"' + $file + '"'))
    if ($mode -eq 'verify') { $arguments += @('-hodrProfileTest',$profile,'-hodrVerifyRanked') }
    else {
        $arguments += @('-hodrMode',$mode,'-hodrQuickQueue',$queue)
        if ($mode -eq 'ranked') {
            $profile = [Guid]::NewGuid().ToString('N')
            $profiles[$label] = $profile
            $arguments += @('-hodrPlaytestReady','6','-hodrRankedCheck','-hodrProfileTest',$profile)
        }
    }
    $peers.Add((Start-Process -FilePath $playerExe -WindowStyle Hidden -PassThru -ArgumentList $arguments))
}

function Wait-RankedLog([string]$label, [string]$pattern) {
    $path = Join-Path $logs.FullName "$label.log"
    $deadline = [DateTime]::UtcNow.AddSeconds(100)
    while ([DateTime]::UtcNow -lt $deadline) {
        foreach ($file in Get-ChildItem -LiteralPath $logs.FullName -Filter '*.log') {
            if (Select-String -LiteralPath $file.FullName -Pattern 'Exception:|Spawn failed:|Start failed:' -Quiet) {
                throw "Ranked acceptance failed in $($file.FullName)"
            }
        }
        if ((Test-Path -LiteralPath $path) -and (Select-String -LiteralPath $path -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 350
    }
    throw "Timeout waiting for $label / $pattern in $($logs.FullName)"
}
function Stop-RankedPeers {
    foreach ($peer in $peers) {
        $peer.Refresh()
        if (!$peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
    $peers.Clear()
}
try {
    Launch-RankedPeer 'unranked' 'quick'
    Wait-RankedLog 'unranked' 'Quick Play lobby created'
    Launch-RankedPeer 'ranked-host' 'ranked'
    Wait-RankedLog 'ranked-host' 'Ranked lobby created'
    Wait-RankedLog 'ranked-host' 'state=Lobby'
    foreach ($i in 1..5) {
        Launch-RankedPeer "ranked-$i" 'ranked'
        Wait-RankedLog "ranked-$i" 'Ranked lobby joined'
    }
    Wait-RankedLog 'ranked-host' 'rated=True rating=(1025|975) matches=1'
    Wait-RankedLog 'ranked-host' 'rated=False rating=(1025|975) matches=1'
    foreach ($i in 1..5) { Wait-RankedLog "ranked-$i" 'rated=True rating=(1025|975) matches=1' }
    Stop-RankedPeers
    Launch-RankedPeer 'reload-host' 'verify' $profiles['ranked-host']
    Launch-RankedPeer 'reload-guest' 'verify' $profiles['ranked-1']
    Wait-RankedLog 'reload-host' 'PASS persisted profile and duplicate receipt rejection'
    Wait-RankedLog 'reload-guest' 'PASS persisted profile and duplicate receipt rejection'
    Write-Output "PASS: isolated queues, rated full round, ineligible finish, and rating reload. Logs: $($logs.FullName)"
} finally { Stop-RankedPeers }
