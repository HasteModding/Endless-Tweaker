using System.Reflection;
using Landfall.Haste;
using Landfall.Haste.Music;
using Landfall.Modding;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using Zorro.Settings;
using SettingsLib.Settings;

namespace EndlessTweaker;

[LandfallPlugin]
public class EndlessTweakerMain
{
    static EndlessTweakerMain()
    { 
        Debug.Log("[Endless Tweaker] Initializing!");
        Debug.Log(typeof(EndlessTweakerMain).AssemblyQualifiedName);


        On.RunHandler.StartNewRun += (orig, setConfig, shardID, seed, setRunConfigRuntimeData) =>
        {
            if (!new OptionsCollector().modEnabled) orig(setConfig, shardID, seed, setRunConfigRuntimeData);
            else
            {
                Debug.Log("<<ET>> Captured RunHandler.StartNewRun");
                EndlessSequencer.ResetEndlessBossStats();
                orig(setConfig, shardID, seed, setRunConfigRuntimeData);
            }
        };

        On.RunHandler.CompleteRun += (orig, win, transitionOutOverride, transitionEffectDelay) =>
        {
            if (!new OptionsCollector().modEnabled || !win) orig(win, transitionOutOverride, transitionEffectDelay);
            else if (RunHandler.config.isEndless)
            {
                Debug.Log("<<ET>> Captured RunHandler.CompleteRun");
                Debug.Log("<<ET>> Reroute to RunHandler.TransitionOnLevelCompleted for Rehook");
                RunHandler.TransitionOnLevelCompleted();
            }
            else
            {
                orig(win, transitionOutOverride, transitionEffectDelay);
            }
        };

        On.RunHandler.TransitionOnLevelCompleted += (orig) =>
        {
            if (!new OptionsCollector().modEnabled || !RunHandler.config.isEndless) orig();
            else
            {
                Debug.Log("<<ET>> Captured RunHandler.TransitionOnLevelCompleted");
                Debug.Log("<<ET>> Reroute to EndlessSequencer.HandleEndOfLevel");
                EndlessSequencer.HandleEndOfLevel();
            }
        };
        On.RunHandler.TransitionBackToLevelMap += (orig) =>
        {
            if (!new OptionsCollector().modEnabled || !RunHandler.config.isEndless) orig();
            else
            {
                Debug.Log("<<ET>> Captured RunHandler.TransitionBackToLevelMap");
                Debug.Log("<<ET>> Reroute to EndlessSequencer.HandleBackToLevelMap");
                EndlessSequencer.HandleBackToLevelMap();
            }
        };

        On.UnlockScreen.GetRerollCost += (orig, self) =>
        {
            OptionsCollector options = new();
            int rerollCount = (int) typeof(UnlockScreen).GetField("rerollCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(self);

            return options.rerollBaseCost + (options.rerollIncCost * rerollCount);
        };

        On.EndlessAward.Start += (orig, self) =>
        {
            if (!new OptionsCollector().modEnabled) orig(self);
            else
            {
                Debug.Log("<<ET>> Captured EndlessAward.Start");
                Player localPlayer = Player.localPlayer;
                EndlessSequencer.inAward = true;
                OptionsCollector options = new();
                int itemCount = RunHandler.RunData.itemData.Count();
                bool canGetMoreItems = itemCount + 1 < options.maxItems;

                System.Random currentLevelRandomInstance = EndlessSequencer.getRandom();
                for (int i = 0; i < options.rewardOptions; i++)
                {
                    UnlockScreen.me.AddItem(ItemDatabase.GetRandomItem(localPlayer, currentLevelRandomInstance, GetRandomItemFlags.Major, TagInteraction.None, null, UnlockScreen.me.itemsToAdd));
                }

                UnlockScreen.me.chooseItem = true;
                UnlockScreen.me.FinishAddingPhase(localPlayer, () =>
                {
                    EndlessSequencer.rewardCount--;
                    Debug.Log("<<ET>> EndlessAward Selected");
                    if (canGetMoreItems && EndlessSequencer.rewardCount > 0)
                    {
                        Debug.Log("<<ET>> RewardCount Remaining: " + EndlessSequencer.rewardCount);
                        SceneManager.LoadScene("EndlessAwardScene");
                    }
                    else
                    {
                        Debug.Log("<<ET>> ExtraReward Finished: Passing back to RunHandler.TransitionBackToLevelMap for re-hooking");
                        RunHandler.TransitionBackToLevelMap();
                    }
                });
            }
        };
        On.UnlockScreen.RerollItems += (orig, self) =>
        {
            if (!new OptionsCollector().modEnabled || !RunHandler.config.isEndless) orig(self);
            else
            {
                Debug.Log("<<ET>> Captured UnlockScreen.RerollItems");
                // Trying to replicate the effects of RerollItems requires so many Reflections as to be obscene.
                // We are instead letting it do its thing and then overwriting the items afterward.
                orig(self);
                self.DeActivateItemButtons();
                self.ResetState();

                Player localPlayer = Player.localPlayer;
                EndlessSequencer.inAward = true;
                OptionsCollector options = new();
                int itemCount = RunHandler.RunData.itemData.Count();
                bool canGetMoreItems = itemCount + 1 < options.maxItems;

                System.Random currentLevelRandomInstance = EndlessSequencer.getRandom();
                for (int i = 0; i < options.rewardOptions; i++)
                {
                    self.AddItem(ItemDatabase.GetRandomItem(localPlayer, currentLevelRandomInstance, GetRandomItemFlags.Major, TagInteraction.None, null, self.itemsToAdd));
                }

                self.chooseItem = true;
                self.FinishAddingPhase(localPlayer, () =>
                {
                    EndlessSequencer.rewardCount--;
                    Debug.Log("<<ET>> EndlessAward Selected");
                    if (canGetMoreItems && EndlessSequencer.rewardCount > 0)
                    {
                        Debug.Log("<<ET>> RewardCount Remaining: " + EndlessSequencer.rewardCount);
                        SceneManager.LoadScene("EndlessAwardScene");
                    }
                    else
                    {
                        Debug.Log("<<ET>> ExtraReward Finished: Passing back to RunHandler.TransitionBackToLevelMap for re-hooking");
                        RunHandler.TransitionBackToLevelMap();
                    }
                });
                self.ActivateItemButtons();
            }
        };
    }
}

internal class OptionsCollector
{
    public bool modEnabled = GameHandler.Instance.SettingsHandler.GetSetting<ModToggleSetting>().Value == OffOnMode.ON;

    // Item Settings Collapsible
    public bool itemsEnabled = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().ItemsEnabledSetting.Value == OffOnMode.ON;
    public bool immediateItem = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().ImmediateItemSetting.Value == OffOnMode.ON;
    public int itemFrequency = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().FrequencySetting.Value;
    public int maxItems = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().MaxItemSetting.Value;
    public int rewardOptions = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().RewardOptionsSetting.Value, 1);
    public int rerollBaseCost = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().RerollBaseCostSetting.Value, 0);
    public int rerollIncCost = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().RerollIncreaseCostSetting.Value, 0);
    public bool challengeReward = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().ChallengeRewardSetting.Value == OffOnMode.ON;
    public bool bossReward = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().BossRewardSetting.Value == OffOnMode.ON;

    // Fragment Collapsible
    public int normalWeight = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().NormalChanceSetting.Value, 0);
    public int challengeWeight = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().ChallengeChanceSetting.Value, 0);
    public bool bossInterval = GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().BossMethodSetting.Value == OffOnMode.ON;
    public int bossNum = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().BossNumberSetting.Value, 0);
    public int shopWeight = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().ShopChanceSetting.Value, 0);
    public int restWeight = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().RestChanceSetting.Value, 0);
    public bool diffControl = GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().DifficultyControlSetting.Value == OffOnMode.ON;
    public int initialDiff = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().InitialDifficultySetting.Value, 0);
    public DiffScaleEnum diffScale = GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().DifficultyScaleMethodSetting.Value;
    public int diffScaleFreq = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().DifficultyScaleFrequencySetting.Value, 0);
    public int diffScaleRate = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().DifficultyScaleRateSetting.Value, 0);

    // Boss Settings Collapsible
    public int bossMinFloors = GameHandler.Instance.SettingsHandler.GetSetting<BossSettingsCollapsible>().BossMinFloorsSetting.Value;
    public int jumperWeight = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<BossSettingsCollapsible>().JumperWeightSetting.Value, 0);
    public int convoyWeight = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<BossSettingsCollapsible>().ConvoyWeightSetting.Value, 0);
    public int snakeWeight = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<BossSettingsCollapsible>().SnakeWeightSetting.Value, 0);

    // Healing Collapsible
    public int stageHeal = GameHandler.Instance.SettingsHandler.GetSetting<HealingCollapsible>().StageHealSetting.Value;
    public int stageLife = GameHandler.Instance.SettingsHandler.GetSetting<HealingCollapsible>().StageLifeSetting.Value;

    public OptionsCollector()
    {
        if (maxItems < 0) maxItems = int.MaxValue;
        if (itemFrequency < 1) itemFrequency = int.MaxValue;
    }
}

// The HasteSetting attribute is equivalent to
// GameHandler.Instance.SettingsHandler.AddSetting(new HelloSetting());
[HasteSetting]
public class ModToggleSetting : OffOnSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Enable Mod?");
    public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
    protected override OffOnMode GetDefaultValue() => OffOnMode.ON;
    public override List<LocalizedString> GetLocalizedChoices() => new List<LocalizedString>
    {
        new UnlocalizedString("Disabled"),
        new UnlocalizedString("Enabled")

    };
}
[HasteSetting]
public class ItemSettingsCollapsible : CollapsibleSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Item Settings");
    public ItemsEnabledSetting ItemsEnabledSetting = new ItemsEnabledSetting();
    public ImmediateItemSetting ImmediateItemSetting = new ImmediateItemSetting();
    public FrequencySetting FrequencySetting = new FrequencySetting();
    public MaxItemSetting MaxItemSetting = new MaxItemSetting();
    public RewardOptionsSetting RewardOptionsSetting = new RewardOptionsSetting();
    public RerollBaseCostSetting RerollBaseCostSetting = new RerollBaseCostSetting();
    public RerollIncreaseCostSetting RerollIncreaseCostSetting = new RerollIncreaseCostSetting();
    public ChallengeRewardSetting ChallengeRewardSetting = new ChallengeRewardSetting();
    public BossRewardSetting BossRewardSetting = new BossRewardSetting();
}
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
    public class FrequencySetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Item Frequency");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 5;
    }
    public class MaxItemSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Maximum Items (Negative for No Limit)");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => -1;
    }
    public class RewardOptionsSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Reward Items to Choose From");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 3;
    }
    public class RerollBaseCostSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Base Reroll Cost");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 1000;
    }
    public class RerollIncreaseCostSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Reroll Additional Cost per Reroll");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 500;
    }
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
    public class BossRewardSetting : OffOnSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Give Item on Boss Complete (Overrides Allow Items setting)");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override OffOnMode GetDefaultValue() => OffOnMode.ON;
        public override List<LocalizedString> GetLocalizedChoices() => new List<LocalizedString>
        {
            new UnlocalizedString("Disabled"),
            new UnlocalizedString("Enabled")

        };
    }
[HasteSetting]
public class FragmentCollapsible : CollapsibleSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Fragment Settings");
    public NormalChanceSetting NormalChanceSetting = new NormalChanceSetting();
    public ChallengeChanceSetting ChallengeChanceSetting = new ChallengeChanceSetting();
    public BossMethodSetting BossMethodSetting = new BossMethodSetting();
    public BossNumberSetting BossNumberSetting = new BossNumberSetting();
    public ShopChanceSetting ShopChanceSetting = new ShopChanceSetting();
    public RestChanceSetting RestChanceSetting = new RestChanceSetting();
    public DifficultyControlSetting DifficultyControlSetting = new DifficultyControlSetting();
    public InitialDifficultySetting InitialDifficultySetting = new InitialDifficultySetting();
    public DifficultyScaleMethodSetting DifficultyScaleMethodSetting = new DifficultyScaleMethodSetting();
    public DifficultyScaleFrequencySetting DifficultyScaleFrequencySetting = new DifficultyScaleFrequencySetting();
    public DifficultyScaleRateSetting DifficultyScaleRateSetting = new DifficultyScaleRateSetting();
}
    public class NormalChanceSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Normal Chance Weight");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 100;
    }
    public class ChallengeChanceSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Challenge Chance Weight");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 0;
    }
    public class BossMethodSetting : OffOnSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Should Boss stages be chance or interval?");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
        public override List<LocalizedString> GetLocalizedChoices() => new List<LocalizedString>
        {
            new UnlocalizedString("Chance"),
            new UnlocalizedString("Interval")

        };
    }
    public class BossNumberSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Boss Weight/Interval");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 0;
    }
    public class ShopChanceSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Shop Chance Weight");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 0;
    }
    public class RestChanceSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Rest Chance Weight");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 0;
    }
    public class DifficultyControlSetting : OffOnSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Modify Difficulty Numbers?");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
        public override List<LocalizedString> GetLocalizedChoices() => new List<LocalizedString>
        {
            new UnlocalizedString("No"),
            new UnlocalizedString("Yes")

        };
    }
    public class InitialDifficultySetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Initial Difficulty Setting");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 10;
    }
    public enum DiffScaleEnum
    {
        Stage = 0,
        Item = 1,
        Boss = 2
    }
    public class DifficultyScaleMethodSetting : EnumSetting<DiffScaleEnum>, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Difficulty Scaling Method");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override DiffScaleEnum GetDefaultValue() => DiffScaleEnum.Stage;
        public override List<LocalizedString> GetLocalizedChoices() => new List<LocalizedString>()
        {
            new UnlocalizedString("Stage"),
            new UnlocalizedString("Item"),
            new UnlocalizedString("Boss")
        };
    }
    public class DifficultyScaleFrequencySetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Difficulty Increase Frequency");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 5;
    }
    public class DifficultyScaleRateSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Difficulty Increase Amount");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 1;
}
[HasteSetting] 
public class BossSettingsCollapsible : CollapsibleSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Boss Settings");
    public BossMinFloorsSetting BossMinFloorsSetting = new BossMinFloorsSetting();
    public JumperWeightSetting JumperWeightSetting = new JumperWeightSetting();
    public ConvoyWeightSetting ConvoyWeightSetting = new ConvoyWeightSetting();
    public SnakeWeightSetting SnakeWeightSetting = new SnakeWeightSetting();
}
    public class BossMinFloorsSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Minimum Floors before Bosses (will override Interval)");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 5;
    }
    public class JumperWeightSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Jumper Boss Weight");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 10;
    }
    public class ConvoyWeightSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Convoy Boss Weight");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 10;
    }
    public class SnakeWeightSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Snake Boss Weight");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 10;
    }
[HasteSetting]
public class HealingCollapsible : CollapsibleSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Healing Settings");
    public StageHealSetting StageHealSetting = new StageHealSetting();
    public StageLifeSetting StageLifeSetting = new StageLifeSetting();
}
    public class StageHealSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Heal Each Stage");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 25;
    }
    public class StageLifeSetting : IntSetting, IExposedSetting
    {
        public string GetCategory() => "EndlessTweaker";
        public LocalizedString GetDisplayName() => new UnlocalizedString("Life Regen Frequency");
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 3;
    }

/**
[HasteSetting]
public class ResetSetting : ButtonSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Reset Settings.\nValues do not visually update until settings is closed and reopened.");
    public override String GetButtonText() => "Reset";
    public override void OnClicked(ISettingHandler settingHandler)
    {
        // Item Settings Collapsible
        GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().ItemsEnabledSetting.SetValue(OffOnMode.ON, settingHandler, false);
        GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().ImmediateItemSetting.SetValue(OffOnMode.ON, settingHandler, false);
        GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().FrequencySetting.SetValue(5, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().MaxItemSetting.SetValue(-1, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().RewardOptionsSetting.SetValue(3, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().ChallengeRewardSetting.SetValue(OffOnMode.ON, settingHandler, false);
        GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().BossRewardSetting.SetValue(OffOnMode.ON, settingHandler, false);

        // Fragment Collapsible
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().NormalChanceSetting.SetValue(100, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().ChallengeChanceSetting.SetValue(0, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().BossMethodSetting.SetValue(OffOnMode.ON, settingHandler, false);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().BossNumberSetting.SetValue(0, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().ShopChanceSetting.SetValue(0, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().RestChanceSetting.SetValue(0, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().DifficultyControlSetting.SetValue(OffOnMode.OFF, settingHandler, false);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().InitialDifficultySetting.SetValue(10, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().DifficultyScaleMethodSetting.SetValue(DiffScaleEnum.Stage, settingHandler, false);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().DifficultyScaleFrequencySetting.SetValue(5, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<FragmentCollapsible>().DifficultyScaleRateSetting.SetValue(1, settingHandler);

        // Boss Settings Collapsible
        GameHandler.Instance.SettingsHandler.GetSetting<BossSettingsCollapsible>().BossMinFloorsSetting.SetValue(5, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<BossSettingsCollapsible>().JumperWeightSetting.SetValue(10, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<BossSettingsCollapsible>().ConvoyWeightSetting.SetValue(10, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<BossSettingsCollapsible>().SnakeWeightSetting.SetValue(10, settingHandler);

        // Healing Collapsible
        GameHandler.Instance.SettingsHandler.GetSetting<HealingCollapsible>().StageHealSetting.SetValue(25, settingHandler);
        GameHandler.Instance.SettingsHandler.GetSetting<HealingCollapsible>().StageLifeSetting.SetValue(3, settingHandler);
    }
}
*/