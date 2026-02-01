using HarmonyLib;
using UnicornsCustomSeeds.Managers;

#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif

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
