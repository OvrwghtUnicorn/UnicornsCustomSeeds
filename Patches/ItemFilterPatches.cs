using System;
using System.Collections.Generic;
using HarmonyLib;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne.ItemFramework;
// ItemFilter_ID.IDs is an Il2CppSystem list on this target, not a managed one, and the
// whole point here is to hand the filter our own list instance. Aliased so the shared
// list below is the exact type the field expects on both targets.
using IdList = Il2CppSystem.Collections.Generic.List<string>;
#elif MONO
using ScheduleOne.ItemFramework;
using IdList = System.Collections.Generic.List<string>;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // Two vanilla filters reject custom coca leaves because they match on a
    // hardcoded "cocaleaf" string literal rather than on a prefab field:
    //
    //   Cauldron slot setup -> new ItemFilter_ID(new List<string>{ "cocaleaf" })
    //   ItemFilter_Dryable  -> instance.ID == "cocaleaf"
    //
    // Neither has a field to edit, so the prefab approach used for
    // MushroomSpawnStation does not apply here. These are patches instead.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The ingredient whitelist every cauldron shares.
    ///
    /// ItemFilter_ID stores its List&lt;string&gt; by reference and never copies it, so
    /// pointing every cauldron's filter at this one list means registering an ID here
    /// updates every cauldron at once - placed, loaded from save, or built later.
    ///
    /// Process-scoped like the spawn station prefab, so it is reset to vanilla on load
    /// rather than accumulating a previous save's leaf IDs.
    /// </summary>
    public static class CauldronIngredientFilter
    {
        public const string VANILLA_LEAF_ID = "cocaleaf";
        public const string CUSTOM_LEAF_SUFFIX = "_customcocaleaf";

        public static readonly IdList SharedIds = CreateVanillaList();

        private static IdList CreateVanillaList()
        {
            var list = new IdList();
            list.Add(VANILLA_LEAF_ID);
            return list;
        }

        /// <summary>Whitelist a custom leaf on every cauldron. Idempotent.</summary>
        public static void Register(string leafId)
        {
            if (string.IsNullOrEmpty(leafId)) return;
            if (SharedIds.Contains(leafId)) return;
            SharedIds.Add(leafId);
            Utility.Log($"CauldronIngredientFilter: whitelisted '{leafId}' ({SharedIds.Count} ids).");
        }

        /// <summary>
        /// Drop everything a previous save registered. Mutates in place - the list
        /// identity has to survive, because live filters hold this exact reference.
        /// </summary>
        public static void ResetToVanilla()
        {
            if (SharedIds.Count == 1) return;
            int before = SharedIds.Count;
            SharedIds.Clear();
            SharedIds.Add(VANILLA_LEAF_ID);
            Utility.Log($"CauldronIngredientFilter: reset {before} -> 1 (vanilla only).");
        }

        public static bool IsCustomCocaLeaf(string id) =>
            !string.IsNullOrEmpty(id) && id.EndsWith(CUSTOM_LEAF_SUFFIX, StringComparison.Ordinal);
    }

    /// <summary>
    /// <summary>
    /// Hands every cauldron ingredient slot the shared whitelist at the moment its filter
    /// is created, so a cauldron built after a mix was synthesized still accepts that
    /// mix's leaf.
    ///
    /// Patching the filter's creation instead of the cauldron avoids ever reading
    /// Cauldron.IngredientSlots, which access-violates on a ghost (placement preview):
    /// Awake skips slot construction when isGhost is set, leaving the field holding a
    /// Unity-serialized array the game's own code never built. A ghost never reaches
    /// AddFilter at all, so this patch cannot see one.
    ///
    /// AddFilter is a small public method with several callers. If IL2CPP inlined it this
    /// prefix silently never fires, which shows up as cauldrons rejecting custom leaves.
    /// </summary>
    [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.AddFilter))]
    public static class ItemSlot_AddFilter_Patch
    {
        private static int repointed = 0;

        private static void Prefix(ItemFilter filter)
        {
            try
            {
                if (filter == null) return;

#if IL2CPP
                ItemFilter_ID idFilter = filter.TryCast<ItemFilter_ID>();
#elif MONO
                ItemFilter_ID idFilter = filter as ItemFilter_ID;
#endif
                if (idFilter == null || idFilter.IDs == null) return;

                // Scoped to the cauldron's ingredient filter. AddFilter also runs for the
                // player inventory, clothing, the pawn shop and every other station, so
                // matching on the vanilla leaf id is what keeps this off all of them. The
                // cauldron's own LiquidSlot ("gasoline") is excluded by the same test.
                if (!idFilter.IDs.Contains(CauldronIngredientFilter.VANILLA_LEAF_ID)) return;

                // Assigned unconditionally - idempotent, and on IL2CPP two managed wrappers
                // can point at the same native list, so a reference check proves nothing.
                idFilter.IDs = CauldronIngredientFilter.SharedIds;
                repointed++;

                Utility.Log($"ItemSlot_AddFilter_Patch: filter #{repointed} now on the shared " +
                            $"whitelist ({CauldronIngredientFilter.SharedIds.Count} ids).");
            }
            catch (Exception e)
            {
                Utility.Error("ItemSlot_AddFilter_Patch.Prefix threw.");
                Utility.PrintException(e);
            }
        }
    }

    /// <summary>
    /// ItemFilter_Dryable passes weed and shrooms by drug type, so custom products of
    /// those types already work. Its coca branch is an ID literal, so custom leaves are
    /// rejected by the drying rack's input slot.
    /// </summary>
    [HarmonyPatch(typeof(ItemFilter_Dryable), nameof(ItemFilter_Dryable.DoesItemMatchFilter))]
    public static class ItemFilter_Dryable_DoesItemMatchFilter_Patch
    {
        private static void Postfix(ItemInstance instance, ref bool __result)
        {
            if (__result) return;
            if (DryableHelper.IsDryableCustomLeaf(instance)) __result = true;
        }
    }

    /// <summary>
    /// Botanist calls ItemFilter_Dryable.IsItemDryable directly rather than going
    /// through the slot's filter, so employees need the static patched too.
    ///
    /// Small public static method: IL2CPP may have inlined it, in which case this patch
    /// silently does nothing and only the slot-level patch above takes effect. That
    /// costs nothing, and the alternative is employees skipping custom leaves.
    /// </summary>
    [HarmonyPatch(typeof(ItemFilter_Dryable), nameof(ItemFilter_Dryable.IsItemDryable))]
    public static class ItemFilter_Dryable_IsItemDryable_Patch
    {
        private static void Postfix(ItemInstance instance, ref bool __result)
        {
            if (__result) return;
            if (DryableHelper.IsDryableCustomLeaf(instance)) __result = true;
        }
    }

    internal static class DryableHelper
    {
        /// <summary>
        /// Mirrors the vanilla cocaleaf branch - a custom leaf below Heavenly quality -
        /// so custom leaves dry under exactly the same rule as the base game's.
        /// </summary>
        internal static bool IsDryableCustomLeaf(ItemInstance instance)
        {
            try
            {
                if (instance == null) return false;
                if (!CauldronIngredientFilter.IsCustomCocaLeaf(instance.ID)) return false;

#if IL2CPP
                QualityItemInstance quality = instance.TryCast<QualityItemInstance>();
#elif MONO
                QualityItemInstance quality = instance as QualityItemInstance;
#endif
                // No quality component means no way to honour the vanilla cap; refuse
                // rather than letting a Heavenly leaf through.
                if (quality == null) return false;

                return quality.Quality < EQuality.Heavenly;
            }
            catch (Exception e)
            {
                Utility.Error("DryableHelper.IsDryableCustomLeaf threw.");
                Utility.PrintException(e);
                return false;
            }
        }
    }
}
