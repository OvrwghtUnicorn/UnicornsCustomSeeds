using HarmonyLib;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.PlayerScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnicornsCustomSeeds.Patches
{
    [HarmonyPatch(typeof(SendableMessage), nameof(SendableMessage.IsValid))]
    public class SendableMessage_IsValid_Patch
    {
        public static bool Prefix(SendableMessage __instance, ref bool __result, out string invalidReason)
        {
            invalidReason = String.Empty;
            if (__instance.Text != null && __instance.Text == "Order Seeds") {
                if (CustomSeedsManager.seedDropoff != null)
                {
                    invalidReason = "Seed Synthesizing is already in progress";
                    __result = false;
                } else if (DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position) == null)
                {
                    invalidReason = "No deaddrops are available";
                    __result = false;
                } else
                {
                    __result = true;
                }
                return false;
            }

            return true;
        }
    }
}
