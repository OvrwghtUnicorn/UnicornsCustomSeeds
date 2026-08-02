using System;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Stations;
using Il2CppFluffyUnderware.DevTools.Extensions;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Product;
using ScheduleOne.StationFramework;
using ScheduleOne.UI.Stations;
using FluffyUnderware.DevTools.Extensions;
#endif

namespace UnicornsCustomSeeds.Seeds
{
    public class PseudoFactory
    {
        // Key: custom pseudo ID → custom liquid meth ID.
        // Retained for any future patch use.
        public static readonly Dictionary<string, string> CustomPseudoIdToLiquidMethId
            = new Dictionary<string, string>();

        private readonly Dictionary<string, QualityItemDefinition> basePseudoDefinitions;
        private readonly LiquidMethDefinition baseLiquidMethDefinition;
        private readonly StationRecipe baseStationRecipe;
        private readonly Transform rootGameObject;

        public PseudoFactory(
            QualityItemDefinition basePseudo,
            QualityItemDefinition lowPseudo,
            QualityItemDefinition highPseudo,
            LiquidMethDefinition baseLiquidMeth,
            StationRecipe baseRecipe)
        {
            basePseudoDefinitions = new Dictionary<string, QualityItemDefinition>
            {
                [CustomPseudoManager.PSEUDO_BASE_ID] = basePseudo,
                [CustomPseudoManager.PSEUDO_LO_ID] = lowPseudo,
                [CustomPseudoManager.PSEUDO_HI_ID] = highPseudo,
            };
            baseLiquidMethDefinition = baseLiquidMeth;
            baseStationRecipe = baseRecipe;

            GameObject go = new GameObject($"{basePseudo.ID}_CustomPseudoChains");
            go.SetActive(false);
            GameObject.DontDestroyOnLoad(go);
            rootGameObject = go.transform;
        }

        public void DeleteChildren()
        {
            rootGameObject.DeleteChildren(true, false);
            CustomPseudoIdToLiquidMethId.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // CreatePseudoChain
        //
        // Builds the full custom chain for a given MethDefinition:
        //   1. Clone liquidmeth — set CookableModule.Product = existing MethDefinition.
        //      No meth clone needed; the existing Registry definition IS the output.
        //   2. Clone pseudo, register CustomPseudoIdToLiquidMethId entry.
        //   3. Register both clones in the Registry.
        //   4. Inject a dedicated StationRecipe for this mix into ChemistryStationCanvas.
        //      The recipe has Unlocked=true and IsDiscovered=true so it appears in the
        //      management config recipe dropdown immediately after synthesis.
        //
        // Returns the custom pseudo (the item placed in the dead drop).
        // ─────────────────────────────────────────────────────────────────────
        public QualityItemDefinition CreatePseudoChain(MethDefinition methDef, string pseudoBaseId)
        {
            QualityItemDefinition basePseudoDefinition = ResolvePseudoBaseDefinition(pseudoBaseId);
            if (basePseudoDefinition == null)
                throw new InvalidOperationException($"PseudoFactory: Base pseudo definition '{pseudoBaseId}' not initialized.");
            if (baseLiquidMethDefinition == null)
                throw new InvalidOperationException("PseudoFactory: Base liquid meth definition not initialized.");
            if (baseStationRecipe == null)
                throw new InvalidOperationException("PseudoFactory: Base station recipe not initialized.");

            LiquidMethDefinition customLiquidMeth = ResolveOrCreateCustomLiquidMeth(methDef);
            QualityItemDefinition customPseudo = ResolveOrCreateCustomPseudo(basePseudoDefinition, customLiquidMeth, methDef);

            if (customLiquidMeth == null || customPseudo == null)
                return null;

            Utility.Log($"PseudoFactory: {methDef.ID} → pseudo:{customPseudo.ID} → liquidmeth:{customLiquidMeth.ID} → meth:{methDef.ID}");
            return customPseudo;
        }

        // ─────────────────────────────────────────────────────────────────────
        // InjectRecipeForMix
        //
        // Re-injects the custom StationRecipe into ChemistryStationCanvas for a
        // mix whose assets already exist in the Registry (scene/session reload).
        // Idempotent — skips if the recipe is already present.
        // ─────────────────────────────────────────────────────────────────────
        public void InjectRecipeForMix(UnicornSeedData data, string methId)
        {
            var rawLm = Registry.GetItem($"{methId}_customliquidmeth");
#if IL2CPP
            LiquidMethDefinition customLiquidMeth = rawLm?.TryCast<LiquidMethDefinition>();
#elif MONO
            LiquidMethDefinition customLiquidMeth = rawLm as LiquidMethDefinition;
#endif
            if (data == null || customLiquidMeth == null)
            {
                Utility.Error($"PseudoFactory.InjectRecipeForMix: Cannot resolve assets for '{methId}' — " +
                              $"data={data != null}, liquidmeth={customLiquidMeth != null}.");
                return;
            }

            List<QualityItemDefinition> pseudoVariants = new List<QualityItemDefinition>();
            foreach (var variant in data.variants)
            {
                var customPseudo = Registry.GetItem<QualityItemDefinition>(variant.seedId);
                if (customPseudo != null)
                    pseudoVariants.Add(customPseudo);
            }

            if (pseudoVariants.Count == 0)
            {
                Utility.Error($"PseudoFactory.InjectRecipeForMix: No pseudo variants resolved for '{methId}'.");
                return;
            }

            InjectCustomRecipeInternal(pseudoVariants, customLiquidMeth, methId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // AddPseudoToChemistryStations (static)
        //
        // Adds a custom pseudo ID to the HardFilter whitelists on every
        // ChemistryStation ingredient slot in the scene so the UI accepts it.
        // Mirrors CocaFactory.AddLeafToCauldrons.
        // ─────────────────────────────────────────────────────────────────────
        public static void AddPseudoToChemistryStations(QualityItemDefinition customPseudo)
        {
            ChemistryStation[] stations = GameObject.FindObjectsOfType<ChemistryStation>();
            int patched = 0;
            foreach (ChemistryStation station in stations)
            {
                foreach (ItemSlot slot in station.IngredientSlots)
                {
                    foreach (ItemFilter filter in slot.HardFilters)
                    {
#if IL2CPP
                        ItemFilter_ID idFilter = filter.TryCast<ItemFilter_ID>();
#elif MONO
                        ItemFilter_ID idFilter = filter as ItemFilter_ID;
#endif
                        if (idFilter != null && !idFilter.IDs.Contains(customPseudo.ID))
                        {
                            idFilter.IDs.Add(customPseudo.ID);
                            patched++;
                        }
                    }
                }
            }
            Utility.Log($"PseudoFactory: Added '{customPseudo.ID}' to {patched} chemistry station slot filter(s).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private LiquidMethDefinition CloneCustomLiquidMeth(MethDefinition methDef)
        {
            LiquidMethDefinition clone = UnityEngine.Object.Instantiate(baseLiquidMethDefinition);
            clone.ID = $"{methDef.ID}_customliquidmeth";
            clone.name = clone.ID;
            clone.Name = $"Liquid {methDef.name}";

            if (baseLiquidMethDefinition.StationItem != null)
            {
                StationItem clonedStationItem = UnityEngine.Object.Instantiate(
                    baseLiquidMethDefinition.StationItem, rootGameObject);
                clonedStationItem.name = $"{methDef.ID}_LiquidMethStationItem";

                CookableModule cookable = clonedStationItem.GetModule<CookableModule>();
                if (cookable != null)
                {
                    // Point LabOven output at the EXISTING meth definition — no clone needed.
                    cookable.Product = methDef;
                    Utility.Log($"PseudoFactory: Set CookableModule.Product = '{methDef.ID}'.");
                }
                else
                {
                    Utility.Error($"PseudoFactory: No CookableModule on liquidmeth StationItem for '{methDef.ID}'.");
                }

                clone.StationItem = clonedStationItem;
            }
            else
            {
                Utility.Error("PseudoFactory: baseLiquidMethDefinition.StationItem is null.");
            }

            return clone;
        }

        private LiquidMethDefinition ResolveOrCreateCustomLiquidMeth(MethDefinition methDef)
        {
            string liquidMethId = $"{methDef.ID}_customliquidmeth";
            var rawExisting = Registry.GetItem(liquidMethId);
#if IL2CPP
            LiquidMethDefinition existing = rawExisting?.TryCast<LiquidMethDefinition>();
#elif MONO
            LiquidMethDefinition existing = rawExisting as LiquidMethDefinition;
#endif
            if (existing != null)
                return existing;

            LiquidMethDefinition created = CloneCustomLiquidMeth(methDef);
            if (created != null)
                Singleton<Registry>.Instance.AddToRegistry(created);
            return created;
        }

        private QualityItemDefinition CloneCustomPseudo(QualityItemDefinition basePseudoDefinition, LiquidMethDefinition customLiquidMeth, MethDefinition methDef)
        {
            QualityItemDefinition clone = UnityEngine.Object.Instantiate(basePseudoDefinition);
            clone.ID = $"{methDef.ID}_{basePseudoDefinition.ID}_custompseudo";
            clone.name = clone.ID;
            switch (basePseudoDefinition.DefaultQuality)
            {
                case EQuality.Trash:
                    clone.Name = "Lo-Quality " + methDef.name;
                    break;
                case EQuality.Standard:
                    clone.Name = "Mid-Quality " + methDef.name;
                    break;
                case EQuality.Premium:
                    clone.Name = "Hi-Quality " + methDef.name;
                    break;
                default:
                    clone.Name = methDef.name;
                    break;
            }
            
            clone.Description = basePseudoDefinition.Description;
            clone.StationItem = basePseudoDefinition.StationItem;

            CustomPseudoIdToLiquidMethId[clone.ID] = customLiquidMeth.ID;
            return clone;
        }

        private QualityItemDefinition ResolveOrCreateCustomPseudo(QualityItemDefinition basePseudoDefinition, LiquidMethDefinition customLiquidMeth, MethDefinition methDef)
        {
            string pseudoId = CustomPseudoManager.BuildPseudoSeedId(methDef.ID, basePseudoDefinition.ID);
            var existing = Registry.GetItem<QualityItemDefinition>(pseudoId);
            if (existing != null)
            {
                CustomPseudoIdToLiquidMethId[existing.ID] = customLiquidMeth.ID;
                return existing;
            }

            QualityItemDefinition created = CloneCustomPseudo(basePseudoDefinition, customLiquidMeth, methDef);
            if (created != null)
                Singleton<Registry>.Instance.AddToRegistry(created);
            return created;
        }

        private void InjectCustomRecipeInternal(
            List<QualityItemDefinition> customPseudoVariants,
            LiquidMethDefinition customLiquidMeth,
            string methId)
        {
            var canvas = Singleton<ChemistryStationInterface>.Instance;
            string recipeName = $"{methId}_customrecipe";

            // Idempotent — skip if already present
            foreach (StationRecipe existing in canvas.Recipes)
            {
                if (existing.name == recipeName)
                {
                    Utility.Log($"PseudoFactory: Recipe '{recipeName}' already in canvas — skipping.");
                    return;
                }
            }

            StationRecipe customRecipe = UnityEngine.Object.Instantiate(baseStationRecipe);
            customRecipe.name = recipeName;
            customRecipe.RecipeTitle = $"{customLiquidMeth.Name}";

            // Explicitly unlock — Instantiate copies the asset's serialized value which
            // may be false at runtime even if it was true in the editor.
            customRecipe.Unlocked = true;
            customRecipe.IsDiscovered = true;

            QualityItemDefinition standardPseudo = null;
            foreach (var variant in customPseudoVariants)
            {
                if (variant != null && variant.ID == CustomPseudoManager.BuildPseudoSeedId(methId, CustomPseudoManager.PSEUDO_BASE_ID))
                {
                    standardPseudo = variant;
                    break;
                }
            }
            if (standardPseudo == null)
                standardPseudo = customPseudoVariants[0];

            foreach (var ingredient in customRecipe.Ingredients)
            {
                if (ingredient.Item != null)
                    Utility.Log($"PseudoFactory: Existing ingredient: {ingredient.Item.ID} x{ingredient.Quantity}");

                bool isPseudoIngredient = false;
                foreach (ItemDefinition itemDefinition in ingredient.Items)
                {
                    if (itemDefinition != null && basePseudoDefinitions.ContainsKey(itemDefinition.ID))
                    {
                        isPseudoIngredient = true;
                        break;
                    }
                }

                if (isPseudoIngredient)
                {
                    ingredient.Items.Clear();
                    ingredient.Items.Add(standardPseudo);
                    foreach (var variant in customPseudoVariants)
                    {
                        if (variant != null && !ingredient.Items.Contains(variant))
                            ingredient.Items.Add(variant);
                    }
                    break;
                }
            }

            // Product points to custom liquidmeth — output is automatic, no patch needed.
            customRecipe.Product.Item = customLiquidMeth;
            customRecipe.Product.Quantity = 1;

            StationRecipeEntry component = UnityEngine.Object.Instantiate<StationRecipeEntry>(canvas.RecipeEntryPrefab, canvas.RecipeContainer).GetComponent<StationRecipeEntry>();
            component.AssignRecipe(customRecipe);
            canvas.Recipes.Add(customRecipe);
            canvas.recipeEntries.Add(component);

            Utility.Log($"PseudoFactory: Injected recipe '{recipeName}' (Unlocked={customRecipe.Unlocked}, IsDiscovered={customRecipe.IsDiscovered}) into ChemistryStationInterface.");
        }

        private QualityItemDefinition ResolvePseudoBaseDefinition(string pseudoBaseId)
        {
            return basePseudoDefinitions.TryGetValue(pseudoBaseId, out QualityItemDefinition definition)
                ? definition
                : null;
        }
    }
}
