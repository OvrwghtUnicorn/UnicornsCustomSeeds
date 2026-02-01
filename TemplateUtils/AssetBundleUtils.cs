using MelonLoader;
using UnityEngine;
#if IL2CPP
using AssetBundle = UnityEngine.Il2CppAssetBundle;
#elif MONO
using AssetBundle = UnityEngine.AssetBundle;
#endif

namespace UnicornsCustomSeeds.TemplateUtils
{
    public static class AssetBundleUtils
    {
        static Core mod = MelonAssembly.FindMelonInstance<Core>();
        static MelonAssembly melonAssembly = mod.MelonAssembly;
        static Dictionary<string, AssetBundle> assetBundles = new Dictionary<string, AssetBundle>();

        public static AssetBundle LoadAssetBundle(string bundleFileName)
        {
            if (assetBundles.ContainsKey(bundleFileName)) { return assetBundles[bundleFileName]; }
            try
            {
                string streamPath = $"{typeof(Core).Namespace}.Assets.{bundleFileName}";
                Stream bundleStream = melonAssembly.Assembly.GetManifestResourceStream($"{streamPath}");
                if (bundleStream == null)
                {
                    mod.Unregister($"AssetBundle: '{streamPath}' not found. \nOpen .csproj file and search for '{bundleFileName}'.\nIf it doesn't exist,\nCopy your asset to Assets/ folder then look for 'your.assetbundle' in .csproj file.");
                    return null;
                }
#if IL2CPP
                byte[] bundleData;
                using (MemoryStream ms = new MemoryStream())
                {
                    bundleStream.CopyTo(ms);
                    bundleData = ms.ToArray();
                }
                Il2CppSystem.IO.Stream stream = new Il2CppSystem.IO.MemoryStream(bundleData);
                AssetBundle ab = Il2CppAssetBundleManager.LoadFromStream(stream);
#elif MONO
                AssetBundle ab = AssetBundle.LoadFromStream(bundleStream);
#endif
                assetBundles.Add(bundleFileName, ab);
                return ab;
            }
            catch (Exception e)
            {
                mod.Unregister($"Failed to load AssetBundle. Please report to dev: {e}");
                return null;
            }
        }

        public static AssetBundle GetLoadedAssetBundle(string bundleName)
        {
            if (assetBundles.ContainsKey(bundleName))
            {
                return assetBundles[bundleName];
            }
            else
            {
                mod.Unregister($"Failed to get {bundleName}");
                throw new Exception($"Asset '{bundleName}' has not been loaded in yet");
            }
        }

        public static T LoadAssetFromBundle<T>(string assetName, string bundleName) where T : UnityEngine.Object
        {
            var bundle = GetLoadedAssetBundle(bundleName);
            if (bundle == null)
            {
                throw new Exception($"Bundle not found for asset: {assetName}");
            }

            var asset = bundle.LoadAsset<T>(assetName);
            if (bundle == null)
            {
                throw new Exception($"{assetName} not found in bundle {bundleName}");
            }

            return asset;
        }
    }
}
