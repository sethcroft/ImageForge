param([switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
dotnet publish "$PSScriptRoot/ImageForge.App/ImageForge.App.csproj" -c Release -r win-x64 --self-contained $selfContained -o "$PSScriptRoot/artifacts/win-x64"
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Write-Host "Ready: $PSScriptRoot/artifacts/win-x64/ImageForge.exe"
