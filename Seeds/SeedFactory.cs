using Harmony;
using Il2Cpp;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Storage;
using MelonLoader;
using MelonLoader.Utils;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

using UnityEngine.Rendering;
using UnicornsCustomSeeds.Managers;

namespace UnicornsCustomSeeds.Seeds
{

    public class SeedFactory
    {
        private Transform rootGameObject;
        private WeedPlant basePlantPrefab;
        private PlantHarvestable baseBranchPrefab;
        private FunctionalSeed baseFunctionalSeedPrefab;
        private Equippable_Seed baseEquippableSeedPrefab;
        private AvatarEquippable baseAvatarEquippablePrefab;
        private StoredItem baseStorableItem;
        private SeedDefinition baseSeedDefinition;

        public SeedFactory(SeedDefinition seedDefinition)
        {
            baseSeedDefinition = seedDefinition;
            baseFunctionalSeedPrefab = seedDefinition.FunctionSeedPrefab;
            baseStorableItem = seedDefinition.StoredItem;
            InitializeBasePlant(seedDefinition);
            InitializeEquippable(seedDefinition);
            if (rootGameObject == null)
            {
                GameObject go = new GameObject($"{baseSeedDefinition.ID}_CustomSeeds");
                go.SetActive(false);
                rootGameObject = go.transform;
            }
        }

        private void InitializeBasePlant(SeedDefinition seedDefinition)
        {
            if (seedDefinition.PlantPrefab.TryCast<WeedPlant>() is WeedPlant weedPlantPrefab)
            {
                basePlantPrefab = weedPlantPrefab;
                if (basePlantPrefab != null)
                    baseBranchPrefab = basePlantPrefab.BranchPrefab;
            }
            else
            {
                Utility.Log("Failed to cast PlantPrefab to WeedPlant.");
            }
        }

        private void InitializeEquippable(SeedDefinition seedDefinition)
        {
            if (seedDefinition.Equippable.TryCast<Equippable_Seed>() is Equippable_Seed equipSeed)
            {
                baseEquippableSeedPrefab = equipSeed;
                baseAvatarEquippablePrefab = equipSeed.AvatarEquippable;
            }
            else
            {
                Utility.Log("Failed to cast Equippable to Equippable_Seed.");
            }
        }

        private void GrowLabel(Transform root, string seedDefId)
        {
            Transform labelTransform = null;
            var items = root.GetComponentsInChildren<Transform>();
            foreach (var item in items)
            {
                //Utility.Log(item.name);
                if (item.name == "Label")
                {
                    labelTransform = item.transform;
                }
            }
            if (labelTransform != null)
            {
                labelTransform.localScale = new Vector3(1.05f, 1.05f, 2);
                labelTransform.position = new Vector3(0, -0.05f, 0);
                if(SeedVisualsManager.customMat == null)
                {
                    SeedVisualsManager.LoadSeedMaterial();
                }

                if (SeedVisualsManager.customMat != null)
                {
                    var rend = labelTransform.GetComponent<Renderer>();
                    // Create an instance of the material instead of using shared
                    rend.material = SeedVisualsManager.customMat;
                    labelTransform.gameObject.AddComponent<SeedVialLabel>();
                    labelTransform.name = labelTransform.name + ":" + seedDefId;
                    //var meshFilter = labelTransform.GetComponent<MeshFilter>();
                    //if (meshFilter != null)
                    //{
                    //    var localBounds = meshFilter.sharedMesh.bounds;

                    //    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    //    block.SetColor("_ColorA", UnityEngine.Random.ColorHSV());
                    //    block.SetColor("_ColorB", UnityEngine.Random.ColorHSV());
                    //    block.SetFloat("_MinZ", localBounds.min.z);
                    //    block.SetFloat("_MaxZ", localBounds.max.z);

                    //    rend.SetPropertyBlock(block);
                    //}
                } else
                {
                    Utility.Log("MATERIAL NOT LOADED!!!");
                }
            }
        }
        public WeedPlant ClonePlantPrefab(WeedDefinition newDef)
        {
            if (basePlantPrefab == null) throw new InvalidOperationException("Base plant prefab not initialized.");
            WeedPlant newPlant = UnityEngine.Object.Instantiate(basePlantPrefab, rootGameObject);
            SetPlantAppearance(newDef, newPlant);
            PlantHarvestable newHarvestable = CloneBranchPrefab(newDef);
            SetBranchAppearance(newHarvestable.transform, newDef);
            newPlant.BranchPrefab = newHarvestable;
            var allBranches = newPlant.GetComponentsInChildren<PlantHarvestable>();
            foreach (PlantHarvestable pH in allBranches)
            {
                pH.Product = newDef;
            }
            return newPlant;
        }

        public void SetPlantAppearance(WeedDefinition newDef, WeedPlant plant)
        {
            foreach (PlantGrowthStage stage in plant.GrowthStages)
            {
                // Print weed appearance settings
                WeedAppearanceSettings weedAppearance = newDef.appearance;
                if (weedAppearance == null)
                {
                    weedAppearance = WeedDefinition.GetAppearanceSettings(newDef.Properties);
                }
                /*
                [Custom Weed Seeds] [Appearance] Main Color = RGBA(109, 146, 81, 255) #6d9251
                [Custom Weed Seeds] [Appearance] Secondary Color = RGBA(153, 150, 78, 255) #99964e
                [Custom Weed Seeds] [Appearance] Stem Color = RGBA(87, 116, 64, 255) #577440
                [Custom Weed Seeds] [Appearance] Leaf Color = RGBA(131, 148, 79, 255)  #83944f
                 */
                //MelonLogger.Msg($"[Appearance] Main Color = {weedAppearance.MainColor}");
                //MelonLogger.Msg($"[Appearance] Secondary Color = {weedAppearance.SecondaryColor}");
                //MelonLogger.Msg($"[Appearance] Stem Color = {weedAppearance.StemColor}");
                //MelonLogger.Msg($"[Appearance] Leaf Color = {weedAppearance.LeafColor}");
                if (stage != null && weedAppearance != null)
                {
                    for (int i = 0; i < stage.transform.childCount; i++)
                    {
                        Transform trans = stage.transform.GetChild(i);
                        if (trans == null) continue;

                        MeshRenderer meshRenderer = trans.GetComponent<MeshRenderer>();
                        if (meshRenderer == null) continue;

                        Material material = meshRenderer.material;
                        if (material == null) continue;

                        if (trans.name == "Stem")
                        {
                            material.color = weedAppearance.StemColor;
                        }

                        if (trans.name == "BigLeaves")
                        {
                            material.color = weedAppearance.LeafColor;
                        }

                        if (trans.name == "SmallLeaves")
                        {
                            material.color = weedAppearance.LeafColor;
                        }

                        foreach (Transform site in stage.GrowthSites)
                        {
                            if (site != null)
                            {
                                Transform harvestableTransform = site.GetChild(0);
                                if (harvestableTransform)
                                {
                                    SetBranchAppearance(harvestableTransform, weedAppearance);
                                }

                            }
                        }
                    }
                }
                else
                {
                    Utility.Error("Plant Stage is NULL! Thats impossible");
                }
            }
        }

        public void SetBranchAppearance(Transform branchTransform, WeedDefinition weedDef)
        {
            WeedAppearanceSettings weedAppearance = weedDef.appearance;
            if (weedAppearance == null)
            {
                weedAppearance = WeedDefinition.GetAppearanceSettings(weedDef.Properties);
            }

            if (weedAppearance != null && branchTransform != null)
            {
                SetBranchAppearance(branchTransform, weedAppearance);
            }
        }

        public void SetBranchAppearance(Transform branchTransform, WeedAppearanceSettings weedAppearance)
        {
            Transform branch = branchTransform.GetChild(0)?.GetChild(0);
            if (branch == null) return;
            for (int j = 0; j < branch.childCount; j++)
            {
                Transform budSpot = branch.GetChild(j);
                if (budSpot == null) continue;

                MeshRenderer meshRenderer = budSpot.GetComponent<MeshRenderer>();
                if (meshRenderer == null) continue;

                Material material = meshRenderer.material;
                if (material == null) continue;

                if (budSpot.name == "Stem")
                {
                    material.color = weedAppearance.StemColor;
                }

                if (budSpot.name == "Leaves")
                {
                    material.color = weedAppearance.LeafColor;
                }

                if (budSpot.name == "Main")
                {
                    material.color = weedAppearance.MainColor;
                }

                if (budSpot.name == "Secondary")
                {
                    material.color = weedAppearance.SecondaryColor;
                }
            }
        }

        public PlantHarvestable CloneBranchPrefab(WeedDefinition newDef)
        {
            if (baseBranchPrefab == null) throw new InvalidOperationException("Base branch prefab not initialized.");
            PlantHarvestable cloneHarvestable = UnityEngine.Object.Instantiate(baseBranchPrefab, rootGameObject);
            cloneHarvestable.gameObject.name = $"{newDef.ID}_Harvestable";
            cloneHarvestable.gameObject.layer = baseBranchPrefab.gameObject.layer;
            cloneHarvestable.Product = newDef;
            return cloneHarvestable;
        }

        public FunctionalSeed CloneFunctionalSeedPrefab(string seedDefId)
        {
            if (baseFunctionalSeedPrefab == null) throw new InvalidOperationException("Base functional seed prefab not initialized.");
            FunctionalSeed newSeed = UnityEngine.Object.Instantiate(baseFunctionalSeedPrefab, rootGameObject);
            GrowLabel(newSeed.transform, seedDefId);
            return newSeed;
        }

        // Equippable_Seed Attaches to SeedDefinition with attribute equippable
        public Equippable_Seed CloneEquippableSeedPrefab(SeedDefinition newDef, WeedAppearanceSettings weedAppearance)
        {
            if (baseEquippableSeedPrefab == null) throw new InvalidOperationException("Base equippable seed prefab not initialized.");
            Equippable_Seed newEquipSeed = UnityEngine.Object.Instantiate(baseEquippableSeedPrefab, rootGameObject);
            GrowLabel(newEquipSeed.transform, newDef.ID); // Pass the ID
            newEquipSeed.gameObject.name = $"{newDef.ID}_Equippable";
            newEquipSeed.gameObject.layer = baseEquippableSeedPrefab.gameObject.layer;
            newEquipSeed.Seed = newDef;
            newEquipSeed.AvatarEquippable = CloneAvatarEquippablePrefab(newDef.ID);
            newEquipSeed.AvatarEquippable.name = $"{newDef.ID}_AvatarEquippable";
            return newEquipSeed;
        }

        // AvatarEquippable attaches to Equippable_Seed
        public AvatarEquippable CloneAvatarEquippablePrefab(string newDefId)
        {
            if (baseAvatarEquippablePrefab == null) throw new InvalidOperationException("Base avatar equippable prefab not initialized.");
            AvatarEquippable newAvatarEquip = UnityEngine.Object.Instantiate(baseAvatarEquippablePrefab, rootGameObject);
            GrowLabel (newAvatarEquip.transform, newDefId);
            return newAvatarEquip;
        }

        public StoredItem CloneStoredItem(string newDefId)
        {
            if (baseStorableItem == null) throw new InvalidOperationException("Base Stored Item not initialized.");
            StoredItem newStoredItem = UnityEngine.Object.Instantiate(baseStorableItem, rootGameObject);
            GrowLabel(newStoredItem.transform, newDefId);
            return newStoredItem;
        }

        public SeedDefinition CreateSeedDefinition(WeedDefinition weedDef)
        {
            if (baseSeedDefinition == null) throw new InvalidOperationException("Base seed definition not initialized.");

            // Clone the SeedDefinition ScriptableObject
            SeedDefinition newSeedDef = UnityEngine.Object.Instantiate(baseSeedDefinition);

            WeedAppearanceSettings weedAppearance = weedDef.appearance;
            if (weedAppearance == null)
            {
                weedAppearance = WeedDefinition.GetAppearanceSettings(weedDef.Properties);
            }

            newSeedDef.name = $"{weedDef.ID}_customseeddefinition";
            newSeedDef.ID = $"{weedDef.ID}_customseeddefinition";
            newSeedDef.Name = $"{weedDef.name} Seed";
            newSeedDef.Description = $"{weedDef.name} Seed";
            newSeedDef.Icon = baseSeedDefinition.Icon;

            newSeedDef.Equippable = CloneEquippableSeedPrefab(newSeedDef,weedAppearance);
            newSeedDef.PlantPrefab = ClonePlantPrefab(weedDef);
            newSeedDef.PlantPrefab.SeedDefinition = newSeedDef;
            newSeedDef.FunctionSeedPrefab = CloneFunctionalSeedPrefab(newSeedDef.ID);
            newSeedDef.FunctionSeedPrefab.name = $"{weedDef.ID}Seed_Functional";
            newSeedDef.StoredItem = CloneStoredItem(newSeedDef.ID);

            SeedVisualsManager.appearanceMap.Add(newSeedDef.ID, weedAppearance);

            if (SeedVisualsManager.baseSeedSprite != null) {
                try
                {
                    Sprite newIcon = SeedVisualsManager.GenerateSpriteWithGradient(weedAppearance.MainColor, weedAppearance.SecondaryColor);
                    newIcon.name = newSeedDef.name + "_icon";
                    SeedVisualsManager.seedIcons.Add(newSeedDef.ID, newIcon);
                    newSeedDef.Icon = newIcon;
                }
                catch (Exception e) {
                    newSeedDef.Icon = SeedVisualsManager.baseSeedSprite;
                    Utility.PrintException(e);
                }

            }

            return newSeedDef;
        }
    }
}