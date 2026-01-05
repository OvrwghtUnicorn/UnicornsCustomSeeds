using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.NPCs.Relation;
using System.Collections;
using System.Collections.Generic;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;
using MelonLoader;
using Il2CppScheduleOne.Dialogue;

namespace UnicornsCustomSeeds.Managers
{
    public static class ConversationManager
    {
        public static Dictionary<string, MSGConversation> Conversations = new Dictionary<string, MSGConversation>();
        public static Albert albert;
        public static NPCRelationData albertRelation;
        public static string welcomeMessage = "A <color='#FA2FBD'><b>Unicorn</b></color> I know can synthesize your custom mixes into seeds. If you'd like to use that service, send me a text.";

        public static void Init()
        {
            // Find Albert
            albert = GameObject.FindObjectOfType<Albert>();
            if (albert != null){
                if(albert.MSGConversation != null){
                Utility.Log("ConversationManager: Found Albert Hoover");
                    RegisterConversation("Albert", albert.MSGConversation);
                    albertRelation = albert.RelationData;
                }

                if (CustomSeedsManager.FirstLoad && albert.RelationData != null)
                {
                    if (albert.RelationData.RelationDelta >= 4f)
                    {
                        MelonCoroutines.Start(WelcomeRoutine());
                    } else {
                        DialogueDatabase db = albert.DialogueHandler.Database;
                        var generic = db.GetModule(EDialogueModule.Generic);
                        for (var i = 0; i < generic.Entries.Count; i++) {
                            if (generic.Entries[i] != null && generic.Entries[i].Key == "supplier_meetings_unlocked")
                            {
                                Utility.Log("Found Supplier Entry");
                                Entry supplierEntry = generic.Entries[i];
                                Utility.Log($"Chain Count: {supplierEntry.Chains.Length}");
                                DialogueChain chain = supplierEntry.Chains[0];
                                if (chain != null && chain.Lines[chain.Lines.Length - 1] != welcomeMessage)
                                {
                                    string[] swap = chain.Lines;
                                    string[] newLines = new string[swap.Length + 1];
                                    newLines[0] = swap[0];
                                    newLines[1] = swap[1];
                                    newLines[2] = welcomeMessage;
                                    chain.Lines = newLines;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                Utility.Log("ConversationManager: Could not find Albert or his conversation.");
            }
        }

        public static void RegisterConversation(string name, MSGConversation convo)
        {
            if (!Conversations.ContainsKey(name))
            {
                Conversations.Add(name, convo);
            }
            else
            {
                Conversations[name] = convo; // Update if exists
            }
        }

        public static MSGConversation GetConversation(string name)
        {
            if (Conversations.TryGetValue(name, out var convo))
            {
                return convo;
            }
            return null;
        }

        public static void SendMessage(string characterName, string text)
        {
            if (Conversations.TryGetValue(characterName, out var convo))
            {
                if (convo != null)
                {
                    convo.SendMessage(new Message(text, Message.ESenderType.Other, true, -1), true, true);
                }
            }
            else
            {
                Utility.Log($"ConversationManager: Conversation for {characterName} not found.");
            }
        }

        public static void SendMessageChain(string characterName, System.Collections.Generic.List<string> messages)
        {
            if (Conversations.TryGetValue(characterName, out var convo))
            {
                if (convo != null)
                {
                    MessageChain messageChain = new MessageChain();
                    foreach (var msg in messages)
                    {
                        messageChain.Messages.Add(msg);
                    }
                    messageChain.id = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    convo.SendMessageChain(messageChain, 0.5f, true, true);
                }
            }
            else
            {
                Utility.Log($"ConversationManager: Conversation for {characterName} not found.");
            }
        }
        
        public static void AlbertWelcomeMessage(NPCRelationData.EUnlockType type, bool temp)
        {
            SendMessage("Albert", welcomeMessage);
        }
        
        private static IEnumerator WelcomeRoutine()
        {
            yield return new WaitForSeconds(10f);
            AlbertWelcomeMessage(NPCRelationData.EUnlockType.DirectApproach, true);
        }
    }
}
