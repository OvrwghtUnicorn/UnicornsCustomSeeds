using UnicornsCustomSeeds.SeedQuests;
#if IL2CPP
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.UI.Phone.Messages;
#elif MONO
using ScheduleOne.Messaging;
using ScheduleOne.UI.Phone.Messages;
#endif
namespace UnicornsCustomSeeds.Managers
{
    public static class SeedQuestManager
    {
        public static CustomSeedQuest seedDropoff;

        public static string messageId = "Synthesize Seeds";
        public static bool HasActiveQuest => S1API.Quests.QuestManager.GetQuestByName("Drop off the Mix") != null;

        private static float lastSentTime = 0f;

        public static void Init()
        {
            var quest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Mix") as CustomSeedQuest;
            if (quest != null)
            {
                seedDropoff = quest;
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
            if (UnityEngine.Time.time - lastSentTime < 1f)
            {
                Utility.Log("Double send?");
                return;
            }
            List<string> messages = new List<string>();
            messages.Add("Drop the weed mix and cash in my drop box.");
            ConversationManager.SendMessageChain("Albert", messages);

            if (seedDropoff == null)
            {
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
