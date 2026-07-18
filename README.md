# ASA Save Inspector
ASA Save Inspector (ASI) is an ARK Survival Ascended save file analyzer.

ASI uses [ArkParse](https://github.com/VincentHenauGithub/ark-save-parser) to extract data from the save file, and provides a modern graphical user interface to analyze the data.

Note: This application is still under development, more features will be added in the future.

## Table of content
1. [Features](#1-features)
2. [Demonstration video](#2-demonstration-video)
3. [Installation](#3-installation)
4. [Quick start](#4-quick-start)
5. [Filters and groups](#5-filters-and-groups)
6. [Custom maps](#6-custom-maps)
7. [Command Line Interface](#7-command-line-interface)
8. [Feature requests and bug reports](#8-feature-requests-and-bug-reports)
9. [Contributing](#9-contributing)
10. [Donations](#10-donations)
11. [Discord](#11-discord)

## 1) Features
- View, search, filter and sort the various game objects (dinos, items, structures and so on).
- Visualize game objects on a map (see "Other" tab to open the map).
- Quickly export some game objects to JSON (using right click on the game object lines).

## 2) Demonstration video
https://www.youtube.com/watch?v=LOZbUW5rd0A

## 3) Installation
1. Download and install the following two requirements:<br>
[.NET Desktop Runtime 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)<br>
[Python 3](https://www.python.org/downloads/)<br>
2. Download the latest installer here: [https://github.com/K07H/ASA-Save-Inspector/releases/latest](https://github.com/K07H/ASA-Save-Inspector/releases/latest)<br>
*You can choose either x64 or x86 installer (if you don't know which one to download, simply get the x64 one).*
3. Once the installer is downloaded, launch it and follow the installation guide.

## 4) Quick start
1. Launch ASA Save Inspector, then click on "Settings" in the left menu.
2. Configure "Python Setup".
3. Extract, then load the "JSON Data".
4. You can now navigate in the app using left menu buttons!

## 5) Filters and groups
The filtering feature has been fully reworked in v3.0. However, if you are still using an older version of ASI, you can find the old filtering documentation here: [https://github.com/K07H/ASA-Save-Inspector/blob/main/OLD_FILTERING.md](https://github.com/K07H/ASA-Save-Inspector/blob/main/OLD_FILTERING.md)

## 6) Custom maps
You can add your own maps in file "maps_info.json" inside ASI's data folder (*to access ASI's data folder, click on "Other" tab then click on "Open ASI data folder" button).*<br>
Here's an example of a custom map added in "maps_info.json": [Custom map example](AddingCustomMap.jpg)<br>
Your map image must be 5096 by 5096 pixels for the minimap to work properly, and it must have a 500 pixels margin, like so: [Map image format](MapImageFormat.jpg)<br>
*If you want me to add some modded maps just let me know on the [Discord](https://discord.gg/dPgTprNyn9) inside #asa-save-inspector channel.*

## 7) Command Line Interface
Available commands:
* `-ExtractPreset`
  * This command takes the preset name as argument (in double quotes).
  * Optionally you can specify a timeout with "-Timeout Seconds", but it needs to be placed at the end of the command (order matters). Default timeout value is 1800 seconds (so if after 30 minutes the entire preset is still not extracted, ASA Save Inspector gets shutdown).
  * Usage example: `ASA_Save_Inspector.exe -ExtractPreset "My preset name" -Timeout 1800`
* `-CleanOldData`
  * This command removes previous extractions data that has been stored on your drive (it only keeps latest extraction data for each map).
  * Usage example: `ASA_Save_Inspector.exe -CleanOldData`

And here's a powershell script example that copy save files into a specific folder, extracts data from these saves files and cleans old data: https://raw.githubusercontent.com/K07H/ASA-Save-Inspector/refs/heads/main/ASI_Automation.ps1

## 8) Feature requests and bug reports
You can report bugs or ask for new features in the issues tab here: https://github.com/K07H/ASA-Save-Inspector/issues

## 9) Contributing
Contributions are welcome, simply make a pull request on the repository.

## 10) Donations
Donations are not required, but highly appreciated: https://paypal.me/osubmarin

## 11) Discord
Don't hesitate to join us on Discord here: https://discord.gg/dPgTprNyn9
