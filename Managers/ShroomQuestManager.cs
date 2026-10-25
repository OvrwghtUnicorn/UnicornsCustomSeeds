using System;
using System.Collections;
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
    public static class ShroomQuestManager
    {
        public static CustomSynthesisQuest shroomDropoff;
        public static string sendableMessageId = "Synthesize Shrooms";
        public static bool IsWaitingForDropoff = false;
        public static bool HasActiveQuest => IsWaitingForDropoff;

        private static float lastSentTime = 0f;

        public static void Init()
        {
            var quest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Shroom Mix") as CustomSynthesisQuest;
            if (quest != null)
            {
                shroomDropoff = quest;
                IsWaitingForDropoff = true;
            }
            else
            {
                IsWaitingForDropoff = false;
            }

            MSGConversation convo = ConversationManager.GetConversation("Phil");
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

            List<string> messages = new List<string> { "Drop the shroom mix and cash in my drop box." };
            ConversationManager.SendMessageChain("Phil", messages);

            IsWaitingForDropoff = true;

            if (shroomDropoff == null)
            {
                NetworkSyncManager.BroadcastQuestConfig(EDrugType.Shrooms);
                shroomDropoff = S1API.Quests.QuestManager.CreateQuest<ShroomSynthesisQuest>() as CustomSynthesisQuest;
                shroomDropoff?.SetDrugType(EDrugType.Shrooms);
                if (shroomDropoff == null)
                    Utility.Error("[ShroomQuestManager.OnSent] CreateQuest failed on host.");
            }
        }

        /// <summary>
        /// Asynchronously creates a CustomSynthesisQuest with retry logic, mirroring
        /// SeedQuestManager.CreateQuestAsync. Called on a client on receipt of a
        /// [NET-QUEST] broadcast for Shrooms.
        /// </summary>
        public static void CreateQuestAsync()
        {
            MelonCoroutines.Start(CreateQuestCoroutine());
        }

        private static IEnumerator CreateQuestCoroutine()
        {
            // Must not create a quest mid-load — SetupJournalEntry NREs on a UI that does
            // not exist yet. See NetworkSyncManager.IsGameLoading.
            while (NetworkSyncManager.IsGameLoading) yield return null;

            const int maxRetries = 5;
            int attemptCount = 0;

            while (attemptCount < maxRetries)
            {
                attemptCount++;
                try
                {
                    var existingQuest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Shroom Mix") as CustomSynthesisQuest;
                    if (existingQuest != null)
                    {
                        shroomDropoff = existingQuest;
                        IsWaitingForDropoff = true;
                        yield break;
                    }

                    shroomDropoff = S1API.Quests.QuestManager.CreateQuest<ShroomSynthesisQuest>() as CustomSynthesisQuest;
                    shroomDropoff?.SetDrugType(EDrugType.Shrooms);

                    if (shroomDropoff != null)
                    {
                        IsWaitingForDropoff = true;
                        yield break;
                    }
                }
                catch
                {
                    // Silently fail - components are still initializing
                }

                if (attemptCount < maxRetries)
                {
                    yield return new WaitForSeconds(1f);
                }
            }

            Utility.Error($"[ShroomQuestManager.CreateQuestAsync] Failed to load quest after {maxRetries} attempts");
        }

        public static void CompleteQuest()
        {
            if (shroomDropoff != null)
            {
                shroomDropoff.Complete();
                shroomDropoff = null;
            }
            IsWaitingForDropoff = false;
        }

        public static void SendMessage(string text) => ConversationManager.SendMessage("Phil", text);
    }
}
