param([string]$Executable = 'Builds/SessionSkills/DragonlandSkills.exe')
$ErrorActionPreference = 'Stop'
$skillExecutable = (Resolve-Path -LiteralPath $Executable).Path
$skillRoot = Join-Path (Get-Location) ('Builds/SkillsAcceptance-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $skillRoot | Out-Null
$skillRoom = 'hodr-skills-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$skillPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function StartSkillPeer([string]$mode) {
    $skillLog = Join-Path $skillRoot ($mode + '.log')
    $skillPeers.Add((Start-Process -FilePath $skillExecutable -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode','-nographics','-hodrMode',$mode,'-hodrRoom',$skillRoom,'-hodrSkills','-logFile',('"' + $skillLog + '"'))))
}
function WaitSkillLog([string]$mode, [string]$pattern) {
    $skillDeadline = [DateTime]::UtcNow.AddSeconds(100)
    while ([DateTime]::UtcNow -lt $skillDeadline) {
        foreach ($skillLog in Get-ChildItem -LiteralPath $skillRoot -Filter '*.log') {
            if (Select-String -LiteralPath $skillLog.FullName -Pattern '\[SkillCheck\] FAIL|Exception:|Start failed:|Spawn failed:' -Quiet) {
                throw "Skills acceptance failed: $($skillLog.FullName)"
            }
        }
        $skillPath = Join-Path $skillRoot ($mode + '.log')
        if ((Test-Path -LiteralPath $skillPath) -and (Select-String -LiteralPath $skillPath -Pattern $pattern -Quiet)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out: $mode / $pattern. Logs: $skillRoot"
}
try {
    StartSkillPeer 'host'
    WaitSkillLog 'host' '\[MatchLifecycle\] round=0 state=Lobby'
    StartSkillPeer 'client'
    WaitSkillLog 'host' '\[SkillsComplete\] server=True'
    WaitSkillLog 'client' '\[SkillsComplete\] server=False'
    Write-Output "PASS: owner RPCs, server-only effects, prerequisites, Mana, cooldowns, damage, defense, passive and session reset. Logs: $skillRoot"
} finally {
    foreach ($skillPeer in $skillPeers) {
        $skillPeer.Refresh()
        if (-not $skillPeer.HasExited) { $skillPeer.Kill(); $skillPeer.WaitForExit(5000) | Out-Null }
        $skillPeer.Dispose()
    }
}
