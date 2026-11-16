param(
    [ValidateSet("x86", "x64", "arm64")]
    [string[]]$Platforms = @("x86", "x64", "arm64")
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$appProject = Join-Path $root "ASA_Save_Inspector\ASA_Save_Inspector.csproj"
$wixProject = Join-Path $root "ASI_Installer\ASI_Installer.wixproj"
$outDir = Join-Path $root "Installers"
$signScript = Join-Path $root "ASI_cert_step3_sign.ps1"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# --- Get ASI version from Directory.Build.props ---
$propsPath = Join-Path $root "Directory.Build.props"
[xml]$props = Get-Content $propsPath -Raw
$Version = ($props.Project.PropertyGroup | ForEach-Object { $_.AsiVersion } | Where-Object { $_ } | Select-Object -First 1)
$parsed = $null
if (-not $Version -or -not [Version]::TryParse($Version, [ref]$parsed) -or $Version.Split('.').Count -ne 3) {
    throw "ASI version is not correct in '$propsPath': '$Version' (expected format: X.Y.Z)."
}
Write-Host "=== Version: $Version ===" -ForegroundColor Cyan

foreach ($arch in $Platforms) {
    $appPlatform = if ($arch -eq "arm64") { "ARM64" } else { $arch }
    $publishDir = Join-Path $root "ASA_Save_Inspector\bin\win-$arch\publish"

    Write-Host "=== [$arch] Publishing application ===" -ForegroundColor Cyan
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    dotnet publish $appProject -c Release -p:Platform=$appPlatform -p:PublishProfile=win-$arch
    if ($LASTEXITCODE -ne 0) { throw "Publishing $arch failed." }

    if (Test-Path $signScript) {
        & powershell.exe -ExecutionPolicy Bypass -File $signScript -Target app -Arch $arch
        if ($LASTEXITCODE -ne 0) { throw "Signing $arch application failed." }
    }
    else { Write-Host "=== [$arch] Signing script not found at '$signScript' ===" -ForegroundColor Red }

    Write-Host "=== [$arch] Generating MSI ===" -ForegroundColor Cyan
    dotnet build $wixProject -c Release -p:Platform=$arch -p:ProductVersion=$Version "-p:AppPublishDir=$publishDir"
    if ($LASTEXITCODE -ne 0) { throw "MSI $arch generation failed." }

    $msi = Get-ChildItem (Join-Path $root "ASI_Installer\bin") -Recurse -Filter "ASA_Save_Inspector_Setup_$arch.msi" |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $target = Join-Path $outDir "ASA_Save_Inspector_Setup_$arch.msi"
    Copy-Item $msi.FullName $target -Force

    if (Test-Path $signScript) {
        & powershell.exe -ExecutionPolicy Bypass -File $signScript -Target msi -Arch $arch
        if ($LASTEXITCODE -ne 0) { throw "Signing $arch MSI failed." }
    }
    else { Write-Host "=== [$arch] Signing script not found at '$signScript' ===" -ForegroundColor Red }

    Write-Host "=== [$arch] OK: $target (v$Version) ===" -ForegroundColor Green
}
