param([Parameter(Mandatory)][string]$RuntimeConfig)
$ErrorActionPreference = 'Stop'
$taskNotices = Join-Path $PSScriptRoot '..\docs\third-party'
$taskInventory = Get-Content -LiteralPath (Join-Path $taskNotices 'RUNTIME-NOTICES.json') -Raw | ConvertFrom-Json
$taskConfig = Get-Content -LiteralPath $RuntimeConfig -Raw | ConvertFrom-Json
$taskFramework = @($taskConfig.runtimeOptions.includedFrameworks | Where-Object name -EQ $taskInventory.runtime)
if ($taskFramework.Count -ne 1 -or $taskFramework[0].version -ne $taskInventory.version) {
    throw 'Bundled .NET runtime does not match RUNTIME-NOTICES.json. Refresh exact-version notices before packaging.'
}
foreach ($taskNotice in $taskInventory.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskNotices $taskNotice.file) -Algorithm SHA256).Hash -ne $taskNotice.sha256) {
        throw "Runtime notice hash mismatch: $($taskNotice.file)"
    }
}
Write-Output "Runtime notices verified: $($taskInventory.runtime) $($taskInventory.version)"
