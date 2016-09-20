# AppListManager
A library for loading information about currently installed apps list in Windows 10 Mobile *capability unlocked devices*.

Windows APIs for retrieving list of apps work slowly, and also they don't show installed legacy apps (WP8 xap packages). This library solves these issues.

This library includes a built-in caching mechanism, and after the first scan, It'll take only a few seconds to refresh data. Also, this library extracts information about legacy apps by calling a legacy API and then extracting information manually from each app's files.


##How to use

First, you need to create an instance of `LoadAppData`:

```
LoadAppData lad = new LoadAppData();
```

* This takes one second or two, because of the delay caused by Legacy APIs. You can disable legacy apps by passing a parameter to the constructor though.

Then, you need to call static function `LoadAppData.LoadCachedAppList()` to load existing cache file. (If it doesn't exist, this function will return `false`)

And finally, you can call `LoadApps`:

```
lad.LoadApps();
```

* This will scan the device, update the cache, and save the new updated list. (If cache was not present, it'll be created here)


##Notes

- This library creates a file named `applistcache.txt` and a folder named `Logos` inside `LocalCacheFolder` of your app.


##Known issues

- Curretnly the display name and publisher name of some *legacy* apps (those with *reference-based names*) cannot be retrieved. Modern apps (compiled for 8.1 or 10) with reference-based names will load fine though.
