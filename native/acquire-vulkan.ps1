param([string]$ArchivePath)
# Vulkan is part of the reviewed recursive native closure. A separate loader
# would invalidate its pinned provenance and public packaging checks.
& (Join-Path $PSScriptRoot 'acquire-libmpv.ps1') -ArchivePath $ArchivePath
