using System;
using HarmonyLib;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppFishNet.Serializing;
using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
#elif MONO
using FishNet.Serializing;
using ScheduleOne;
using ScheduleOne.ItemFramework;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // ItemInstance.CreateInstanceAndRead (ItemInstance.cs:77) is:
    //
    //     string text = reader.ReadString();
    //     if (string.IsNullOrEmpty(text)) return null;
    //     ItemInstance defaultInstance = Registry.GetItem(text).GetDefaultInstance(1);
    //     defaultInstance.Read(reader);
    //
    // Registry.GetItem is not null-checked. An ID the client has no definition for
    // therefore throws an NRE *in the middle of a FishNet read*, which does not just
    // lose that one slot — it aborts the read with the stream parked mid-item and the
    // joining client never finishes replication. That is the observed hang.
    //
    // This prefix makes the failure graceful instead: peek the ID, and if it is one we
    // cannot resolve, consume exactly the bytes the real instance would have consumed,
    // then return null so the slot is simply left empty. The client loads in, minus the
    // unknown item, instead of hanging.
    //
    // It is a safety net, not the fix. An item reaching a client that lacks its
    // definition is still a bug upstream: the host writes item slots for containers whose
    // definitions have not replicated yet. That ordering problem is unsolved.
    //
    // Patched on ItemSerializers.ReadItemInstance, not CreateInstanceAndRead: the probe
    // run showed CreateInstanceAndRead gets inlined away on IL2CPP, while the
    // ReadItemInstance prefix fires.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ItemSerializers), nameof(ItemSerializers.ReadItemInstance))]
    public static class ItemStreamGuard
    {
        private static int rescued;

        public static int RescuedCount => rescued;

        public static bool Prefix(Reader reader, ref ItemInstance __result)
        {
            int start = reader.Position;
            string id;

            try
            {
                id = reader.ReadString();
            }
            catch (Exception)
            {
                reader.Position = start;
                return true;
            }

            // Empty means "no item", which the original already handles correctly.
            if (string.IsNullOrEmpty(id) || Registry.ItemExists(id))
            {
                reader.Position = start;
                return true;
            }

            try
            {
                DrainRemainder(reader, id);
                rescued++;
                Utility.Error($"[ITEMGUARD] '{id}' is not registered on this client — slot left empty, " +
                              $"stream kept aligned ({rescued} rescued).");
            }
            catch (Exception e)
            {
                // A failed drain leaves the stream misaligned either way; at least do not
                // also throw out of the trampoline.
                Utility.Error($"[ITEMGUARD] drain failed for '{id}' — stream may be misaligned.");
                Utility.PrintException(e);
            }

            __result = null;
            return false;
        }

        /// <summary>
        /// Consumes the bytes ItemInstance.Read would have consumed for this ID.
        ///
        /// The byte layout depends on the instance subclass, so a stand-in built from the
        /// definition this custom item was cloned from consumes the correct amount by
        /// construction — ItemInstance writes quantity (ushort), QualityItemInstance adds
        /// quality (ushort), and so on. Falling back to a bare quantity read is correct
        /// for the plain case and is the best guess available otherwise.
        /// </summary>
        private static void DrainRemainder(Reader reader, string id)
        {
            string baseId = ResolveBaseId(id);

            if (!string.IsNullOrEmpty(baseId))
            {
                var baseDef = Registry.GetItem(baseId);
                if (baseDef != null)
                {
                    ItemInstance standIn = baseDef.GetDefaultInstance(1);
                    if (standIn != null)
                    {
                        standIn.Read(reader);
                        return;
                    }
                }

                Utility.Error($"[ITEMGUARD] stand-in '{baseId}' for '{id}' unavailable — " +
                              "falling back to a plain quantity read.");
            }

            // Plain ItemInstance layout: quantity only.
            reader.ReadUInt16();
        }

        /// <summary>
        /// Maps a custom item ID back to the vanilla definition its factory cloned. The
        /// suffixes are generated by this mod (SeedFactory, CocaFactory, SyringeFactory,
        /// PseudoFactory), so this table is authoritative rather than a guess.
        /// </summary>
        private static string ResolveBaseId(string id)
        {
            if (id.EndsWith("_customseeddefinition")) return CustomSeedsManagerBaseSeedId;
            if (id.EndsWith("_customcocaseed")) return "cocaseed";
            if (id.EndsWith("_customcocaleaf")) return "cocaleaf";
            if (id.EndsWith("_customcocainebase")) return "cocainebase";
            if (id.EndsWith("_customliquidmeth")) return "liquidmeth";
            if (id.EndsWith("_customsyringedefinition")) return "sporesyringe";
            if (id.EndsWith("_customspawndefinition")) return ResolveSpawnBaseId();

            // Pseudo variants are "{methId}_{basePseudoId}_custompseudo", so the base is
            // carried in the ID itself — there are three quality tiers, not one base.
            const string pseudoSuffix = "_custompseudo";
            if (id.EndsWith(pseudoSuffix))
            {
                string head = id.Substring(0, id.Length - pseudoSuffix.Length);
                int cut = head.LastIndexOf('_');
                if (cut >= 0 && cut < head.Length - 1)
                    return head.Substring(cut + 1);
            }

            return null;
        }

        private const string CustomSeedsManagerBaseSeedId = "ogkushseed";

        private static string ResolveSpawnBaseId()
        {
            try
            {
                var syringe = Registry.GetItem<SporeSyringeDefinition>("sporesyringe");
                if (syringe != null && syringe.SpawnDefinition != null)
                    return syringe.SpawnDefinition.ID;
            }
            catch (Exception)
            {
                // Fall through to the plain read.
            }
            return null;
        }
    }
}
