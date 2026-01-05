using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Phone.Messages;
using MelonLoader;
using S1API.Quests;
using System;
using System.Collections.Generic;
using UnicornsCustomSeeds.SeedQuests;
using UnityEngine;

namespace UnicornsCustomSeeds.Managers
{
    public static class SeedQuestManager
    {
        public static CustomSeedQuest seedDropoff;

        public static string messageId = "Synthesize Seeds";
        public static bool HasActiveQuest => seedDropoff != null;

        public static void Init()
        {
             MSGConversation convo = ConversationManager.GetConversation("Albert");
             if (convo != null)
             {
                 MessageSenderInterface senderInterface = convo.senderInterface;
                 SendableMessage sendable = convo.CreateSendableMessage(messageId);
                 sendable.onSent += (Action)OnSent;
             }
        }

        public static void OnSent()
        {
            List<string> messages = new List<string>();
            messages.Add("Drop the weed mix and cash in my drop box.");
            ConversationManager.SendMessageChain("Albert", messages);

            if (seedDropoff == null)
            {
                Utility.Log("Quest Started");
                seedDropoff = S1API.Quests.QuestManager.CreateQuest<CustomSeedQuest>() as CustomSeedQuest;
            }
        }

        public static void CompleteQuest()
        {
            if (seedDropoff != null)
            {
                seedDropoff.Complete();
                seedDropoff = null;
            }
        }

        public static void SendMessage(string text)
        {
             ConversationManager.SendMessage("Albert", text);
        }

        public static void OnSelected()
        {
             MSGConversation convo = ConversationManager.GetConversation("Albert");
             if (convo != null)
             {
                 convo.senderInterface.SetVisibility(MessageSenderInterface.EVisibility.Docked);
             }
        }
    }
}
