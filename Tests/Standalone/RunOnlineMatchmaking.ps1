param([string]$Executable = 'Builds/M66/Dragonland.exe')
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$id = [Guid]::NewGuid().ToString('N')
$logs = New-Item -ItemType Directory -Path "Builds/OnlineAcceptance-$($id.Substring(0,8))"
$peers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

function Start-Peer([string]$label, [string]$mode, [string]$room = 'unused', [switch]$ready) {
    $log = Join-Path $logs.FullName "$label.log"
    $arguments = @('-batchmode','-nographics','-job-worker-count','1','-hodrMode',$mode,'-hodrRoom',$room,
        '-hodrQuickQueue',$id,'-logFile',('"' + $log + '"'))
    if ($ready) { $arguments += @('-hodrPlaytestReady','2') }
    $process = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru -ArgumentList $arguments
    $peers.Add($process)
    return $process
}

function Wait-Log([string]$label, [string]$pattern, [string]$expectedPhoton = '^$') {
    $file = Join-Path $logs.FullName "$label.log"
    $deadline = [DateTime]::UtcNow.AddSeconds(65)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $file) {
            $unexpected = Select-String -LiteralPath $file -Pattern 'Exception:|Spawn failed:' |
                Where-Object { $_.Line -notmatch $expectedPhoton } | Select-Object -First 1
            if ($null -ne $unexpected) { throw "Runtime failure in $file" }
            $match = Select-String -LiteralPath $file -Pattern $pattern | Select-Object -First 1
            if ($null -ne $match) { return $match.Line }
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Timed out waiting for $label / $pattern. Logs: $($logs.FullName)"
}

function Stop-OwnedPeer($process) {
    $process.Refresh()
    if (!$process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
}

try {
    $quickHost = Start-Peer 'quick-host' 'quick' -ready
    Wait-Log 'quick-host' 'Quick Play lobby created\.' | Out-Null
    Wait-Log 'quick-host' 'state=Lobby' | Out-Null
    $quickGuest = Start-Peer 'quick-guest' 'quick' -ready
    Wait-Log 'quick-guest' 'Quick Play lobby joined\.' | Out-Null
    Wait-Log 'quick-host' 'state=Playing' | Out-Null
    $quickNext = Start-Peer 'quick-next' 'quick'
    Wait-Log 'quick-next' 'Quick Play lobby created\.' | Out-Null
    @($quickGuest,$quickHost,$quickNext) | ForEach-Object { Stop-OwnedPeer $_ }

    $privateHost = Start-Peer 'private-host' 'private-host'
    $line = Wait-Log 'private-host' 'Host connected / pm\d+-([A-Z2-9]{10})'
    $code = [regex]::Match($line, 'pm\d+-([A-Z2-9]{10})').Groups[1].Value
    Wait-Log 'private-host' 'state=Lobby' | Out-Null
    $privateGuest = Start-Peer 'private-guest' 'private-join' ($code.ToLowerInvariant())
    Wait-Log 'private-guest' 'Client connected / pm\d+-' | Out-Null
    Stop-OwnedPeer $privateHost
    Wait-Log 'private-guest' '\[NetworkState\] LobbyDisconnected' | Out-Null
    Stop-OwnedPeer $privateGuest
    $missing = Start-Peer 'unavailable' 'private-join' 'ZZZZZZZZZZ'
    Wait-Log 'unavailable' '\[NetworkState\] SessionNotFound' 'Photon\.Realtime\.OperationException:.*ErrorCode: 32758' | Out-Null
    Stop-OwnedPeer $missing
    $invalid = Start-Peer 'invalid' 'private-join' 'BAD'
    Wait-Log 'invalid' '\[NetworkState\] FailedJoin' | Out-Null
    Stop-OwnedPeer $invalid

    $name = "accept-$id"
    $custom = Start-Peer 'custom-host' 'custom-host' $name
    Wait-Log 'custom-host' 'state=Lobby' | Out-Null
    for ($i = 1; $i -le 5; $i++) {
        $null = Start-Peer "custom-$i" 'custom-join' $name
        Wait-Log "custom-$i" 'Client connected / custom\d+-' | Out-Null
        Wait-Log 'custom-host' "count=$($i+1)/6" | Out-Null
    }
    $null = Start-Peer 'full' 'custom-join' $name
    Wait-Log 'full' '\[NetworkState\] SessionFull' 'Photon\.Realtime\.OperationException:.*ErrorCode: 32765' | Out-Null
    Write-Output "PASS: Quick Play create/join, active-match exclusion, private code, lobby disconnect, unavailable and invalid code, named custom lobby, and full lobby. Logs: $($logs.FullName)"
} finally {
    foreach ($peer in $peers) { Stop-OwnedPeer $peer; $peer.Dispose() }
}
