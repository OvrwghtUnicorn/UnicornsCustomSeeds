using System;
using System.Collections.Generic;
using UnityEngine;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.Storage;
using Il2CppFluffyUnderware.DevTools.Extensions;
#elif MONO
using ScheduleOne;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.DevUtilities;
using ScheduleOne.Equipping;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Product;
using ScheduleOne.StationFramework;
using ScheduleOne.Storage;
using FluffyUnderware.DevTools.Extensions;
#endif

namespace UnicornsCustomSeeds.Seeds
{
    public class CocaFactory
    {
        // Static registries for type-safe identification in patches.
        // Key: cloned QualityItemDefinition ID
        // Value for leaves: the linked base definition ID
        // Value for bases: the target cocaine mix ID
        public static readonly Dictionary<string, string> CustomLeafIdToBaseId
            = new Dictionary<string, string>();
        public static readonly Dictionary<string, string> CustomBaseIdToMixId
      = new Dictionary<string, string>();

        private Transform rootGameObject;
        private SeedDefinition baseSeedDefinition;
        private CocaPlant baseCocaPlantPrefab;
        private PlantHarvestable baseHarvestable;
        private FunctionalSeed baseFunctionalSeedPrefab;
        private Equippable_Seed baseEquippableSeedPrefab;
        private AvatarEquippable baseAvatarEquippablePrefab;
        private StoredItem baseStoredItem;

        private QualityItemDefinition baseCocaLeafDefinition;
        private QualityItemDefinition baseCocaineBaseDefinition;

        public CocaFactory(SeedDefinition baseSeed, QualityItemDefinition baseCocaLeaf, QualityItemDefinition baseCocaineBase)
        {
            baseSeedDefinition = baseSeed;
            baseCocaLeafDefinition = baseCocaLeaf;
            baseCocaineBaseDefinition = baseCocaineBase;

#if IL2CPP
            baseCocaPlantPrefab = baseSeed.PlantPrefab.TryCast<CocaPlant>();
#elif MONO
          baseCocaPlantPrefab = (CocaPlant)baseSeed.PlantPrefab;
#endif
            if (baseCocaPlantPrefab == null)
                Utility.Error("CocaFactory: Failed to cast PlantPrefab to CocaPlant.");

            baseHarvestable = baseCocaPlantPrefab?.Harvestable;
            baseFunctionalSeedPrefab = baseSeed.FunctionSeedPrefab;
            baseStoredItem = baseSeed.StoredItem;

#if IL2CPP
            baseEquippableSeedPrefab = baseSeed.Equippable.TryCast<Equippable_Seed>();
#elif MONO
    baseEquippableSeedPrefab = (Equippable_Seed)baseSeed.Equippable;
#endif
            if (baseEquippableSeedPrefab == null)
                Utility.Error("CocaFactory: Failed to cast Equippable to Equippable_Seed.");

            baseAvatarEquippablePrefab = baseEquippableSeedPrefab?.AvatarEquippable;

            GameObject go = new GameObject($"{baseSeedDefinition.ID}_CustomCocaSeeds");
            go.SetActive(false);
            GameObject.DontDestroyOnLoad(go);
            rootGameObject = go.transform;
        }

        public void DeleteChildren()
        {
            rootGameObject.DeleteChildren(true, false);
            CustomLeafIdToBaseId.Clear();
            CustomBaseIdToMixId.Clear();
        }

        public SeedDefinition CreateCocaSeedDefinition(ProductDefinition cocaineDef)
        {
            if (baseSeedDefinition == null) throw new InvalidOperationException("Base seed definition not initialized.");

            // Step 1: Clone cocaine base — Object.Instantiate works for ScriptableObjects under IL2CPP
            QualityItemDefinition customBase = CloneCustomCocaineBase(cocaineDef);

            // Step 2: Clone coca leaf, link to base
            QualityItemDefinition customLeaf = CloneCustomCocaLeaf(customBase, cocaineDef);

            // Step 3: Clone plant, wire harvestable product to custom leaf
            CocaPlant newPlant = CloneCocaPlant(customLeaf);

            // Step 4: Clone seed definition ScriptableObject
            SeedDefinition newSeed = UnityEngine.Object.Instantiate(baseSeedDefinition);
            newSeed.ID = $"{cocaineDef.ID}_customcocaseed";
            newSeed.name = newSeed.ID;
            newSeed.Name = $"{cocaineDef.name} Coca Seed";
            newSeed.Description = $"A custom coca seed that produces {cocaineDef.name}.";
            newSeed.Icon = baseSeedDefinition.Icon;

            newSeed.PlantPrefab = newPlant;
            newSeed.PlantPrefab.SeedDefinition = newSeed;
            newSeed.FunctionSeedPrefab = CloneFunctionalSeedPrefab(newSeed.ID);
            newSeed.FunctionSeedPrefab.name = $"{cocaineDef.ID}CocaSeed_Functional";
            newSeed.Equippable = CloneEquippableSeedPrefab(newSeed);
            newSeed.StoredItem = CloneStoredItem(newSeed.ID);

            // Register intermediates in the Registry so patches can resolve them by ID
            Singleton<Registry>.Instance.AddToRegistry(customLeaf);
            Singleton<Registry>.Instance.AddToRegistry(customBase);

            Utility.Log($"CocaFactory: {newSeed.ID} ? leaf:{customLeaf.ID} ? base:{customBase.ID} ? mix:{cocaineDef.ID}");
            return newSeed;
        }

        /// <summary>
        /// Clones baseCocaineBaseDefinition and patches its StationItem's CookableModule.Product
        /// to point at cocaineDef. This makes the LabOven output the right mix with zero oven patching.
        /// Also registers this base's ID in CustomBaseIdToMixId for CauldronPatches.
        /// </summary>
        private QualityItemDefinition CloneCustomCocaineBase(ProductDefinition cocaineDef)
        {
            QualityItemDefinition clone = UnityEngine.Object.Instantiate(baseCocaineBaseDefinition);
            clone.ID = $"{cocaineDef.ID}_customcocainebase";
            clone.name = clone.ID;
            clone.Name = $"{cocaineDef.name} Base";

            // Clone the StationItem so CookableModule.Product can be set per-mix
            // without affecting the vanilla cocainebase used by other cauldrons
            if (baseCocaineBaseDefinition.StationItem != null)
            {
                StationItem clonedStationItem = UnityEngine.Object.Instantiate(
         baseCocaineBaseDefinition.StationItem, rootGameObject);
                clonedStationItem.name = $"{cocaineDef.ID}_CocaineBaseStationItem";

                CookableModule cookable = clonedStationItem.GetModule<CookableModule>();
                if (cookable != null)
                {
                    cookable.Product = cocaineDef;
                    Utility.Log($"CocaFactory: Set CookableModule.Product = {cocaineDef.ID}");
                }
                else
                {
                    Utility.Error($"CocaFactory: No CookableModule on StationItem for {cocaineDef.ID}. LabOven will output vanilla product.");
                }
                clone.StationItem = clonedStationItem;
            }
            else
            {
                Utility.Error("CocaFactory: baseCocaineBaseDefinition.StationItem is null.");
            }

            // Register for CauldronPatches: base ID ? target mix ID
            CustomBaseIdToMixId[clone.ID] = cocaineDef.ID;
            return clone;
        }

        /// <summary>
        /// Clones baseCocaLeafDefinition. The StationItem is shared (not cloned) because
        /// CookableModule on the leaf drives Cauldron cook time, not output — output is
        /// intercepted by CauldronPatches via CustomLeafIdToBaseId.
        /// </summary>
        private QualityItemDefinition CloneCustomCocaLeaf(QualityItemDefinition customBase, ProductDefinition cocaineDef)
        {
            QualityItemDefinition clone = UnityEngine.Object.Instantiate(baseCocaLeafDefinition);
            clone.ID = $"{cocaineDef.ID}_customcocaleaf";
            clone.name = clone.ID;
            clone.Name = $"Coca Leaf ({cocaineDef.name})";
            // StationItem shared — controls Cauldron cook time; output overridden in patch
            clone.StationItem = baseCocaLeafDefinition.StationItem;

            // Register for CauldronPatches: leaf ID ? base ID
            CustomLeafIdToBaseId[clone.ID] = customBase.ID;
            return clone;
        }

        private CocaPlant CloneCocaPlant(QualityItemDefinition customLeaf)
        {
            CocaPlant newPlant = UnityEngine.Object.Instantiate(baseCocaPlantPrefab, rootGameObject);
            newPlant.gameObject.name = $"{customLeaf.ID}_CocaPlant";

            PlantHarvestable newHarvestable = UnityEngine.Object.Instantiate(baseHarvestable, rootGameObject);
            newHarvestable.gameObject.name = $"{customLeaf.ID}_Harvestable";
            newHarvestable.Product = customLeaf;
            newPlant.Harvestable = newHarvestable;

            foreach (PlantHarvestable pH in newPlant.GetComponentsInChildren<PlantHarvestable>())
                pH.Product = customLeaf;

            return newPlant;
        }

        private FunctionalSeed CloneFunctionalSeedPrefab(string seedDefId)
        {
            if (baseFunctionalSeedPrefab == null) throw new InvalidOperationException("Base functional seed prefab not initialized.");
            FunctionalSeed newSeed = UnityEngine.Object.Instantiate(baseFunctionalSeedPrefab, rootGameObject);
            newSeed.gameObject.name = $"{seedDefId}_Functional";
            return newSeed;
        }

        private Equippable_Seed CloneEquippableSeedPrefab(SeedDefinition newDef)
        {
            if (baseEquippableSeedPrefab == null) throw new InvalidOperationException("Base equippable seed prefab not initialized.");
            Equippable_Seed newEquipSeed = UnityEngine.Object.Instantiate(baseEquippableSeedPrefab, rootGameObject);
            newEquipSeed.gameObject.name = $"{newDef.ID}_Equippable";
            newEquipSeed.gameObject.layer = baseEquippableSeedPrefab.gameObject.layer;
            newEquipSeed.Seed = newDef;
            newEquipSeed.AvatarEquippable = CloneAvatarEquippablePrefab(newDef.ID);
            newEquipSeed.AvatarEquippable.name = $"{newDef.ID}_AvatarEquippable";
            return newEquipSeed;
        }

        private AvatarEquippable CloneAvatarEquippablePrefab(string newDefId)
        {
            if (baseAvatarEquippablePrefab == null) throw new InvalidOperationException("Base avatar equippable prefab not initialized.");
            AvatarEquippable newAvatarEquip = UnityEngine.Object.Instantiate(baseAvatarEquippablePrefab, rootGameObject);
            newAvatarEquip.gameObject.name = $"{newDefId}_AvatarEquippable";
            return newAvatarEquip;
        }

        private StoredItem CloneStoredItem(string newDefId)
        {
            if (baseStoredItem == null) throw new InvalidOperationException("Base stored item not initialized.");
            StoredItem newStoredItem = UnityEngine.Object.Instantiate(baseStoredItem, rootGameObject);
            newStoredItem.gameObject.name = $"{newDefId}_StoredItem";
            return newStoredItem;
        }

        /// <summary>
        /// Adds a custom leaf ID to the Cauldron's IngredientSlot ItemFilter_ID whitelists.
        /// Call this after the leaf has been registered in the Registry.
        /// Mirrors CustomShroomsManager.AddSyringeToSpawnStations.
        /// </summary>
        public static void AddLeafToCauldrons(QualityItemDefinition customLeaf)
        {
            Cauldron[] cauldrons = GameObject.FindObjectsOfType<Cauldron>();
            int patched = 0;
            foreach (Cauldron cauldron in cauldrons)
            {
                foreach (ItemSlot slot in cauldron.IngredientSlots)
                {
                    foreach (ItemFilter filter in slot.HardFilters)
                    {
#if IL2CPP
                        ItemFilter_ID idFilter = filter.TryCast<ItemFilter_ID>();
#elif MONO
         ItemFilter_ID idFilter = filter as ItemFilter_ID;
#endif
                        if (idFilter != null && !idFilter.IDs.Contains(customLeaf.ID))
                        {
                            idFilter.IDs.Add(customLeaf.ID);
                            patched++;
                        }
                    }
                }
            }
            Utility.Log($"CocaFactory: Added '{customLeaf.ID}' to {patched} cauldron slot filter(s).");
        }
    }
}
