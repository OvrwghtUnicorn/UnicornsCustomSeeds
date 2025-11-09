using Il2CppScheduleOne.Economy;
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
using UnityEngine;
using UnityEngine.Events;
using static Il2CppLiquidVolumeFX.LiquidVolume;

namespace UnicornsCustomSeeds.SupplierStashes
{
    public static class StashManager
    {
        private static Dictionary<string, List<PropertyItemDefinition>> ingredientsCache = new Dictionary<string, List<PropertyItemDefinition>>();
        private static Dictionary<string, float> ingredientCostCache = new Dictionary<string, float>();
        public static SupplierStash albertsStash;

        public static void GetAlbertsStash()
        {
            var temp = GameObject.FindObjectsOfType<SupplierStash>();
            foreach (SupplierStash stash in temp)
            {
                if (stash != null && stash.gameObject.name.ToLower().Contains("albert"))
                {
                    Utility.Log(stash.gameObject.name);
                    albertsStash = stash;
                    stash.Storage.onClosed.AddListener((UnityAction)AlbertsStashClosed);
                }
            }
        }

        public static void AlbertsStashClosed()
        {
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
                string packaging = weedInstance.AppliedPackaging?.Name;
                int quantity = weedInstance.Quantity;
                uint packageAmount = PackageAmount(packaging);
                uint total = (uint)(quantity * packageAmount);
                Utility.Log($"Packaging: {packaging} | Quantity: {quantity} | Total: {total}");
                if (cashInstance.Balance >= 500 && total >= 20)
                {
                    if (weedInstance.Definition.TryCast<WeedDefinition>() is WeedDefinition definition)
                    {
                        WeedDefinition baseStrain = GetBaseStrain(definition);
                        if (baseStrain != null)
                        {
                            Utility.Log($"Base is {baseStrain.Name}");
                        }

                        if(CustomSeedsManager.seedDropoff != null)
                        {
                            weedSlot.ChangeQuantity(-(20/(int)packageAmount));
                            cashInstance.ChangeBalance(-500);
                            CustomSeedsManager.CompleteQuest(definition);
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


        /// <summary>
        /// Public wrapper to start the deep search for a product's ingredients.
        /// </summary>
        public static List<PropertyItemDefinition> DeepSearchRecipeMemo(ProductDefinition product)
        {
            // We call our new recursive function that returns a list.
            return DeepSearchRecursiveWithMemo(product, new HashSet<string>());
        }

        /// <summary>
        /// The new recursive method that uses memoization to find all base ingredients.
        /// </summary>
        private static List<PropertyItemDefinition> DeepSearchRecursiveWithMemo(ProductDefinition product, HashSet<string> visited)
        {
            // Base case: The product is null.
            if (product == null || visited.Contains(product.ID))
            {
                return new List<PropertyItemDefinition>();
            }

            // --- MEMOIZATION CHECK ---
            // If we've already calculated the ingredients for this product,
            // immediately return the cached list instead of doing more work.
            if (ingredientsCache.ContainsKey(product.ID))
            {
                return ingredientsCache[product.ID];
            }

            // Base case: The product is a raw material with no further recipe.
            if (product.Recipes.Count == 0)
            {
                var baseIngredientList = new List<PropertyItemDefinition> { product };
                // Cache the result for this "base" product before returning.
                ingredientsCache[product.ID] = baseIngredientList;
                return baseIngredientList;
            }

            var ingredientsForThisProduct = new List<PropertyItemDefinition>();

            foreach (var Recipe in product.Recipes)
            {
                // --- RECURSIVE STEP ---
                // If the product is not in the cache, we need to compute its ingredients.
                var recipeIngredients = new List<PropertyItemDefinition>();
                // Assuming one recipe per product, as in your original code.
                var ingredient1 = Recipe.Ingredients[0];
                var ingredient2 = Recipe.Ingredients[1];

                // Declare the variables to hold the correctly typed ingredients
                ProductDefinition productIngredient = null;
                PropertyItemDefinition mixerIngredient = null;
                // Check if the first ingredient is the ProductDefinition
                if (ingredient1.Item.TryCast<ProductDefinition>() is ProductDefinition subProduct1)
                {
                    // Case 1: ingredient1 is the ProductDefinition
                    productIngredient = subProduct1;
                }
                else if (ingredient1.Item.TryCast<PropertyItemDefinition>() is PropertyItemDefinition subIngredient1)
                {
                    // Case 2: ingredient1 is the mixer
                    mixerIngredient = subIngredient1;
                }

                // Check if the first ingredient is the ProductDefinition
                if (ingredient2.Item.TryCast<ProductDefinition>() is ProductDefinition subProduct2)
                {
                    // Case 1: ingredient1 is the ProductDefinition
                    productIngredient = subProduct2;
                }
                else if (ingredient2.Item.TryCast<PropertyItemDefinition>() is PropertyItemDefinition subIngredient2)
                {
                    // Case 2: ingredient1 is the mixer
                    mixerIngredient = subIngredient2;
                }

                if (productIngredient != null && mixerIngredient != null)
                {
                    recipeIngredients = DeepSearchRecursiveWithMemo(productIngredient,visited);
                    recipeIngredients.Add(mixerIngredient);

                    if (ingredientsForThisProduct.Count == 0 || recipeIngredients.Count < ingredientsForThisProduct.Count)
                    {
                        ingredientsForThisProduct = recipeIngredients;
                    }
                }
            }

            PrintList(ingredientsForThisProduct, -1);

            ingredientsCache[product.ID] = ingredientsForThisProduct;

            return ingredientsForThisProduct;
        }

        public static void PrintList(List<PropertyItemDefinition> ingredients, int ver = 0)
        {
            // 1. Extract the Name from each PropertyItemDefinition using LINQ's Select.
            // 2. Join all the resulting names into a single string using " -> " as the separator.
            string ingredientString = string.Join(" -> ", ingredients.Select(i => i.Name));

            if (ver == 0)
            {
                Utility.Log(ingredientString);

            }
            else if (ver == 1)
            {
                Utility.Success(ingredientString);
            }
            else
            {
                Utility.Error(ingredientString);
            }

        }

    }
}
