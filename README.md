# Pokemon Go - Gym Analysis GoMapBot (C#)
_Pokemon Go - Gym Analysis (PGGA)_ is a tool to collect and analyze data of gyms and trainers in _Pokemon Go_. See [pogo.ansoft.at](http://pogo.ansoft.at) for an analysis of **Graz, Austria**.

The PGGA-GoMapBot collects gym data (in HTML format) from [gomap.eu](https://gomap.eu) (Firefox only!) and writes it to a txt-file. These files are a source for the [PGGA-Interface](https://github.com/Celegast/PGGA-Interface).

## How does it work?
### Short version
_GoMapBot_ scans the screen for gyms and extracts their information by emulating mouse and keyboard events. Then it moves the map and repeats this process until the end of the afore specified path is reached.
### Extended version
With gomap.eu open in Firefox (window maximized) _GoMapBot_ takes a screenshot and scans for gyms in the working area. One by one detected gyms then get processed in the following way:
1. Click on gym icon.
2. Take screenshot and search for gym popup boundaries (_greyX.png_ and _lastUpdate.png_).
3. Select gym information.
4. Right-click and select "View selection source".
5. Copy selected source code and append it to output file.
6. Take screenshot and look for _tabCloseX.png_.
7. Close tab 'DOM Source of Selection'.
8. Close gym popup.
After handling all gyms the map is moved one step along the specified path. This procedure is repeated until the end of the path is reached or the user aborts with _Esc_.

## User manual
### Setup
Depending on your operating system: Make sure to place the correct *.png files (_greyX_, _lastUpdate_ and _tabCloseX_) in the _img_-folder.

As _output file_ best set the _/data_ folder of the PGGA-Interface with a short txt-file name (e.g. "E:\PGGA\data\g.txt", with 'g' standing for Graz). In case _Add timestamp to ouput file name_ is selected it adds a string in the format "_yyyyMMdd_HHmmss" to written files (e.g. "g_20170714_143643.txt").

### First run and execution instructions
On first run GoMapBot creates a file named _config.dat_ in its directory. This file contains all settings and can be modified via a text editor. The most important values can be changed directly in the program window. It's initialized with the recommended parameters for a screen resolution of 1920x1080 (with taskbar on the bottom).

* Start _Firefox_ and maximize the window.
* Go to _gomap.eu_ and open your city map.
* Make sure only gyms are shown.
* Move the map to the starting position. Choose a zoom level with as little overlapping gym icons as possible. If an area is scanned for the first time best use a meandering path. Begin always at the same position, with the same zoom level!
* Enter the right path (cardinal points only, separated by a blank).
* Move the _GoMapBot_ window to the bottom of the screen, so it doesn't overlap with the working area.
* Hit _Start_ and enjoy the show.

Note: When Firefox is freshly opened it can be laggy on the first "View selection source", resulting in the gomap-tab to be closed. To prevent this make a manual "View selection source" first, by selecting a (gym) text and right-click, then close the tab. That should do the trick.

### Settings
#### Output file
See [Setup](https://github.com/Celegast/PGGA-GoMapBot#setup).
#### Path
Path on the map. Enter cardinal points ("N", "E", "S", "W") only, separated by a blank. The map gets dragged in the assigned direction by the amount of pixels specified in the [Working Area](https://github.com/Celegast/PGGA-GoMapBot#working-area) (width/height).
#### Working area
Screen area where gyms are looked for. Make sure to leave enough room for a maxed gyms' popup to be fully visible. If you have a different screen resolution than 1920x1080, or the layout of the map changes, then adjust these values accordingly. You can check the area by clicking on _Show area_, which moves the mouse pointer to each corner.

Enable _Map screenshots_ to save sector images (png-files) to subfolder _/map_. All gyms get labelled in this process.
#### Timing [ms]
* **WaitAfter\***: Time in ms after said action.
* **MapMovementSteps**: Number of mouse steps used for map movement. Goes hand in hand with _WaitAfterMapContourMovement_(!), as \#steps \* WaitAfterMapContourMovement = TimeForMovement
#### config.dat
* **PopupSize**: Minimum dimension of the gym popup. Used to speed up the bitmap detection process.
* **MapMovementPenalty**: To make sure gyms right on the edge of the working area are not missed, the map movement is reduced by this amount of pixels. Double readings get filtered out in the _PGGA-Interface_.
* **BrightnessThreshold**: Threshold of pixel brightness to detect gym icons.
* **DotSize**: Number of pixels in X/Y-direction to detect gym icons.
* **ForbiddenAreaRadius**: Once a gym icon is detected, the (pixel-)area around it will be locked to prevent multiple detections of the same gym.
