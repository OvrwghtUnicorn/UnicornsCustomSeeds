using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppScheduleOne.Product;
using UnicornsCustomSeeds.SupplierStashes;

namespace UnicornsCustomSeeds.Patches
{
    [HarmonyPatch(typeof(ProductDefinition), nameof(ProductDefinition.AddRecipe))]
    public class ProductDefinition_AddRecipe_Patch
    {
        static void Postfix(ProductDefinition __instance)
        {
            StashManager.ProcessNewRecipe(__instance);
        }
    }
}
