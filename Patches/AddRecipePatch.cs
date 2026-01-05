using HarmonyLib;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.NPCs.Relation;
using Il2CppScheduleOne.Product;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnicornsCustomSeeds.Managers;

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

    //[HarmonyPatch(typeof(Supplier), nameof(Supplier.RelationshipChange))]
    //public class Supplier_RelationshipChange_Patch
    //{
    //    static void Postfix(Supplier __instance, float change)
    //    {
    //        float num = __instance.RelationData.RelationDelta - change;
    //        float relationDelta = __instance.RelationData.RelationDelta;

    //        if (num < 4f && relationDelta >= 4f)
    //        {
    //            ConversationManager.AlbertWelcomeMessage(NPCRelationData.EUnlockType.DirectApproach, true);
    //        }
    //    }
    //}
    //protected virtual void RelationshipChange(float change)
}
