# Documentation for the old filtering system

This is the documentation for the old filtering system, the one that was used prior to version 3.0 of ASI.
This documentation is now deprecated starting at v3.0.

## 1) Filters
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

## 2) Groups
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

## 3) Good practices when using Filters and Groups
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

## 4) Default filters
Some default filters are enabled by default on Dinos and Structures pages:
* Dinos page: Only displays tamed dinos by default (IsTamed is True).
* Structures page: Only displays structures belonging to a tribe (Tribe ID is greater than 49999).

You can remove these default filters if you want to see wild dinos and structures.
