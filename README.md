# AppListManager
A library for loading information about currently installed apps list in Windows 10 Mobile *capability unlocked devices*.

Windows APIs for retrieving list of apps work slowly. This library includes a built-in caching mechanism, and after the first scan, It'll take only a few seconds to refresh data.


##How to use

First, you need to create an instance of `LoadAppData`:

```
LoadAppData lad = new LoadAppData();
```

Then, you need to call static function `LoadAppData.LoadCachedAppList()` to load existing cache file. (If it doesn't exist, this function will return `false`)

And finally, you can call `LoadApps`:

```
lad.LoadApps();
```

* This will scan the device, update the cache, and save the new updated list. (If cache was not present, it'll be created here)


##Notes

- This library creates a file named `applistcache.txt` and a folder named `Logos` inside `LocalCacheFolder` of your app.


##Known issues

- Currently the *publisher name* of some apps (those with *reference-based names*) cannot be retrieved; `Publisher` attribute will be blank for these cases.


##License

You can use this library in your apps and modify it to fit your needs, as long as you mention the name `AppListManager` and the name of the publisher `Mahdi Ghiasi` with a link to this repository in Github.