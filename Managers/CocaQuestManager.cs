using System;
using System.Collections.Generic;
using MelonLoader;
using UnicornsCustomSeeds.SeedQuests;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Product;
#elif MONO
using FishNet;
using ScheduleOne.Messaging;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.DevUtilities;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class CocaQuestManager
    {
        public static CustomSynthesisQuest cocaDropoff;
        public static string sendableMessageId = "Synthesize Coca";
        public static bool IsWaitingForDropoff = false;
        public static bool HasActiveQuest => IsWaitingForDropoff;

        private static float lastSentTime = 0f;

        public static void Init()
        {
            var quest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Cocaine Mix") as CustomSynthesisQuest;
            if (quest != null)
            {
                cocaDropoff = quest;
                IsWaitingForDropoff = true;
            }
            else
            {
                IsWaitingForDropoff = false;
            }

            MSGConversation convo = ConversationManager.GetConversation("Salvador");
            if (convo != null)
            {
                SendableMessage sendable = convo.CreateSendableMessage(sendableMessageId);
                sendable.onSent += (Action)OnSent;
            }
        }

        public static void OnSent()
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - lastSentTime < 1f) return;
            lastSentTime = Time.time;

            List<string> messages = new List<string> { "Drop the cocaine mix and cash in my drop box." };
            ConversationManager.SendMessageChain("Salvador", messages);

            IsWaitingForDropoff = true;

            if (cocaDropoff == null)
            {
                cocaDropoff = S1API.Quests.QuestManager.CreateQuest<CustomSynthesisQuest>() as CustomSynthesisQuest;
                cocaDropoff?.SetDrugType(EDrugType.Cocaine);
            }
        }

        public static void CompleteQuest()
        {
            if (cocaDropoff != null)
            {
                cocaDropoff.Complete();
                cocaDropoff = null;
            }
            IsWaitingForDropoff = false;
        }

        public static void SendMessage(string text) => ConversationManager.SendMessage("Salvador", text);
    }
}
