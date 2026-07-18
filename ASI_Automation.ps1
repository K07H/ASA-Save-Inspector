# ------------------------
#   SCRIPT CONFIGURATION
# ------------------------

# ASA Save Inspector (ASI) executable location.
$asiPath = "C:\Users\Me\AppData\Roaming\ASA Save Inspector\ASA_Save_Inspector.exe"

# Our preset name in ASI.
$asiPresetName = "My Preset"

# The list of .ark save files to copy.
$arkFiles = @(
	"C:\ArkServers\TheIsland\ShooterGame\Saved\SavedArks\TheIsland_WP\TheIsland_WP.ark",
	"C:\ArkServers\Aberration\ShooterGame\Saved\SavedArks\Aberration_WP\Aberration_WP.ark",
	"C:\ArkServers\Genesis\ShooterGame\Saved\SavedArks\Genesis_WP\Genesis_WP.ark"
)

# Where to copy .ark save files (this is where our ASI preset expects to find the files).
$destinationFolder = "C:\Users\Me\Documents\ArkSaveFiles"

# ------------------------
#       SCRIPT LOGIC
# ------------------------

# Create destination folder if it does not exists.
if (-not (Test-Path $destinationFolder)) {
	New-Item -ItemType Directory -Path $destinationFolder -Force | Out-Null
}

# Copy .ark save files.
foreach ($file in $arkFiles) {
	if (Test-Path $file) {
		$fileName = Split-Path $file -Leaf
		$destination = Join-Path $destinationFolder $fileName
		
		Copy-Item -Path $file -Destination $destination -Force
		Write-Host "File copied: $file -> $destination" -ForegroundColor Green
	} else {
		Write-Host "File not found: $file" -ForegroundColor Red
	}
}

# Extract data with ASI, and then clean old data.
if (Test-Path $asiPath) {
	Start-Process -FilePath $asiPath -ArgumentList "-ExtractData `"$asiPresetName`" -Timeout 1800" -Wait
	Write-Host "ASA_Save_Inspector.exe executed with preset '$asiPresetName'." -ForegroundColor Green
	Start-Process -FilePath $asiPath -ArgumentList "-CleanOldData" -Wait
} else {
	Write-Host "ASA_Save_Inspector.exe not found at: $asiPath" -ForegroundColor Red
}
