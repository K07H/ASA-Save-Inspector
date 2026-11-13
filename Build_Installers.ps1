param(
    [string]$Version = "4.0.0",
    [ValidateSet("x86", "x64", "arm64")]
    [string[]]$Platforms = @("x86", "x64", "arm64")
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$appProject = Join-Path $root "ASA_Save_Inspector\ASA_Save_Inspector.csproj"
$wixProject = Join-Path $root "ASI_Installer\ASI_Installer.wixproj"
$outDir = Join-Path $root "Installers"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

foreach ($arch in $Platforms) {
    $appPlatform = if ($arch -eq "arm64") { "ARM64" } else { $arch }
    $publishDir = Join-Path $root "ASA_Save_Inspector\bin\win-$arch\publish"

    Write-Host "=== [$arch] Publication de l'application ===" -ForegroundColor Cyan
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    dotnet publish $appProject -c Release -p:Platform=$appPlatform -p:PublishProfile=win-$arch
    if ($LASTEXITCODE -ne 0) { throw "Publication $arch échouée." }

    $signApp = Join-Path $root "ASI_cert_step3_sign_app_$arch.ps1"
    if (Test-Path $signApp) { & powershell.exe -ExecutionPolicy Bypass -File $signApp }
	else { Write-Host "=== [$arch] App signing script not found at '$signApp' ===" -ForegroundColor Red }

    Write-Host "=== [$arch] Génération du MSI ===" -ForegroundColor Cyan
    dotnet build $wixProject -c Release -p:Platform=$arch -p:ProductVersion=$Version "-p:AppPublishDir=$publishDir"
    if ($LASTEXITCODE -ne 0) { throw "Génération du MSI $arch échouée." }

    $msi = Get-ChildItem (Join-Path $root "ASI_Installer\bin") -Recurse -Filter "ASA_Save_Inspector_Setup_$arch.msi" |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $target = Join-Path $outDir "ASA_Save_Inspector_Setup_$arch.msi"
    Copy-Item $msi.FullName $target -Force

    $signMsi = Join-Path $root "ASI_cert_step3_sign_msi_wix_$arch.ps1"
    if (Test-Path $signMsi) { & powershell.exe -ExecutionPolicy Bypass -File $signMsi }
	else { Write-Host "=== [$arch] MSI signing script not found at '$signMsi' ===" -ForegroundColor Red }

    Write-Host "=== [$arch] OK : $target ===" -ForegroundColor Green
}
