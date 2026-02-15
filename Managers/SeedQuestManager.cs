using UnicornsCustomSeeds.SeedQuests;
using Newtonsoft.Json;
using Il2CppScheduleOne.Quests;
using UnicornsCustomSeeds.Seeds;





#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Product;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using FishNet;
using ScheduleOne.Messaging;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.DevUtilities;
using ScheduleOne.Product;
using GenericCol = System.Collections.Generic;
#endif
namespace UnicornsCustomSeeds.Managers
{
    public static class SeedQuestManager
    {
        public static CustomSeedQuest seedDropoff;

        public static string messageId = "Synthesize Seeds";
        public static bool IsWaitingForDropoff = false;
        public static bool HasActiveQuest => IsWaitingForDropoff;

        private static float lastSentTime = 0f;

        public static void Init()
        {
            var quest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Mix") as CustomSeedQuest;
            if (quest != null)
            {
                seedDropoff = quest;
                IsWaitingForDropoff = true;
                if(!InstanceFinder.IsOffline)
                    Utility.Log("Not Offline");
                //NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null,"00000000-0000-0000-0000-000000000000", "11111111-1111-1111-1111-111111111111");
            }
            else
            {
                IsWaitingForDropoff = false;
            }

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
            if (InstanceFinder.IsServer)
            {
                if (UnityEngine.Time.time - lastSentTime < 1f)
                {
                    return;
                }
                List<string> messages = new List<string>();
                messages.Add("Drop the weed mix and cash in my drop box.");
                ConversationManager.SendMessageChain("Albert", messages);

                IsWaitingForDropoff = true;

                if (seedDropoff == null)
                {
                    if (InstanceFinder.IsServer) BroadcastCustomQuest();
                    seedDropoff = S1API.Quests.QuestManager.CreateQuest<CustomSeedQuest>() as CustomSeedQuest;
                }
            }
        }

        public static void BroadcastCustomQuest()
        {
            ProductManager prodManager = NetworkSingleton<ProductManager>.Instance;
            string payload = "[NET-QUEST]";

            var props = new GenericCol.List<string>();
            var appearance = new WeedAppearanceSettings(
                prodManager.DefaultWeed.MainMat.color,
                prodManager.DefaultWeed.SecondaryMat.color,
                prodManager.DefaultWeed.LeafMat.color,
                prodManager.DefaultWeed.StemMat.color);

            prodManager.CreateWeed_Server(payload, CustomSeedsManager.BASE_SEED_ID,
                                             EDrugType.Marijuana, props, appearance);
        }

        public static void CompleteQuest()
        {
            if (seedDropoff != null)
            {
                seedDropoff.Complete();
                seedDropoff = null;
            }
            IsWaitingForDropoff = false;
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
