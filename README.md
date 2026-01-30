# ASA Save Inspector
ASA Save Inspector (ASI) is an ARK Survival Ascended save file analyzer.

ASI uses [ArkParse](https://github.com/VincentHenauGithub/ark-save-parser) to extract data from the save file, and provides a modern graphical user interface to analyze the data.

Note: This application is still under development, more features will be added in the future.

## Table of content
1. [Features](#1-features)
2. [Demonstration video](#2-demonstration-video)
3. [Installation](#3-installation)
4. [Quick start](#4-quick-start)
5. [Quick filters](#5-quick-filters)
6. [Filters and groups](#6-filters-and-groups)
   - 6.a [Filters](#6a-filters)
   - 6.b [Groups](#6b-groups)
   - 6.c [Good practices when using Filters and Groups](#6c-good-practices-when-using-filters-and-groups)
   - 6.d [Default filters](#6d-default-filters)
7. [Feature requests and bug reports](#7-feature-requests-and-bug-reports)
8. [Contributing](#8-contributing)
9. [Donations](#9-donations)
10. [Discord](#10-discord)

## 1) Features
- View, search, filter and sort the various game objects (dinos, items, structures and so on).
- Visualize game objects on a map (double clicking on a map marker will select the associated game object in the app's main window).
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

## 5) Quick filters
Use the dropdowns to quickly filter by tribe, item, dino or structure type. For more advanced filtering refer to [Filters and groups](#5-filters-and-groups).

## 6) Filters and groups

This entire section 6) is now deprecated in ASI v3. The filtering system as been fully reworked.

### 6.a) Filters
Filters are divided into 2 categories: "OR" and "AND".<br>
When filtering happens, ASI will:
1. filter the items using the "OR" filters, this gives the first subset.
2. filter the subset obtained at step 1 using the "AND" filters.

For example if I'm on the dinos page, and I have 3 filters:
- OR "level" greater than 216.
- OR "base food" equals 35.
- AND "tribe name" equals "A Great Tribe".

ASI will first select all dinos having their "base food" stat set to 35 or their "level" greater than 216. That's the first subset. Then it will filter that first subset to remove any dino that does not belong to the tribe called "A Great Tribe".<br>
The filters order does not matter. The result would be exactly the same if I had the following filters in the list:
- OR "level" greater than 216.
- AND "tribe name" equals "A Great Tribe".
- OR "base food" equals 35.

Currently set filters can be saved into a preset if you plan to use them later, or if you want to use groups (see below).

### 6.b) Groups
Groups are divided into 2 categories: "OR" and "AND".<br>
A group is composed of 1 or more filters preset.<br>
When grouping happens, ASI will:
1. select the items that matches every "AND" group, this give the first subset.
2. happend to the subset obtained at step 1 all the items that matches each "OR" group.

For example if I'm on the dinos page, and I previously created the following 3 filters presets:
- Filters preset 1: OR "level" greater than 216 OR "base food" equals 35.
- Filters preset 2: AND "tamed" is true.
- Filters preset 3: AND "tribe name" is "A Great Tribe".

Now I can combine these various presets using grouping. For example if I have the following filters presets in my group:
- AND "Filters preset 1".
- AND "Filters preset 2".
- OR "Filters preset 3".

ASI will display all tamed dinos having a level greater than 216 or a base food stat of 35, OR any dino from the tribe "A Great Tribe" (no matter their level or base food stat).<br>
The filters presets order in the group does not matter. The result would be exactly the same if I had the following filters presets in the group:
- AND "Filters preset 2".
- OR "Filters preset 3".
- AND "Filters preset 1".

### 6.c) Good practices when using Filters and Groups
When you create a filters preset, it's a good practice to make sure all filters inside your preset have the same operator (either "OR", or "AND").<br>
This will help you later on when using groups, because having different operators inside filters presets can quickly produce ambiguous results when you start combining your filters presets in a group (unless you understand exactly what's happening during filtering and grouping phases).

As you can see in the grouping example I gave above (at the beginning of "Groups" paragraph):
- Filters preset 1 only contains "OR" filters.
- Filters preset 2 only contains "AND" filters.
- Filters preset 3 only contains "AND" filters.

This makes it easy to use grouping, because our grouping syntax will produce an easily predictable result.

For example, if we have the following grouping syntax:<br>
AND FiltersPreset(x AND y)  OR  FiltersPreset(c)  AND  FiltersPreset(a AND b)  OR  FiltersPreset(j AND k)<br>
this will produce the following easily predictable result:<br>
Items = (x AND y AND a AND b) OR (c) OR (j AND k)<br>

Whereas if you start mixing operators inside filters presets, this can lead to confusing results when grouping. For example:<br>
AND FiltersPreset(x AND y OR z)  OR  FiltersPreset(i AND j OR k)  AND  FiltersPreset(a OR b AND c)<br>
will produce the following result:<br>
Items = (x AND y OR z AND a OR b AND c) OR (i AND j OR k)<br>
As you can see, this result is harder to read, and harder to make sense of.<br>
That's why it's a good practice to only use one type of operator inside a filters preset.<br>

If with all these explanations and examples Filters and Groups are still confusing to you, the simplest method is to only use "AND" operators in your filters presets, and to use grouping for your "OR" conditions.<br>
For example, you start by creating your AND filters presets:
- Filters preset 1: AND a AND b AND c
- Filters preset 2: AND j AND k
- Filters preset 3: AND x

Then you create a group which will "OR" your various filters presets:<br>
OR  FiltersPreset1  OR  FiltersPreset2  OR  FiltersPreset3<br>
This will produce the following result:<br>
(a AND b AND c) OR (j AND k) OR (x)<br>

### 6.d) Default filters
Some default filters are enabled by default on Dinos and Structures pages:
* Dinos page: Only displays tamed dinos by default (IsTamed is True).
* Structures page: Only displays structures belonging to a tribe (Tribe ID is greater than 49999).

You can remove these default filters if you want to see wild dinos and structures.

## 7) Feature requests and bug reports
You can report bugs or ask for new features in the issues tab here: https://github.com/K07H/ASA-Save-Inspector/issues

## 8) Contributing
Contributions are welcome, simply make a pull request on the repository.

## 9) Donations
Donations are not required, but highly appreciated: https://paypal.me/osubmarin

## 10) Discord
Don't hesitate to join us on Discord here: https://discord.gg/dPgTprNyn9
