using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Phone.Messages;
using MelonLoader;
using S1API.Quests;
using System;
using UnicornsCustomSeeds.CustomQuests;
using UnityEngine;

namespace UnicornsCustomSeeds.Managers
{
    public static class SeedQuestManager
    {
        public static CustomSeedQuest seedDropoff;
        public static MSGConversation albertsConvo;
        public static bool HasActiveQuest => seedDropoff != null;

        public static void GetAlbertHoover()
        {
            Albert albert = GameObject.FindObjectOfType<Albert>();
            if (albert != null)
            {
                Utility.Log("Found Albert Hoover");
                MSGConversation convo = albert.MSGConversation;
                if (convo != null)
                {
                    Utility.Log("Found Alberts Conversation");
                    albertsConvo = convo;
                    MessageSenderInterface senderInterface = convo.senderInterface;
                    SendableMessage sendable = albertsConvo.CreateSendableMessage("Order Seeds");
                    sendable.onSent += (Action)OnSent;
                }
            }
        }

        public static void OnSent()
        {
            MessageChain messageChain = new MessageChain();
            messageChain.Messages.Add("Drop the weed mix and cash in my drop box.");
            messageChain.id = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            albertsConvo.SendMessageChain(messageChain, 0.5f, true, true);

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
             if (albertsConvo != null)
             {
                 albertsConvo.SendMessage(new Message(text, Message.ESenderType.Other, true, -1), true, true);
             }
        }

        public static void OnSelected()
        {
             if (albertsConvo != null)
             {
                 albertsConvo.senderInterface.SetVisibility(MessageSenderInterface.EVisibility.Docked);
             }
        }
    }
}
