using System.Reflection;
using Landfall.Haste;
using Landfall.Modding;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using Zorro.Settings;

namespace EndlessTweaker;

[LandfallPlugin]
public class Program
{
    
    private static int extraReward = 0;

    static Program()
    {
        Debug.Log("[Endless Tweaker] Initializing!");
        //new Harmony("Yawrf.EndlessTweaker").PatchAll();


        On.RunHandler.OnLevelCompleted += (orig) =>
        {
            if (RunHandler.isEndless)
            {
                //Debug.Log("<<ET>> Finished Level: " + RunHandler.RunData.currentLevel);
                //Debug.Log("<<ET>> Biome: " + RunHandler.selectedBiome);
                //Debug.Log("<<ET>> Config: " + RunHandler.configOverride);
                //Debug.Log("<<ET>> NodeType: " + RunHandler.RunData.currentNodeType);

                RunHandler.RunData.currentLevelID++;
                RunHandler.RunData.currentLevel++;
                Player.localPlayer.data.resource += Player.localPlayer.data.temporaryResource;

                RunNextScene(false);

                HasteStats.SetStat(HasteStatType.STAT_ENDLESS_HIGHSCORE, RunHandler.RunData.currentLevel, onlyInc: true);
                HasteStats.OnLevelComplete();
            }
            else
            {
                orig();
            }
        };

        On.EndlessAward.Start += (orig, self) =>
        {
            int optionCount = GameHandler.Instance.SettingsHandler.GetSetting<RewardOptionsSetting>().Value;

            int maxItems = GameHandler.Instance.SettingsHandler.GetSetting<MaxItemSetting>().Value;
                if (maxItems < 0) maxItems = int.MaxValue;
            int itemCount = RunHandler.RunData.itemData.Count();
            bool canGetMoreItems = itemCount + 1 < maxItems;

            System.Random currentLevelRandomInstance = new System.Random(RunHandler.GetCurrentLevelSeed(extraReward));
            for (int i = 0; i < optionCount; i++)
            {
                UnlockScreen.me.AddItem(ItemDatabase.GetRandomItem(currentLevelRandomInstance, MinorItemInteraction.MajorOnly, UnlockScreen.me.itemsToAdd));
            }

            UnlockScreen.me.chooseItem = true;
            //UnlockScreen.me.FinishAddingPhase(RunHandler.PlayNextLevel);
                UnlockScreen.me.FinishAddingPhase(() =>
                {
                    if (canGetMoreItems && extraReward > 0)
                    {
                        Debug.Log("<<ET>> ExtraReward: " +  extraReward);
                        extraReward--;
                        SceneManager.LoadScene("EndlessAwardScene");
                    }
                    else
                    {
                        RunNextScene(true);
                    }
                });
        };

        On.RunHandler.TransitionBackToLevelMap += (orig) =>
        {
            if (RunHandler.isEndless)
            {
                RunNextScene(true);
            }
            else orig();
        };
    }

    private static void AddHealthReflect(Player localPlayer, float amount)
    {
        MethodInfo getAddHealthMethod = typeof(Player).GetMethod("AddHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        getAddHealthMethod.Invoke(localPlayer, [ amount ]);
    }
    private static GameObject GetBiomeReflect()
    {
        // Thank you to @stevelion in the Haste Discord for helping me figure this part out!
        MethodInfo getBiomeMethod = typeof(RunConfig).GetMethod("GetBiome", BindingFlags.Instance | BindingFlags.NonPublic);
        return (GameObject)getBiomeMethod.Invoke(RunHandler.config, [ RunHandler.GetCurrentLevelRandomInstance() ]);
    }
    private static void RunScene()
    {
        UI_TransitionHandler.instance.Transition(() =>
        {
            SceneManager.LoadScene("RunScene", LoadSceneMode.Single);
            Debug.Log("Starting level");
        }, "Dots", 0.3f, 0.5f);
    }
    private static void RunNextScene(bool fromAward)
    {
        bool itemsEnabled = GameHandler.Instance.SettingsHandler.GetSetting<ItemsEnabledSetting>().Value == OffOnMode.ON;
        bool immediateItem = GameHandler.Instance.SettingsHandler.GetSetting<ImmediateItemSetting>().Value == OffOnMode.ON;
        int frequency = GameHandler.Instance.SettingsHandler.GetSetting<FrequencySetting>().Value;
        int maxItems = GameHandler.Instance.SettingsHandler.GetSetting<MaxItemSetting>().Value;
            if (maxItems < 0) maxItems = int.MaxValue;
        int itemCount = RunHandler.RunData.itemData.Count();
        bool canGetMoreItems = itemCount < maxItems;

        int challengeChance = GameHandler.Instance.SettingsHandler.GetSetting<ChallengeChanceSetting>().Value;
        bool challengeReward = GameHandler.Instance.SettingsHandler.GetSetting<ChallengeRewardSetting>().Value == OffOnMode.ON;
        int shopChance = challengeChance + GameHandler.Instance.SettingsHandler.GetSetting<ShopChanceSetting>().Value; 
        int restChance = shopChance + GameHandler.Instance.SettingsHandler.GetSetting<RestChanceSetting>().Value;
        int maxWeight = restChance + GameHandler.Instance.SettingsHandler.GetSetting<NormalChanceSetting>().Value; // Normal is the final else, so also acts as MaxWeight

        int roll = (int)(Math.Floor(RunHandler.GetCurrentLevelRandomInstance().NextFloat() * maxWeight));

        bool giveReward = itemsEnabled; // Master Toggle for run-based rewards
            if (RunHandler.RunData.currentLevel == 1) giveReward = giveReward && immediateItem; // If just finished first stage, give item if ImmediateItem setting
            else giveReward = giveReward && (RunHandler.RunData.currentLevel % frequency == 0); // Otherwise check if frequency

        if (canGetMoreItems && RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.Challenge && challengeReward)
        {
            if (!giveReward) giveReward = true; // If just finished Challenge frag and ChallengeReward, override true
            else extraReward++; // If already giving reward, signal to give extra reward
        }

        RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.Default;
        Debug.Log("<<ET>> MaxWeight: " + maxWeight);
        Debug.Log("<<ET>> Rolled: " + roll);

        if (canGetMoreItems && giveReward && !fromAward)
        {
            Debug.Log("<<ET>> Sending to EndlessAward");
            SceneManager.LoadScene("EndlessAwardScene");
        }
        else if (roll < challengeChance)
        {
            Debug.Log("<<ET>> Sending to Challenge");
            RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.Challenge;
            RunHandler.configOverride = (LevelGenConfig)Resources.Load("Ethereal");
            RunScene();
        }
        else if ((roll + challengeChance) < shopChance)
        {
            Debug.Log("<<ET>> Sending to Shop");
            RunHandler.TransitionToShop();
        }
        else if ((roll + challengeChance + shopChance) < restChance)
        {
            Debug.Log("<<ET>> Sending to Rest");
            RunHandler.PlayRestScene();
        }
        else
        {
            Debug.Log("<<ET>> Sending to Default");
            //RunHandler.selectedBiome = RunHandler.config.GetBiome(RunHandler.GetCurrentLevelRandomInstance());
                RunHandler.selectedBiome = GetBiomeReflect();
                RunHandler.configOverride = null;
            //RunHandler.LoadLevelScene();
                RunScene();
        }

        if (RunHandler.InRun && !fromAward) 
        {
            //Debug.Log("<<ET>> Add Health per Level Completed: " + RunHandler.config.addHealthPerLevelCompleted); // 25
            //Debug.Log("<<ET>> Add Life Every N Levels: " + RunHandler.config.addLifeEveryNLevels); // 3

            int stageHealAmount = GameHandler.Instance.SettingsHandler.GetSetting<StageHealSetting>().Value;
            int stageLifeFrequency = GameHandler.Instance.SettingsHandler.GetSetting<StageLifeSetting>().Value;

            if (stageHealAmount > 0)
            {
                AddHealthReflect(Player.localPlayer, stageHealAmount);
            }
            if (stageLifeFrequency > 0 && RunHandler.RunData.currentLevel % stageLifeFrequency == 0)
            {
                Player.localPlayer.EditLives(1);
            }
        }
    }
}


// The HasteSetting attribute is equivalent to
// GameHandler.Instance.SettingsHandler.AddSetting(new HelloSetting());
[HasteSetting]
public class ItemsEnabledSetting : OffOnSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Allow Items in Endless");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override OffOnMode GetDefaultValue() => OffOnMode.ON;
    public override List<LocalizedString> GetLocalizedChoices() => new List<LocalizedString>
    {
        new UnlocalizedString("Disabled"),
        new UnlocalizedString("Enabled")

    };
}
[HasteSetting]
public class ImmediateItemSetting : OffOnSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Give Item after first Fragment");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override OffOnMode GetDefaultValue() => OffOnMode.ON;
    public override List<LocalizedString> GetLocalizedChoices() => new List<LocalizedString>
    {
        new UnlocalizedString("Disabled"),
        new UnlocalizedString("Enabled")

    };
}
[HasteSetting]
public class FrequencySetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Item Frequency");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => 5;
}
[HasteSetting]
public class MaxItemSetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Maximum Items (Negative for No Limit)");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => -1;
}
[HasteSetting]
public class RewardOptionsSetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Reward Items to Choose From");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => 3;
}
[HasteSetting]
public class NormalChanceSetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Normal Chance Weight");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => 100;
}
[HasteSetting]
public class ChallengeChanceSetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Challenge Chance Weight");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => 0;
}
[HasteSetting]
public class ChallengeRewardSetting : OffOnSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Give Item on Challenge Complete (Overrides Allow Items setting)");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override OffOnMode GetDefaultValue() => OffOnMode.ON;
    public override List<LocalizedString> GetLocalizedChoices() => new List<LocalizedString>
    {
        new UnlocalizedString("Disabled"),
        new UnlocalizedString("Enabled")

    };
}
[HasteSetting]
public class ShopChanceSetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Shop Chance Weight");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => 0;
}
[HasteSetting]
public class RestChanceSetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Rest Chance Weight");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => 0;
}
[HasteSetting]
public class StageHealSetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Heal Each Stage");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => 25;
}
[HasteSetting]
public class StageLifeSetting : IntSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Life Regen Frequency");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override int GetDefaultValue() => 3;
}