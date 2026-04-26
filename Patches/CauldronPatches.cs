using System;
using System.Collections.Generic;
using HarmonyLib;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // Strategy: swap Cauldron.CocaineBaseDefinition on the specific instance.
    //
    // Cauldron.FinishCookOperation already does exactly what we want:
    //   QualityItemInstance output = this.CocaineBaseDefinition.GetDefaultInstance(10)
    //   output.SetQuality(this.InputQuality)
    //   this.OutputSlot.InsertItem(output)
    //
    // So instead of redirecting the output ourselves, we just replace
    // CocaineBaseDefinition on the cauldron instance at cook-start, let vanilla
    // run untouched, and restore the original value in a Postfix so other
    // cooks on the same cauldron aren't affected.
    // ─────────────────────────────────────────────────────────────────────────

    public static class CauldronBaseSwap
    {
        // Cauldron instance → original CocaineBaseDefinition to restore after cook
        public static readonly Dictionary<Cauldron, QualityItemDefinition> OriginalBase
            = new Dictionary<Cauldron, QualityItemDefinition>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch A — Prefix on RemoveIngredients().
    //
    // CauldronCanvas calls RemoveIngredients() while slots are still populated,
    // BEFORE SendCookOperation(). Inspect slots here and swap CocaineBaseDefinition
    // if a custom leaf is found. Vanilla cook flow then outputs the right item.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.RemoveIngredients))]
    public static class Patch_Cauldron_RemoveIngredients
    {
        static void Prefix(Cauldron __instance)
        {
            try
            {
                foreach (ItemSlot slot in __instance.IngredientSlots)
                {
                    if (slot?.ItemInstance == null) continue;

                    if (!CocaFactory.CustomLeafIdToBaseId.TryGetValue(slot.ItemInstance.ID, out string baseId))
                        continue;

                    var rawCustomBase = Registry.GetItem(baseId);
                    if (rawCustomBase == null)
                    {
                        Utility.Error($"CauldronPatches: Could not resolve custom base '{baseId}' from Registry.");
                        break;
                    }

                    QualityItemDefinition customBase = rawCustomBase.TryCast<QualityItemDefinition>();

                    if (customBase == null)
                    {
                        Utility.Error($"CauldronPatches: Item '{baseId}' is not a QualityItemDefinition.");
                        break;
                    }

                    // Save original so Patch B can restore it
                    CauldronBaseSwap.OriginalBase[__instance] = __instance.CocaineBaseDefinition;
                    __instance.CocaineBaseDefinition = customBase;
                    Utility.Log($"CauldronPatches: Swapped CocaineBaseDefinition → '{customBase.ID}' on '{__instance.name}'.");
                    break;
                }
            }
            catch (Exception e) { Utility.PrintException(e); }
        }
    }

    //[HarmonyPatch(typeof(Cauldron), nameof(Cauldron.RemoveIngredients))]
    //public static class Patch_Cauldron_RemoveIngredients
    //{
    //    static bool Prefix(Cauldron __instance)
    //    {
    //        try
    //        {
    //            Utility.Log($"CauldronPatches: Removing ingredients'{__instance.name}'.");
    //        }
    //        catch (Exception e) { Utility.PrintException(e); }

    //        return true;
    //    }
    //}

    // ─────────────────────────────────────────────────────────────────────────
    // Patch B — Postfix on FinishCookOperation().
    //
    // Vanilla has already output the item using the swapped CocaineBaseDefinition.
    // Restore the original value so subsequent vanilla cooks on the same cauldron
    // still output the correct default cocaine base.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.FinishCookOperation))]
    public static class Patch_Cauldron_FinishCookOperation
    {
        static void Postfix(Cauldron __instance)
        {
            try
            {
                if (!CauldronBaseSwap.OriginalBase.TryGetValue(__instance, out QualityItemDefinition original))
                    return;

                __instance.CocaineBaseDefinition = original;
                CauldronBaseSwap.OriginalBase.Remove(__instance);
                Utility.Log($"CauldronPatches: Restored CocaineBaseDefinition on '{__instance.name}'.");
            }
            catch (Exception e) { Utility.PrintException(e); }
        }
    }
}
