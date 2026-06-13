using MelonLoader;
using UnicornsCustomSeeds.TemplateUtils;





#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne;
using ScheduleOne.Growing;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Management;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class StashManager
    {
        private static Dictionary<string, List<PropertyItemDefinition>> ingredientsCache = new Dictionary<string, List<PropertyItemDefinition>>();
        private static Dictionary<string, float> ingredientCostCache = new Dictionary<string, float>();
        // Base strain product ID -> the SeedDefinition that grows it. Nulls are cached too.
        private static Dictionary<string, SeedDefinition> baseStrainSeedCache = new Dictionary<string, SeedDefinition>();
        private static float lastClosedTime = 0f;
        public static SupplierStash albertsStash;

        // Constants for Albert's Stash requirements
        public static MelonPreferences_Category ConfigCategory;
        public static MelonPreferences_Entry<int> StashCostEntry;
        public static MelonPreferences_Entry<int> StashQtyEntry;
        public static MelonPreferences_Entry<int> SynthesizeTime;

        public static void InitializeConfig()
        {
            ConfigCategory = MelonPreferences.CreateCategory("Unicorns Custom Seeds");
            StashCostEntry = ConfigCategory.CreateEntry("StashCostRequirement", 500, "Stash Cost Requirement", "The price that Albert charges to synthesize seeds");
            StashQtyEntry = ConfigCategory.CreateEntry("StashQtyRequirement", 20, "Stash Quantity Requirement", "The quantity of weed that needs to be provided of a certain mix");
            SynthesizeTime = ConfigCategory.CreateEntry("SynthesizeTime", 30, "Synthesize Time", "Time in secondsd that it will take for Albert to synthesize a seed");
        }

        public static void GetAlbertsStash()
        {
            var temp = UnityEngine.Object.FindObjectsOfType<SupplierStash>();
            foreach (SupplierStash stash in temp)
            {
                if (stash != null && stash.gameObject.name.ToLower().Contains("albert"))
                {
                    Utility.Log("Alberts Ready to Synthesize");
                    albertsStash = stash;
                    stash.Storage.onClosed += (Action)AlbertsStashClosed;
                    break;
                }
            }
        }

        public static void AlbertsStashClosed()
        {
            if (UnityEngine.Time.time - lastClosedTime < 1.0f) return;
            lastClosedTime = UnityEngine.Time.time;

            ItemSlot cashSlot = null;
            CashInstance cashInstance = null;

            ItemSlot weedSlot = null;
            WeedInstance weedInstance = null;
            var items = albertsStash.Storage.GetAllItems();
            Utility.Log("Alberts Stash Looping through items");
            foreach (var slot in albertsStash.Storage.ItemSlots)
            {
                if (slot?.ItemInstance == null)
                {
                    continue;
                }

#if IL2CPP
                if (slot.ItemInstance.TryCast<CashInstance>() is CashInstance cashValue)
#elif MONO
                if (slot.ItemInstance is CashInstance cashValue)
#endif
                {
                    cashSlot = slot;
                    cashInstance = cashValue;
                    continue;
                }

#if IL2CPP
                if (slot.ItemInstance.TryCast<WeedInstance>() is WeedInstance weedInput)
#elif MONO
                if (slot.ItemInstance is WeedInstance weedInput)
#endif
                {
                    weedSlot = slot;
                    weedInstance = weedInput;
                    continue;
                }
            }

            if (cashInstance != null && weedInstance != null)
            {
                string packaging = weedInstance.AppliedPackaging?.Name;
                int quantity = weedInstance.Quantity;
                uint packageAmount = PackageAmount(packaging);
                if (packageAmount <= 0) return;
                uint total = (uint)(quantity * packageAmount);
                if (cashInstance.Balance >= StashCostEntry.Value && total >= StashQtyEntry.Value)
                {
#if IL2CPP
                    WeedDefinition definition = weedInstance.Definition.TryCast<WeedDefinition>();
#elif MONO
                    WeedDefinition definition = (WeedDefinition)weedInstance.Definition;
#endif
                    if (definition != null && !CustomSeedsManager.DiscoveredSeeds.ContainsKey(weedInstance.Definition.ID))
                    {
                        if (SeedQuestManager.HasActiveQuest)
                        {
                            weedSlot.ChangeQuantity(-(StashQtyEntry.Value / (int)packageAmount));
                            cashInstance.ChangeBalance(-StashCostEntry.Value);
                            SeedQuestManager.CompleteQuest();
                            SeedQuestManager.SendMessage("I will begin synthesizing the seed");
                            CustomSeedsManager.StartSeedCreation(definition);
                        }
                    }
                }
            }
        }

        private static uint PackageAmount(string packaging)
        {
            // Return the amount based on the packaging type - UPDATABLE
            return packaging switch
            {
                "Brick" => 20,
                "Jar" => 5,
                "Baggie" => 1,
                _ => 1, // Fallback for loose items
            };
        }

        public static WeedDefinition GetBaseStrain(ProductDefinition product)
        {
            var ingredients = GetRecipe(product);
            var rawBaseStrain = ingredients[0];

#if IL2CPP
            WeedDefinition weedDefinition = rawBaseStrain.TryCast<WeedDefinition>();
#elif MONO
            WeedDefinition weedDefinition = (WeedDefinition) rawBaseStrain;
#endif

            if (weedDefinition != null) { return weedDefinition; }
            return null;
        }

        public static List<PropertyItemDefinition> GetRecipe(ProductDefinition product)
        {
            if (ingredientsCache.ContainsKey(product.ID)) return ingredientsCache[product.ID];
            return DeepSearchRecipe(product);
        }

        public static float GetIngredientCost(ProductDefinition product)
        {
            if (ingredientCostCache.ContainsKey(product.ID)) return ingredientCostCache[product.ID];

            var ingredients = GetRecipe(product);
            float totalCost = CalculateTotalCost(ingredients);
            ingredientCostCache.Add(product.ID, totalCost);
            return totalCost;
        }

        public static void ProcessNewRecipe(ProductDefinition product)
        {
            if (product == null)
                return;

            // Primes ingredientsCache for this product. The cost itself is computed lazily
            // by GetIngredientCost — computing it here too only burned CPU and spammed the
            // log on every AddRecipe during load, since the result was discarded.
            DeepSearchRecipe(product);
        }

        /// <summary>
        /// Sums a mix's ingredient costs: every additive's own shop price, plus the shop
        /// price of the base strain's SEED. A base ProductDefinition's own BasePurchasePrice
        /// is deliberately not counted — for a runtime-created mix it is a meaningless
        /// inherited copy of the ProductManager.DefaultWeed template.
        /// </summary>
        private static float CalculateTotalCost(List<PropertyItemDefinition> ingredients)
        {
            float totalCost = 0f;
            foreach (var ingredient in ingredients)
            {
#if IL2CPP
                ProductDefinition product = ingredient.TryCast<ProductDefinition>();
#elif MONO
                ProductDefinition product = ingredient as ProductDefinition;
#endif
                if (product == null)
                {
                    // Additive (banana, cuke, ...) — its own shop price counts directly.
                    totalCost += ingredient.BasePurchasePrice;
                    continue;
                }
								
                SeedDefinition seed = ResolveSeedForProduct(product);
                if (seed != null)
								{
                    totalCost += seed.BasePurchasePrice;
                }
                else
                {
                    Utility.Error($"StashManager: no SeedDefinition for base strain '{product.ID}' — its price is missing from the total.");
                }
            }
            return totalCost;
        }

        /// <summary>
        /// Resolves the SeedDefinition that grows a given base strain. Appending "seed" to
        /// the product ID is only a convention of the weed assets — it already breaks in
        /// vanilla for coca (cocaleaf resolves to cocaseed) — so it is used as a fast path
        /// with a real reverse lookup as the fallback.
        /// </summary>
        private static SeedDefinition ResolveSeedForProduct(ProductDefinition product)
        {
            if (baseStrainSeedCache.TryGetValue(product.ID, out SeedDefinition cached))
                return cached;

            SeedDefinition seed = Registry.GetItem<SeedDefinition>(product.ID + "seed");
            if (seed == null)
                seed = FindSeedByHarvestedProduct(product);

            // Cache nulls too, so a strain that resolves to nothing doesn't rescan every call.
            baseStrainSeedCache[product.ID] = seed;
            return seed;
        }

        /// <summary>
        /// Finds the seed whose plant actually harvests this product, using the game's own
        /// object references rather than an ID convention. Mod-created seeds are skipped: a
        /// player can synthesize a custom seed from a pure vanilla strain, and matching that
        /// here would feed a custom seed's price back into its own calculation.
        /// </summary>
        private static SeedDefinition FindSeedByHarvestedProduct(ProductDefinition product)
        {
            var utilities = Singleton<ManagementUtilities>.Instance;
            if (utilities == null || utilities.Seeds == null)
                return null;

            foreach (SeedDefinition seed in utilities.Seeds)
            {
                if (seed == null || seed.PlantPrefab == null)
                    continue;
                if (seed.ID != null && seed.ID.Contains("customseeddefinition"))
                    continue;

                foreach (PlantHarvestable harvestable in seed.PlantPrefab.GetComponentsInChildren<PlantHarvestable>(true))
                {
                    if (harvestable == null || harvestable.Product == null)
                        continue;

                    // Compare by ID — reference equality is unreliable across IL2CPP wrappers.
                    if (harvestable.Product.ID == product.ID)
                        return seed;
                }
            }
            return null;
        }

        /// <summary>
        /// Drops every cached lookup. Must run on scene change: none of these were ever
        /// cleared, so loading save A then save B in one session reused A's ingredient
        /// lists and prices for any colliding mix ID (mix IDs come from user-chosen names).
        /// </summary>
        public static void ClearCaches()
        {
            ingredientsCache.Clear();
            ingredientCostCache.Clear();
            baseStrainSeedCache.Clear();
        }

        public static List<PropertyItemDefinition> DeepSearchRecipe(ProductDefinition product)
        {
            if (ingredientsCache.ContainsKey(product.ID))
            {
                return ingredientsCache[product.ID];
            }

            var result = new List<PropertyItemDefinition>();
            var visited = new HashSet<string>();
            DeepSearchRecursive(product, result, visited);
            ingredientsCache.Add(product.ID, result);
            return result;
        }
        private static void DeepSearchRecursive(ProductDefinition product, List<PropertyItemDefinition> result, HashSet<string> visited)
        {
            if (product == null || visited.Contains(product.ID))
            {
                return;
            }

            visited.Add(product.ID);

            if (product.Recipes.Count == 0)
            {
                result.Insert(0, product);
                return;
            }

            foreach (var ingredient in product.Recipes[0].Ingredients)
            {
                if (ingredient != null)
                {
#if IL2CPP
                    if (ingredient.Item.TryCast<ProductDefinition>() is ProductDefinition prodDef)
#elif MONO
                    if (ingredient.Item is ProductDefinition prodDef)
#endif
                    {
                        DeepSearchRecursive(prodDef, result, visited);
                    }
#if IL2CPP
                    else if (ingredient.Item.TryCast<PropertyItemDefinition>() is PropertyItemDefinition propertyItem)
#elif MONO
                    else if (ingredient.Item is PropertyItemDefinition propertyItem)
#endif
                    {
                        result.Add(propertyItem);
                    }
                }
            }

        }



    }
}