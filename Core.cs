using MelonLoader;
using UnityEngine.Events;
using UnicornsCustomSeeds.Seeds;
using Newtonsoft.Json;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Patches;
using UnicornsCustomSeeds.TemplateUtils;



#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.Persistence;
using ScheduleOne.ObjectScripts;
#endif

[assembly: MelonInfo(typeof(UnicornsCustomSeeds.Core), UnicornsCustomSeeds.BuildInfo.Name, UnicornsCustomSeeds.BuildInfo.Version, UnicornsCustomSeeds.BuildInfo.Author, UnicornsCustomSeeds.BuildInfo.DownloadLink)]
[assembly: MelonColor(255, 191, 0, 255)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UnicornsCustomSeeds
{
    public static class BuildInfo
    {
        public const string Name = "Unicorns Custom Seeds";
        public const string Description = "Your good buddy Unicorn can help you synthesize seeds";
        public const string Author = "OverweightUnicorn";
        public const string Company = "UnicornsCanMod";
        public const string Version = "1.1.2";
        public const string DownloadLink = null;
    }

    public class Core : MelonMod
    {

        public override void OnInitializeMelon()
        {
            AssetBundleUtils.Initialize(this);
        }

        public override void OnLateInitializeMelon()
        {
            StashManager.InitializeConfig();
            SeedVisualsManager.LoadSeedMaterial();
            LoadManager.Instance.onLoadComplete.AddListener((UnityAction)InitMod);
            SaveManager.Instance.onSaveComplete.AddListener((UnityAction)SaveData);
        }

        public void SaveData()
        {
            try
            {
                string saveFolder = Singleton<LoadManager>.Instance.LoadedGameFolderPath;
                if (string.IsNullOrEmpty(saveFolder) || !Directory.Exists(saveFolder))
                    return;

                // ── DiscoveredCustomSeeds.json ────────────────────────────────────
                {
                    var all = new List<UnicornSeedData>();
                    all.AddRange(CustomSeedsManager.DiscoveredSeeds.Values);
                    all.AddRange(CustomShroomsManager.DiscoveredShrooms.Values);
                    all.AddRange(CustomCocaSeedsManager.DiscoveredCocaSeeds.Values);

                    string json = JsonConvert.SerializeObject(all, Formatting.Indented);
                    File.WriteAllText(Path.Combine(saveFolder, "DiscoveredCustomSeeds.json"), json);
                }

                // ── UnicornsActiveCooking.json ────────────────────────────────────
                {
                    var entries = new List<UnicornsCustomSeeds.Managers.ActiveCookingEntry>();
                    foreach (var kvp in UnicornsCustomSeeds.Managers.ActiveCookingRegistry.GuidToMixId)
                        entries.Add(new UnicornsCustomSeeds.Managers.ActiveCookingEntry { stationGuid = kvp.Key, mixId = kvp.Value });

                    string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
                    File.WriteAllText(Path.Combine(saveFolder, "UnicornsActiveCooking.json"), json);
                }
            }
            catch (Exception e) { Utility.PrintException(e); }
        }

        public void InitMod()
        {
            CustomSeedsManager.Initialize();
            CustomShroomsManager.Initialize();
            CustomCocaSeedsManager.Initialize();
            StashManager.GetAlbertsStash();

            if (CustomSeedsManager.letsMigrate)
            {
                bool hasData = CustomSeedsManager.DiscoveredSeeds.Count > 0
                            || CustomShroomsManager.DiscoveredShrooms.Count > 0
                            || CustomCocaSeedsManager.DiscoveredCocaSeeds.Count > 0;

                if (hasData)
                    SaveData();
                else
                    Utility.Error("Core: migration was flagged but nothing loaded — skipping the save so the existing file isn't overwritten with an empty list.");
                Utility.Success($"Successfully migrated {CustomSeedsManager.DiscoveredSeeds.Count} seed(s)");
                CustomSeedsManager.letsMigrate = false;
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            var baseSeed = Registry.GetItem<SeedDefinition>("ogkushseed");
            if (CustomSeedsManager.factory == null && baseSeed != null)
            {
                CustomSeedsManager.factory = new SeedFactory(baseSeed);
            }

            var baseSyringe = Registry.GetItem<SporeSyringeDefinition>(CustomShroomsManager.BASE_SYRINGE_ID);
            if (CustomShroomsManager.factory == null && baseSyringe != null)
            {
                CustomShroomsManager.factory = new SyringeFactory(baseSyringe);
            }

            var temp = Singleton<Registry>.Instance.ItemDictionary;

            // CocaFactory — requires three base definitions to be present in the Registry
            var baseCocaSeed = Registry.GetItem<SeedDefinition>(CustomCocaSeedsManager.BASE_SEED_ID);
            var baseCocaLeaf = Registry.GetItem<QualityItemDefinition>(CustomCocaSeedsManager.BASE_LEAF_ID);
            var baseCocaBase = Registry.GetItem<QualityItemDefinition>(CustomCocaSeedsManager.BASE_BASE_ID);
            if (CustomCocaSeedsManager.factory == null && baseCocaSeed != null && baseCocaLeaf != null && baseCocaBase != null)
            {
                CustomCocaSeedsManager.factory = new CocaFactory(baseCocaSeed, baseCocaLeaf, baseCocaBase);
            }
            else if (CustomCocaSeedsManager.factory == null)
            {
                Utility.Error($"Core: CocaFactory init failed — cocaseed={baseCocaSeed != null}, cocaleaf={baseCocaLeaf != null}, cocainebase={baseCocaBase != null}");
            }

            StashManager.GetAlbertsStash();

            // When returning to the main scene clear all data structures to prevent overlap with other saves
            if (sceneName.ToLower() != "main")
            {
                CustomSeedsManager.ClearAll();
                CustomShroomsManager.ClearAll();
                CustomCocaSeedsManager.ClearAll();
                UnicornsCustomSeeds.Managers.ActiveCookingRegistry.Clear();
                ProductManagerAppPatches.ClearPendingIndicators();
                StashManager.ClearCaches();
            }
            else
            {
                // Reload assets when entering main scene to prevent garbage collection issues
                if (SeedVisualsManager.seedIcon == null || SeedVisualsManager.baseSeedSprite == null)
                {
                    SeedVisualsManager.LoadSeedMaterial();
                }
            }
        }
    }
}