using MelonLoader;
using Il2CppScheduleOne;


#if IL2CPP
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class StashManager
    {
        private static Dictionary<string, List<PropertyItemDefinition>> ingredientsCache = new Dictionary<string, List<PropertyItemDefinition>>();
        private static Dictionary<string, float> ingredientCostCache = new Dictionary<string, float>();
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
            StashCostEntry = ConfigCategory.CreateEntry("StashCostRequirement", 500, "Stash Cost Requirement","The price that Albert charges to synthesize seeds");
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
                    stash.Storage.onClosed += (Action) AlbertsStashClosed;
                    break;
                }
            }
        }

        public static void AlbertsStashClosed()
        {
            if (UnityEngine.Time.time - lastClosedTime < 1.0f) return;
            lastClosedTime = UnityEngine.Time.time;

            Utility.Log("Alberts Stash Closed");
            
            ItemSlot cashSlot = null;
            CashInstance cashInstance = null;

            ItemSlot weedSlot = null;
            WeedInstance weedInstance = null;
            var items = albertsStash.Storage.GetAllItems();
            Utility.Log("Alberts Stash Looping through items");
            foreach (var slot in albertsStash.Storage.ItemSlots) {
                if (slot?.ItemInstance == null) {
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
                    if ( definition != null && !CustomSeedsManager.DiscoveredSeeds.ContainsKey(weedInstance.Definition.ID))
                    {
                        if(SeedQuestManager.HasActiveQuest)
                        {
                            weedSlot.ChangeQuantity(-(StashQtyEntry.Value/(int)packageAmount));
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

            if ( weedDefinition != null ) { return weedDefinition; }
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

            var ingredients = DeepSearchRecipe(product);

            float totalCost = CalculateTotalCost(ingredients);

        }

        private static float CalculateTotalCost(List<PropertyItemDefinition> ingredients)
        {
            float totalCost = 0f;
            foreach (var ingredient in ingredients)
            {
                if (ingredient is not ProductDefinition)
                {
                    totalCost += ingredient.BasePurchasePrice;
                }
            }
            return totalCost;
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
