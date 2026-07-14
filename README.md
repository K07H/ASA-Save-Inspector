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
7. [Feature requests and bug reports](#7-feature-requests-and-bug-reports)
8. [Contributing](#8-contributing)
9. [Donations](#9-donations)
10. [Discord](#10-discord)

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

## 7) Feature requests and bug reports
You can report bugs or ask for new features in the issues tab here: https://github.com/K07H/ASA-Save-Inspector/issues

## 8) Contributing
Contributions are welcome, simply make a pull request on the repository.

## 9) Donations
Donations are not required, but highly appreciated: https://paypal.me/osubmarin

## 10) Discord
Don't hesitate to join us on Discord here: https://discord.gg/dPgTprNyn9
