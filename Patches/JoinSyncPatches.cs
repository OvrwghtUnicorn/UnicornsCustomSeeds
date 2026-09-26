using System;
using HarmonyLib;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using FishNet;
using FishNet.Connection;
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Patches
{
    /// <summary>
    /// Cauldron.OnSpawnServer sends the cook operation and the configuration to a joining
    /// client, but never the item slots — unlike StorageEntity (which enqueues them),
    /// LabOven, ChemistryStation and MixingStation (which all call
    /// SendItemSlotDataToClient). The result is a vanilla bug: a client that joins while a
    /// cauldron holds coca leaves, gasoline or finished cocaine base sees an empty
    /// cauldron until something else touches a slot.
    ///
    /// This postfix adds the missing send, routed through JoinSyncManager so it also lands
    /// after this mod's custom item definitions rather than before them.
    /// </summary>
    [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.OnSpawnServer))]
    public static class Patch_Cauldron_OnSpawnServer
    {
        public static void Postfix(Cauldron __instance, NetworkConnection connection)
        {
            try
            {
                if (connection == null) return;
                if (!InstanceFinder.IsServer) return;
                if (connection.IsHost) return;

                JoinSyncManager.RegisterCauldron(__instance, connection);
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
        }
    }
}
