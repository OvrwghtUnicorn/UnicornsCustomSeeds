using System;
using HarmonyLib;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Management;
#elif MONO
using ScheduleOne.StationFramework;
using ScheduleOne.UI.Management;
#endif

namespace UnicornsCustomSeeds.Patches
{
    /// <summary>
    /// Restores the Clear button on a chemistry station's recipe field.
    ///
    /// Vanilla StationRecipeFieldUI.Refresh enables the button in both branches that want
    /// it, then unconditionally switches it off again on the way out:
    ///
    ///     if (AreFieldsUniform()) {
    ///         if (newVal != null) { ClearButton.SetActive(true); ... }
    ///         else None.SetActive(true);
    ///     } else { Mixed.SetActive(true); ClearButton.SetActive(true); }
    ///     ClearButton.SetActive(false);        // overrides both
    ///
    /// So a station with a recipe selected has no way to change it: no Clear button, and
    /// the selected-recipe panel covers the field's own click target. The only workaround
    /// is to pick the station up and place it again, which is how this surfaced - a
    /// chemist sat idle because the station was still on a recipe whose ingredients did
    /// not include a custom pseudo, and the recipe could not be changed.
    ///
    /// A game bug rather than a mod one, and it hits vanilla recipes identically, but it
    /// blocks this mod's custom recipes from being selected on an in-use station.
    ///
    /// Refresh is invoked through a UnityEvent listener, so it is reached by delegate and
    /// is not a candidate for IL2CPP inlining.
    /// </summary>
    [HarmonyPatch(typeof(StationRecipeFieldUI), "Refresh")]
    public static class StationRecipeFieldUI_Refresh_Patch
    {
        private static void Postfix(StationRecipeFieldUI __instance, StationRecipe newVal)
        {
            try
            {
                if (__instance == null || __instance.ClearButton == null) return;

                // Mirrors what vanilla intended. The mixed state is read back off the
                // object Refresh just set rather than re-deriving uniformity, which would
                // mean comparing ScriptableObject references across interop wrappers.
                bool mixed = __instance.Mixed != null && __instance.Mixed.activeSelf;

                if (newVal != null || mixed)
                    __instance.ClearButton.SetActive(true);
            }
            catch (Exception e)
            {
                Utility.Error("StationRecipeFieldUI_Refresh_Patch.Postfix threw.");
                Utility.PrintException(e);
            }
        }
    }
}
