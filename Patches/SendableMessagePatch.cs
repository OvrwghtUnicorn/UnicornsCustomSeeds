using HarmonyLib;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.PlayerScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading.Tasks;
using UnicornsCustomSeeds.Managers;

namespace UnicornsCustomSeeds.Patches
{
    [HarmonyPatch(typeof(SendableMessage), nameof(SendableMessage.IsValid))]
    public class SendableMessage_IsValid_Patch
    {
        public static bool Prefix(SendableMessage __instance, ref bool __result, out string invalidReason)
        {
            invalidReason = String.Empty;

            if (__instance.Text != null && __instance.Text == SeedQuestManager.messageId) {
                if (SeedQuestManager.HasActiveQuest)
                {
                    invalidReason = "Seed Synthesizing is already in progress";
                    __result = false;
                } else if (DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position) == null)
                {
                    invalidReason = "No deaddrops are available";
                    __result = false;
                } else if (ConversationManager.albert == null || ConversationManager.albert.RelationData.RelationDelta < 4f)
                {
                    invalidReason = "Relationship isn't good enough";
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
