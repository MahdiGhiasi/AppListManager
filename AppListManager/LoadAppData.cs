using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Imaging;
using WinRTXamlToolkit.Imaging;

namespace MahdiGhiasi.AppListManager
{
    public class LoadingEventArgs : EventArgs
    {
        private readonly int current = 0;
        private readonly int total = 1;

        // Constructor. 
        public LoadingEventArgs(int current, int total)
        {
            this.current = current;
            this.total = total;
        }

        public int Current
        {
            get { return current; }
        }

        public int Total
        {
            get { return total; }
        }
    }

    public class LoadAppData
    {
        public static ObservableCollection<AppData> appsData { get; set; } = new ObservableCollection<AppData>();
        public static Dictionary<string, AppData> familyNameAppData { get; set; } = new Dictionary<string, AppData>();

        public delegate void LoadingEventHandler(object sender, LoadingEventArgs e);

        public event LoadingEventHandler LoadingProgress;
        public event EventHandler LoadCompleted;

        public bool LoadLegacyAppsToo { get; set; }

        public LoadAppData(bool loadLegacyAppsToo = true)
        {
            LoadLegacyAppsToo = loadLegacyAppsToo;
        }

        protected virtual void OnLoadingProgress(LoadingEventArgs e)
        {
            if (LoadingProgress != null)
                LoadingProgress(this, e);
        }

        protected virtual void OnLoadCompleted()
        {
            if (LoadCompleted != null)
                LoadCompleted(this, new EventArgs());
        }

        public async Task LoadApps()
        {
            Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
            IEnumerable<Windows.ApplicationModel.Package> packages;

            if (LoadLegacyAppsToo)
                packages = (IEnumerable<Windows.ApplicationModel.Package>)packageManager.FindPackagesForUserWithPackageTypes("", PackageTypes.Bundle | PackageTypes.Framework | PackageTypes.Main | PackageTypes.None | PackageTypes.Resource | PackageTypes.Xap);
            else
                packages = (IEnumerable<Windows.ApplicationModel.Package>)packageManager.FindPackagesForUser("");

            int count = packages.Count();
            int progress = 0;


            StorageFolder localCacheFolder = ApplicationData.Current.LocalCacheFolder;

            if ((await localCacheFolder.TryGetItemAsync("Logos")) == null)
                await localCacheFolder.CreateFolderAsync("Logos");

            StorageFolder logosFolder = await localCacheFolder.GetFolderAsync("Logos");

            HashSet<string> existingAppFamilyNames = new HashSet<string>();
            foreach (var item in packages)
            {
                System.Diagnostics.Debug.WriteLine(progress);

                AppData appD = await LoadModernAndLegacyAppData(item, logosFolder);
                if ((appD != null) && (appD.PackageId != ""))
                {
                    appsData.AddSorted(appD, new AppDataNameComparer());
                    familyNameAppData.Add(appD.FamilyName, appD);
                    existingAppFamilyNames.Add(appD.FamilyName);
                }
                else if (appD != null)
                {
                    existingAppFamilyNames.Add(appD.FamilyName);
                }

                progress++;
                OnLoadingProgress(new LoadingEventArgs(progress, count));
            }

            //Remove apps that are no longer installed on device from cache.
            List<AppData> removedApps = new List<AppData>();
            foreach (var item in appsData)
            {
                if (!existingAppFamilyNames.Contains(item.FamilyName))
                    removedApps.Add(item);
            }

            foreach (var item in removedApps)
            {
                familyNameAppData.Remove(item.FamilyName);
                appsData.Remove(item);
            }


            SaveAppList();
            OnLoadCompleted();
        }
        
        private async Task<AppData> LoadModernAndLegacyAppData(Windows.ApplicationModel.Package item, StorageFolder saveLogoLocation)
        {
            AppData data = new AppData();
            try
            {
                data.FamilyName = item.Id.FamilyName;

                if (familyNameAppData.ContainsKey(data.FamilyName))
                {
                    familyNameAppData[data.FamilyName].PackageId = item.Id.FullName; //Refresh package id.

                    data.PackageId = "";
                    return data;
                }

                IReadOnlyList<Windows.ApplicationModel.Core.AppListEntry> x = await item.GetAppListEntriesAsync();

                if ((x == null) || (x.Count == 0))
                    return null;

                data.DisplayName = (x.First().DisplayInfo.DisplayName);

                data.PackageId = item.Id.FullName;
                data.PackageRootFolder = item.InstalledLocation.Path;

                data.IsLegacyApp = data.PackageRootFolder[data.PackageRootFolder.Length - 1] == '}';

                data.PackageDataFolder = await GetDataFolder(data);

                if ((await saveLogoLocation.TryGetItemAsync(data.FamilyName + ".png")) == null)
                {
                    WriteableBitmap bmp = null;
                    try
                    {
                        var stream = await x.First().DisplayInfo.GetLogo(new Size(50, 50)).OpenReadAsync();
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                        bmp = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
                        bmp.SetSource(stream);

                        await bmp.SaveAsync(saveLogoLocation, data.FamilyName + ".png");
                    }
                    catch { }
                }

                data.LogoPath = System.IO.Path.Combine(saveLogoLocation.Path, data.FamilyName + ".png");

                try
                {
                    if (data.IsLegacyApp)
                        data.Publisher = await TryGetPublisherName_Legacy(item);
                    else
                        data.Publisher = await TryGetPublisherName_Modern(item);
                }
                catch { }

                return data;
            }
            catch { }

            return null;
        }

        private static async Task<string> TryGetPublisherName_Legacy(Package item)
        {
            IStorageItem m = await item.InstalledLocation.TryGetItemAsync("WMAppManifest.xml");
            if ((m != null) && (m is StorageFile))
            {
                string publisherName;

                StorageFile manifest = (StorageFile)m;

                string text = await FileIO.ReadTextAsync(manifest);


                string appTag = text.Substring(text.IndexOf("<App "));
                appTag = appTag.Substring(0, appTag.IndexOf(">"));

                publisherName = appTag.Substring(appTag.IndexOf(@"Publisher=""") + @"Publisher=""".Length);
                publisherName = publisherName.Substring(0, publisherName.IndexOf("\""));
                publisherName = GetNameStringFromManifestFormat(publisherName);

                return publisherName;
            }
            return "";
        }

        private static async Task<string> TryGetPublisherName_Modern(Windows.ApplicationModel.Package item)
        {
            var appxManifest = await item.InstalledLocation.TryGetItemAsync("AppxManifest.xml");
            if ((appxManifest != null) && (appxManifest is StorageFile))
            {
                string appxManifestData = await FileIO.ReadTextAsync((StorageFile)appxManifest);

                string publisher = appxManifestData.Substring(appxManifestData.IndexOf("<PublisherDisplayName>") + "<PublisherDisplayName>".Length);
                publisher = publisher.Substring(0, publisher.IndexOf("</PublisherDisplayName>"));

                if ((publisher.Length > "ms-resource:".Length) && (publisher.Substring(0, "ms-resource:".Length) == "ms-resource:"))
                    publisher = "";

                return publisher;
            }

            return "";
        }

        public static async Task<string> GetDataFolder(AppData data)
        {
            string assumedDataPath = "";

            if (data.IsLegacyApp)
            {
                string packageLegacyId = System.IO.Path.GetFileName(data.PackageRootFolder);

                string Cpath = "C:\\Data\\Users\\DefApps\\AppData\\" + packageLegacyId;
                string Dpath = "D:\\WPSystem\\AppData\\" + packageLegacyId;

                try
                {
                    var x = await StorageFolder.GetFolderFromPathAsync(Cpath);
                    assumedDataPath = Cpath;
                }
                catch
                {
                    try
                    {
                        var y = await StorageFolder.GetFolderFromPathAsync(Dpath);
                        assumedDataPath = Dpath;
                    }
                    catch
                    {
                        assumedDataPath = Cpath;
                    }
                }
            }
            else
                assumedDataPath = "C:\\Data\\Users\\DefApps\\APPDATA\\Local\\Packages\\" + data.FamilyName;

            try
            {
                string assumedParent = System.IO.Path.GetDirectoryName(assumedDataPath);
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(assumedParent);
                StorageFolder folder2 = (await folder.TryGetItemAsync(System.IO.Path.GetFileName(assumedDataPath))) as StorageFolder;

                if ((folder2 == null) || ((await folder2.GetItemsAsync()).Count == 0))
                    assumedDataPath = "";
            }
            catch
            {
                assumedDataPath = "";
            }

            return assumedDataPath;
        }

        public static async Task DeleteAppListCache()
        {
            StorageFolder localCacheFolder = ApplicationData.Current.LocalCacheFolder;
            var cacheFile = await localCacheFolder.TryGetItemAsync("applistcache.txt");
            if ((cacheFile != null) && (cacheFile is StorageFile))
            {
                await (cacheFile as StorageFile).DeleteAsync();
            }
        }

        public static async Task SaveAppList()
        {
            string serializedData = Newtonsoft.Json.JsonConvert.SerializeObject(appsData, Newtonsoft.Json.Formatting.Indented);

            StorageFolder localCacheFolder = ApplicationData.Current.LocalCacheFolder;
            StorageFile cacheFile = await localCacheFolder.CreateFileAsync("applistcache.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(cacheFile, serializedData);
        }

        public static async Task<bool> LoadCachedAppList()
        {
            appsData = new ObservableCollection<AppData>(await GetCachedAppList());
            familyNameAppData.Clear();
            foreach (var item in appsData)
            {
                familyNameAppData.Add(item.FamilyName, item);
            };

            return appsData.Count != 0;
        }

        private static async Task<List<AppData>> GetCachedAppList()
        {
            StorageFolder localCacheFolder = ApplicationData.Current.LocalCacheFolder;
            var file = await localCacheFolder.TryGetItemAsync("applistcache.txt");
            if ((file != null) && (file is StorageFile))
            {
                StorageFile cacheFile = file as StorageFile;
                string data = await FileIO.ReadTextAsync(cacheFile);

                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<AppData>>(data);
            }

            return new List<AppData>();
        }

        static string GetNameStringFromManifestFormat(string inputS/*, StorageFolder curPath*/)
        {
            if (inputS.Length < 1)
                return inputS;
            else if (inputS[0] != '@')
                return inputS;
            else
                return "Unknown";
        }
    }
}
