using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Product.Packaging;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Phone.Delivery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnicornsCustomSeeds.Seeds;
using UnityEngine;
using UnityEngine.Events;

using MelonLoader;

namespace UnicornsCustomSeeds.Managers
{
    public static class StashManager
    {
        private static Dictionary<string, List<PropertyItemDefinition>> ingredientsCache = new Dictionary<string, List<PropertyItemDefinition>>();
        private static Dictionary<string, float> ingredientCostCache = new Dictionary<string, float>();
        public static SupplierStash albertsStash;
        public static bool onClosedExecuting = false;
        
        // Constants for Albert's Stash requirements
        // Constants for Albert's Stash requirements
        private static MelonPreferences_Category ConfigCategory;
        private static MelonPreferences_Entry<int> StashCostEntry;
        private static MelonPreferences_Entry<int> StashQtyEntry;

        public static void InitializeConfig()
        {
            ConfigCategory = MelonPreferences.CreateCategory("UnicornsCustomSeeds");
            StashCostEntry = ConfigCategory.CreateEntry("StashCostRequirement", 500, "Stash Cost Requirement");
            StashQtyEntry = ConfigCategory.CreateEntry("StashQtyRequirement", 20, "Stash Quantity Requirement");
        }

        public static void GetAlbertsStash()
        {
            var temp = UnityEngine.Object.FindObjectsOfType<SupplierStash>();
            foreach (SupplierStash stash in temp)
            {
                if (stash != null && stash.gameObject.name.ToLower().Contains("albert"))
                {
                    Utility.Log(stash.gameObject.name);
                    albertsStash = stash;
                    stash.Storage.onClosed += (Action) AlbertsStashClosed;
                }
            }
        }

        public static void AlbertsStashClosed()
        {
            if(onClosedExecuting) return;
            onClosedExecuting = true;
            ItemSlot cashSlot = null;
            CashInstance cashInstance = null;

            ItemSlot weedSlot = null;
            WeedInstance weedInstance = null;
            var items = albertsStash.Storage.GetAllItems();

            foreach (var slot in albertsStash.Storage.ItemSlots) {
                if (slot?.ItemInstance == null) {
                    continue;
                }

                if (slot.ItemInstance.TryCast<CashInstance>() is CashInstance cashValue)
                {
                    cashSlot = slot;
                    cashInstance = cashValue;
                    continue;
                }

                if (slot.ItemInstance.TryCast<WeedInstance>() is WeedInstance weedInput)
                {
                    weedSlot = slot;
                    weedInstance = weedInput;
                    continue;
                }
            }

            if (cashInstance != null && weedInstance != null)
            {
                Utility.Log($"Weed: {weedInstance.ID}");
                string packaging = weedInstance.AppliedPackaging?.Name;
                int quantity = weedInstance.Quantity;
                uint packageAmount = PackageAmount(packaging);
                uint total = (uint)(quantity * packageAmount);
                if (cashInstance.Balance >= StashCostEntry.Value && total >= StashQtyEntry.Value)
                {
                    if (weedInstance.Definition.TryCast<WeedDefinition>() is WeedDefinition definition && !CustomSeedsManager.DiscoveredSeeds.ContainsKey(weedInstance.Definition.ID))
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
            onClosedExecuting = false;
        }

        private static uint PackageAmount(string packaging)
        {
            // Return the amount based on the packaging type - UPDATABLE
            return packaging switch
            {
                "Brick" => 20,
                "Jar" => 5,
                "Baggie" => 1,
                _ => 0,
            };
        }

        public static WeedDefinition GetBaseStrain(ProductDefinition product)
        {
            var ingredients = GetRecipe(product);
            var rawBaseStrain = ingredients[0];
            if (rawBaseStrain.TryCast<WeedDefinition>() is WeedDefinition weedDefinition) { return weedDefinition; }
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
                    if (ingredient.Item.TryCast<ProductDefinition>() is ProductDefinition prodDef)
                    {
                        DeepSearchRecursive(prodDef, result, visited);
                    }
                    else if (ingredient.Item.TryCast<PropertyItemDefinition>() is PropertyItemDefinition propertyItem)
                    {
                        result.Add(propertyItem);
                    }
                }
            }

        }



    }
}
