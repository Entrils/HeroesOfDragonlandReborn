param(
    [ValidateSet('A','B')][string]$AttackingTeam = 'B',
    [string]$Executable = 'Builds/MultiplayerFortress/DragonlandFortress.exe',
    [string]$ValidationRoot = 'Builds/MultiplayerFortressAcceptance'
)
$ErrorActionPreference = 'Stop'
$fortressExe = (Resolve-Path -LiteralPath $Executable).Path
$fortressDirectory = [IO.Path]::GetFullPath((Join-Path (Join-Path (Get-Location) $ValidationRoot) $AttackingTeam))
New-Item -ItemType Directory -Path $fortressDirectory -Force | Out-Null
$fortressRoom = 'hodr-m8-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$fortressPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function LaunchPeer([string]$mode, [string]$name) {
    $log = Join-Path $fortressDirectory "$name.log"
    $peer = Start-Process -FilePath $fortressExe -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$fortressRoom,
        '-hodrProbe','-hodrFortress',$AttackingTeam,'-hodrQuit','420','-logFile',('"' + $log + '"'))
    $fortressPeers.Add($peer)
}
function WaitLog([string]$name, [string]$pattern, [int]$seconds = 90) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    $path = Join-Path $fortressDirectory "$name.log"
    while ([DateTime]::UtcNow -lt $deadline) {
        foreach ($file in @('host','client','late')) {
            $candidate = Join-Path $fortressDirectory "$file.log"
            if ((Test-Path -LiteralPath $candidate) -and (Select-String -LiteralPath $candidate -Pattern '\[FortressCheck\] FAIL|Start failed:|Exception:|spawn is not on its NavMesh' -Quiet)) { throw "Fortress acceptance failed: $candidate" }
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
    WaitLog 'host' '\[FortressComplete\] server=True' 240
    WaitLog 'client' '\[FortressComplete\] server=False'
    LaunchPeer 'client' 'late'
    WaitLog 'late' '\[FortressComplete\] server=False'
    Start-Sleep -Seconds 3
    & python (Join-Path $PSScriptRoot 'VerifyMultiplayerFortress.py') (Split-Path -Parent $fortressDirectory) $AttackingTeam
    if ($LASTEXITCODE -ne 0) { throw "Fortress log verification failed for team $AttackingTeam" }
    Write-Output "PASS: fortress team $AttackingTeam combat, match end and late join. Logs: $fortressDirectory"
} finally {
    foreach ($peer in $fortressPeers) {
        $peer.Refresh()
        if (-not $peer.HasExited) { $peer.Kill(); $peer.WaitForExit(5000) | Out-Null }
        $peer.Dispose()
    }
}
