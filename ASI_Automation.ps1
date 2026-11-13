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

# Extract data with ASI, and then clean old data if the extraction succeeded.
# ASI exit codes for -ExtractPreset: 0 = OK, 1 = at least one extraction failed, 2 = preset not found or empty, 3 = timeout, 4 = unexpected error, 5 = initialization timeout, 6 = ASI is already running (close the ASI window first).
if (Test-Path $asiPath) {
	$extract = Start-Process -FilePath $asiPath -ArgumentList "-ExtractPreset `"$asiPresetName`" -Timeout 1800" -Wait -PassThru
	if ($extract.ExitCode -eq 0) {
		Write-Host "ASA_Save_Inspector.exe executed with preset '$asiPresetName'." -ForegroundColor Green
		$clean = Start-Process -FilePath $asiPath -ArgumentList "-CleanOldData" -Wait -PassThru
		if ($clean.ExitCode -eq 0) {
			Write-Host "Old data has been cleaned." -ForegroundColor Green
		} elseif ($clean.ExitCode -eq 6) {
			Write-Host "Old data has NOT been cleaned: another ASI instance is running." -ForegroundColor Yellow
		} else {
			Write-Host "Old data cleaning failed (exit code $($clean.ExitCode))." -ForegroundColor Red
		}
	} elseif ($extract.ExitCode -eq 6) {
		Write-Host "Extraction of preset '$asiPresetName' skipped: another ASI instance is running (close the ASI window, or wait for the other extraction to finish)." -ForegroundColor Red
		exit $extract.ExitCode
	} else {
		Write-Host "Extraction of preset '$asiPresetName' failed (exit code $($extract.ExitCode)). Old data has NOT been cleaned." -ForegroundColor Red
		exit $extract.ExitCode
	}
} else {
	Write-Host "ASA_Save_Inspector.exe not found at: $asiPath" -ForegroundColor Red
	exit 1
}
