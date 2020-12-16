# SlideshowWallpaper
This is an Android app that provides a simple live wallpaper that displays a slideshow of the files in `Internal Storage/Pictures/Wallpapers`. If that folder does not exist or is empty, it will probably crash, so make sure that's not the case.

Additionally, images placed in the `Landscape` and `Portrait` folders inside the `Wallpapers` folder will be used as appropriate.

The slideshow will advance every minute on the clock, crossfading between images for one second. If your launcher supports it, the wallpaper will scroll based on your position in your homescreen (to be precise, it does this based on the offsets received from OnOffsetsChanged, which idk how a launcher provides it).
