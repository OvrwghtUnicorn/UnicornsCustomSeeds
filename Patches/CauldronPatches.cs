using System;
using System.Collections.Generic;
using HarmonyLib;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

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
    // We fully replace RemoveIngredients() so we can:
    //   1. Count every leaf type present across all ingredient slots (in
    //      first-seen order).
    //   2. Elect a winner: largest total quantity; first-discovered breaks ties.
    //   3. Swap CocaineBaseDefinition to the winner's custom base (or restore
    //      the vanilla base if the winner is a plain cocaleaf).
    //   4. Replicate the vanilla removal loop restricted to the winner's slots.
    //
    // Patch B (Postfix on FinishCookOperation) restores CocaineBaseDefinition
    // after the cook completes so the cauldron is clean for the next cook.
    // ─────────────────────────────────────────────────────────────────────────

    public static class CauldronBaseSwap
    {
        // Cauldron instance → original CocaineBaseDefinition to restore after cook
        public static readonly Dictionary<Cauldron, QualityItemDefinition> OriginalBase
   = new Dictionary<Cauldron, QualityItemDefinition>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch A — full replacement of RemoveIngredients().
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.RemoveIngredients))]
    public static class Patch_Cauldron_RemoveIngredients
    {
        static bool Prefix(Cauldron __instance, ref EQuality __result)
        {
            try
            {
                // ── Phase 1: tally leaf types in first-seen order ─────────────────
                // orderedIds preserves discovery order for tie-breaking.
                var orderedIds = new List<string>();
                var counts = new Dictionary<string, int>();

                for (int i = 0; i < __instance.IngredientSlots.Length; i++)
                {
                    ItemSlot slot = __instance.IngredientSlots[i];
                    if (slot?.ItemInstance == null || slot.Quantity <= 0) continue;

                    string id = slot.ItemInstance.ID;
                    if (!counts.ContainsKey(id))
                    {
                        orderedIds.Add(id);
                        counts[id] = 0;
                    }
                    counts[id] += slot.Quantity;
                }

                if (orderedIds.Count == 0)
                {
                    // Nothing in slots — let vanilla handle the no-op gracefully.
                    Utility.Error("CauldronPatches: RemoveIngredients called with empty slots.");
                    return true;
                }

                // ── Phase 2: elect winner ─────────────────────────────────────────
                // Largest count wins; first-discovered breaks any tie.
                string winnerId;
                if (orderedIds.Count == 1)
                {
                    winnerId = orderedIds[0];
                }
                else
                {
                    int maxCount = 0;
                    foreach (var kvp in counts)
                        if (kvp.Value > maxCount) maxCount = kvp.Value;

                    winnerId = null;
                    foreach (string id in orderedIds)
                    {
                        if (counts[id] == maxCount)
                        {
                            winnerId = id;
                            break; // first-discovered with max count
                        }
                    }
                }

                Utility.Log($"CauldronPatches: Elected leaf '{winnerId}' " +
                $"(count {counts[winnerId]}) on '{__instance.name}'.");

                // ── Phase 3: set CocaineBaseDefinition for the winner ─────────────
                if (CocaFactory.CustomLeafIdToBaseId.TryGetValue(winnerId, out string baseId))
                {
                    // Winner is a custom leaf — swap to its custom cocaine base.
                    var rawBase = Registry.GetItem(baseId);
#if IL2CPP
                    QualityItemDefinition customBase = rawBase?.TryCast<QualityItemDefinition>();
#elif MONO
    QualityItemDefinition customBase = rawBase as QualityItemDefinition;
#endif
                    if (customBase != null)
                    {
                        // Save original only once per cauldron so repeated cooks
                        // don't overwrite the true vanilla value.
                        if (!CauldronBaseSwap.OriginalBase.ContainsKey(__instance))
                            CauldronBaseSwap.OriginalBase[__instance] = __instance.CocaineBaseDefinition;

                        __instance.CocaineBaseDefinition = customBase;
                        Utility.Log($"CauldronPatches: Swapped CocaineBaseDefinition → '{customBase.ID}'.");
                    }
                    else
                    {
                        Utility.Error($"CauldronPatches: Could not resolve '{baseId}' as " +
                                  "QualityItemDefinition — vanilla base will be used.");
                    }
                }
                else
                {
                    // Winner is a vanilla leaf — restore the original definition if
                    // it was previously swapped (e.g. after a custom cook on the
                    // same cauldron that never called FinishCookOperation cleanly).
                    if (CauldronBaseSwap.OriginalBase.TryGetValue(__instance, out QualityItemDefinition original))
                    {
                        __instance.CocaineBaseDefinition = original;
                        CauldronBaseSwap.OriginalBase.Remove(__instance);
                        Utility.Log("CauldronPatches: Restored vanilla CocaineBaseDefinition " +
                          "for vanilla leaf winner.");
                    }
                }

                // ── Phase 4: replicate vanilla removal — winner slots only ─────────
                // Vanilla: consume 1 gasoline, then drain 20 leaves back-to-front,
                // tracking the lowest quality seen.
                __instance.LiquidSlot.ChangeQuantity(-1, false);

                EQuality bestQuality = EQuality.Heavenly;
                int remaining = 20;

                for (int i = __instance.IngredientSlots.Length - 1; i >= 0 && remaining > 0; i--)
                {
                    ItemSlot slot = __instance.IngredientSlots[i];
                    if (slot?.ItemInstance == null || slot.Quantity <= 0) continue;
                    if (slot.ItemInstance.ID != winnerId) continue;

#if IL2CPP
                    QualityItemInstance qi = slot.ItemInstance.TryCast<QualityItemInstance>();
#elif MONO
    QualityItemInstance qi = slot.ItemInstance as QualityItemInstance;
#endif
                    if (qi != null && qi.Quality < bestQuality)
                        bestQuality = qi.Quality;

                    int toRemove = Mathf.Min(remaining, slot.Quantity);
                    slot.ChangeQuantity(-toRemove, false);
                    remaining -= toRemove;
                }

                __result = bestQuality;
                return false; // skip vanilla RemoveIngredients
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
                return true; // fall back to vanilla on unexpected error
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch B — Postfix on FinishCookOperation().
    //
    // Vanilla has already output the item using the (possibly swapped)
    // CocaineBaseDefinition. Restore the original so subsequent cooks on the
    // same cauldron default to the vanilla cocaine base.
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
