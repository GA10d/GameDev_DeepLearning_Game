param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$taskSource = Join-Path $ProjectRoot 'docs\levels.json'
$taskTarget = Join-Path $ProjectRoot 'UnityGame\Assets\Resources\Campaign\levels.json'
$taskCampaign = Get-Content -LiteralPath $taskSource -Encoding UTF8 -Raw | ConvertFrom-Json
if ($taskCampaign.levels.Count -ne 55) { throw 'Expected 55 campaign nodes; review the curriculum version before changing this contract.' }
Copy-Item -LiteralPath $taskSource -Destination $taskTarget -Force
Write-Output "Campaign synchronized: $($taskCampaign.levels.Count) nodes"
