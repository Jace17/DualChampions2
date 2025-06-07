using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualChampions2
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class DualChampions2 : BaseUnityPlugin
    {
        public const string pluginGuid = "com.jacelendro.dualchampions2";
        public const string pluginName = "Dual Champions 2";
        public const string pluginVersion = "1.0";

        public void Awake()
        {
            Logger.LogInfo("Loading Dual Champions 2...");

            var harmony = new Harmony(pluginGuid);
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(SaveManager), "SetupRun")]
    class AddSubclassChampionCard
    {
        public static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("AddSubclassChampionCard");
        public static void Postfix(SaveManager __instance, AllGameData ___allGameData)
        {
            if(__instance.HasAlliedChampion)
            {
                Log.LogInfo("Allied champion enabled via mutator, skipping subclass champion card addition.");
                return;
            }

            ClassData subclass = __instance.GetSubClass();
            if (__instance.HasCardById(subclass.GetChampionCard(__instance.GetSubChampionIndex()).GetID()))
            {
                Log.LogInfo("Subclass champion card already exists.");
                return;
            }
            __instance.AddCardToDeck(subclass.GetChampionCard(__instance.GetSubChampionIndex()), null, false, 0, false, false, true, true);
            Log.LogInfo($"Added subclass champion card: {subclass.GetChampionCard(__instance.GetSubChampionIndex()).GetName()}");
        }
    }

    [HarmonyPatch(typeof(ChampionUpgradeScreen), "ReturnToMap")]
    class OverrideReturnToMap
    {
        public static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("OverrideReturnToMap");
        public static bool Prefix(ChampionUpgradeScreen __instance, SaveManager ___saveManager, String ___targetChampionClassId, GrantableRewardData.Source ___rewardSource, RewardState ___rewardState, Action<GrantResult> ___rewardGrantedCallback)
        {
            if (___saveManager.HasAlliedChampion)
            {
                Log.LogInfo("Allied champion enabled via mutator, skipping return to map override.");
                return true;
            }

            List<CardState> championCards = ___saveManager.GetDeckState().FindAll(cs => cs.IsChampionCard());
            // Get Random Upgrade
            if (___saveManager.GetMainClass() == ___saveManager.GetSubClass() || championCards.Count > 2)
            {
                Log.LogInfo("Same main and allied clan or more than two champion cards detected. Random upgrades will be given instead.");
                foreach (CardState championCard in championCards)
                {
                    if (championCard.GetCardDataID() != ___saveManager.GetMainClass().GetChampionCard(___saveManager.GetMainChampionIndex()).GetID())
                    {
                        CardUpgradeTreeData upgradeTreeData = ChampionUpgradeRewardData.GetUpgradeTree(championCard.GetSpawnCharacterData(), ___saveManager.GetBalanceData());
                        List<CardUpgradeData> upgrades = upgradeTreeData.GetRandomChoices(1, championCard);
                        CardUpgradeState upgrade = new CardUpgradeState();
                        upgrade.Setup(upgrades[0], false);
                        championCard.Upgrade(upgrade, ___saveManager, true);
                        championCard.RemoveEarlierTreeUpgrades(upgrade, upgradeTreeData);
                    }
                }
                return true;
            }

            Log.LogInfo("Returning to map from ChampionUpgradeScreen...");
            if (___targetChampionClassId == ___saveManager.GetMainClass().GetID())
            {
                Log.LogInfo("Setting up for Subclass Champion...");
                CardState subChampionCard = ___saveManager.GetDeckState().Find(cs => cs.IsChampionCard() && cs.GetCardDataID() == ___saveManager.GetSubClass().GetChampionCard(___saveManager.GetSubChampionIndex()).GetID());
                CardUpgradeTreeData upgradeTreeData = ChampionUpgradeRewardData.GetUpgradeTree(subChampionCard.GetSpawnCharacterData(), ___saveManager.GetBalanceData());

                ___rewardState.RemainingUses = 1;

                GrantableRewardData.GrantParams grantParams = new GrantableRewardData.GrantParams
                {
                    source = GrantableRewardData.Source.Map,
                    correspondingReward = ___rewardState
                };

                __instance.Setup(___saveManager.GetSubClass().GetID(), upgradeTreeData, grantParams, ___rewardGrantedCallback);
                Log.LogInfo("Subclass Champion setup complete.");
                return false;
            } else 
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(DeckScreen), "SortCards")]
    class SwapFirstAndSecondChampionCards
    {
        public static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SwapFirstAndSecondChampionCards");
        public static void Postfix(DeckScreen __instance, SaveManager ___saveManager, List<DeckScreen.CardInfo> ___cardInfos, DeckScreen.SortOrder ___sortOrder)
        {
            if (___saveManager.HasAlliedChampion)
            {
                Log.LogInfo("Allied champion enabled via mutator, skipping sort cards override.");
                return;
            }

            if (___cardInfos.Count > 1 && ___sortOrder == DeckScreen.SortOrder.Default)
            {
                DeckScreen.CardInfo firstCard = ___cardInfos[0];
                DeckScreen.CardInfo secondCard = ___cardInfos[1];
                if (firstCard.cardState.IsChampionCard() && secondCard.cardState.IsChampionCard()
                    && ___saveManager.GetSubClass().GetChampionCard(___saveManager.GetSubChampionIndex()).GetID() == firstCard.cardState.GetCardDataID()
                    && ___saveManager.GetMainClass().GetChampionCard(___saveManager.GetMainChampionIndex()).GetID() == secondCard.cardState.GetCardDataID())
                {
                    ___cardInfos[0] = secondCard;
                    ___cardInfos[1] = firstCard;
                    Log.Log(BepInEx.Logging.LogLevel.Info, "Swapped first and second champion cards.");
                }
            }
        }
    }
}
