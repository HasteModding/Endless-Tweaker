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
public class Program
{
    
    private static int extraReward = 0;
    private static int shopCount = 0;
    private static int restCount = 0;
    private static bool inAward = false;

    static Program()
    { 
        Debug.Log("[Endless Tweaker] Initializing!");

        On.RunHandler.StartNewRun += (orig, setConfig, shardID, seed) =>
        {
            resetEndlessBossStats();
            orig(setConfig, shardID, seed);
        };

        On.RunHandler.WinRun += (orig, transitionOutOverride) =>
        {
            if (RunHandler.isEndless)
            {
                RunHandler.OnLevelCompleted();
            }
            else
            {
                orig(transitionOutOverride);
            }
        };

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

                RunNextScene();

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
            inAward = true;
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
                        UI_TransitionHandler.instance.Transition(RunNextScene, "Dots", 0.3f, 0.5f);
                        //RunNextScene();
                    }
                });
        };

        On.RunHandler.TransitionBackToLevelMap += (orig) =>
        {
            if (RunHandler.isEndless)
            {
                MusicPlayer.Instance.ChangePlaylist(RunHandler.RunData.runConfig.musicPlaylist);
                UI_TransitionHandler.instance.Transition(RunNextScene, "Dots", 0.3f, 0.5f);
                //RunNextScene(true);
            }
            else orig();
        };
    }

    private static void AddHealthReflect(Player localPlayer, float amount)
    {
        MethodInfo getAddHealthMethod = typeof(Player).GetMethod("AddHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        getAddHealthMethod.Invoke(localPlayer, [ amount ]);
    }
    private static void LoadLevelSceneReflect()
    {
        MethodInfo LoadLevelSceneMethod = typeof(RunHandler).GetMethod("LoadLevelScene", BindingFlags.Static | BindingFlags.NonPublic);
        LoadLevelSceneMethod.Invoke(null, []);
    }
    private static GameObject GetBiomeReflect()
    {
        // Thank you to @stevelion in the Haste Discord for helping me figure this part out!
        MethodInfo getBiomeMethod = typeof(RunConfig).GetMethod("GetBiome", BindingFlags.Instance | BindingFlags.NonPublic);
        return (GameObject)getBiomeMethod.Invoke(RunHandler.config, [ RunHandler.GetCurrentLevelRandomInstance() ]);
    }
    private static bool[] jumper = [false, false];
    private static bool[] convoy = [false, false];
    private static bool[] snake  = [false, false];
    private static void resetEndlessBossStats()
    {
        jumper = [false, false];
        convoy = [false, false];
        snake  = [false, false];
    }
    private static void GoToBoss()
    {
        int jumperWeight = GameHandler.Instance.SettingsHandler.GetSetting<JumperWeightSetting>().Value;
        int convoyWeight = jumperWeight + GameHandler.Instance.SettingsHandler.GetSetting<ConvoyWeightSetting>().Value;
        int snakeWeight = convoyWeight + GameHandler.Instance.SettingsHandler.GetSetting<SnakeWeightSetting>().Value;

        string[] bossScenes = ["Challenge_ForestBoss", "Challenge_DesertBoss", "Challenge_SnakeBoss"];

        int roll = (int)Math.Floor(RunHandler.GetCurrentLevelRandomInstance().NextFloat() * snakeWeight);
        string boss;
        int level = 1;

        if(roll < jumperWeight)
        {
            boss = bossScenes[0];
        } else if (roll < convoyWeight)
        {
            boss = bossScenes[1];
        } else
        {
            boss = bossScenes[2];
        }

        switch (boss)
        {
            case "Challenge_ForestBoss":
                {
                    level += jumper[0] ? 1 : 0;
                    level += jumper[1] ? 1 : 0;
                }
                break;
            case "Challenge_DesertBoss":
                {
                    level += convoy[0] ? 1 : 0;
                    level += convoy[1] ? 1 : 0;
                }
                break;
            case "Challenge_SnakeBoss":
                {
                    level += snake[0] ? 1 : 0;
                    level += snake[1] ? 1 : 0;
                }
                break;
        }

        level = (int)Math.Floor(RunHandler.GetCurrentLevelRandomInstance().NextFloat() * level);

        if (level < 2)
        {
            switch (boss)
            {
                case "Challenge_ForestBoss":
                    {
                        jumper[level] = true;
                    }
                    break;
                case "Challenge_DesertBoss":
                    {
                        convoy[level] = true;
                    }
                    break;
                case "Challenge_SnakeBoss":
                    {
                        snake[level] = true;
                    }
                    break;
            }
        }

        RunHandler.config.bossScene = boss;
        RunHandler.config.bossTeir = level;

        RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.Boss;
        RunHandler.TransitionToBoss();
    }
    private static void RunNextScene()
    {
        bool fromAward = inAward || RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.Shop || RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.RestStop;
            inAward = false;
        bool itemsEnabled = GameHandler.Instance.SettingsHandler.GetSetting<ItemsEnabledSetting>().Value == OffOnMode.ON;
        bool immediateItem = GameHandler.Instance.SettingsHandler.GetSetting<ImmediateItemSetting>().Value == OffOnMode.ON;
        int frequency = GameHandler.Instance.SettingsHandler.GetSetting<FrequencySetting>().Value;
        int maxItems = GameHandler.Instance.SettingsHandler.GetSetting<MaxItemSetting>().Value;
            if (maxItems < 0) maxItems = int.MaxValue;
        int itemCount = RunHandler.RunData.itemData.Count();
        bool canGetMoreItems = itemCount < maxItems;

        bool bossInterval = GameHandler.Instance.SettingsHandler.GetSetting<BossMethodSetting>().Value == OffOnMode.ON;
        int bossNum = GameHandler.Instance.SettingsHandler.GetSetting<BossNumberSetting>().Value;
        int bossMinFloors = GameHandler.Instance.SettingsHandler.GetSetting<BossMinFloorsSetting>().Value;
        bool bossElegible = RunHandler.RunData.currentLevel >= bossMinFloors;
            if(!bossElegible && !bossInterval) bossNum = 0;

        int challengeChance = GameHandler.Instance.SettingsHandler.GetSetting<ChallengeChanceSetting>().Value;
        bool challengeReward = GameHandler.Instance.SettingsHandler.GetSetting<ChallengeRewardSetting>().Value == OffOnMode.ON;
        bool bossReward = GameHandler.Instance.SettingsHandler.GetSetting<BossRewardSetting>().Value == OffOnMode.ON;
        if (!bossInterval) bossNum += challengeChance;
        int shopChance = ( bossInterval ? challengeChance : bossNum ) + GameHandler.Instance.SettingsHandler.GetSetting<ShopChanceSetting>().Value; 
        int restChance = shopChance + GameHandler.Instance.SettingsHandler.GetSetting<RestChanceSetting>().Value;
        int maxWeight = restChance + GameHandler.Instance.SettingsHandler.GetSetting<NormalChanceSetting>().Value; // Normal is the final else, so also acts as MaxWeight

        int roll = (int)(Math.Floor(new System.Random(RunHandler.GetCurrentLevelSeed(shopCount + restCount)).NextFloat() * maxWeight));

        bool giveReward = itemsEnabled; // Master Toggle for run-based rewards
            if (RunHandler.RunData.currentLevel == 1) giveReward = giveReward && immediateItem; // If just finished first stage, give item if ImmediateItem setting
            else giveReward = giveReward && (RunHandler.RunData.currentLevel % frequency == 0); // Otherwise check if frequency

        if (canGetMoreItems && RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.Challenge && challengeReward)
        {
            if (!giveReward) giveReward = true; // If just finished Challenge frag and ChallengeReward, override true
            else extraReward++; // If already giving reward, signal to give extra reward
        } 
        if (canGetMoreItems && RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.Boss && bossReward)
        {
            if (!giveReward) giveReward = true; // If just finished Boss frag and BossReward, override true
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
        else if (bossInterval && bossElegible && RunHandler.RunData.currentLevel % bossNum == 0)
        {
            Debug.Log("<<ET>> Sending to Boss via Interval");
            GoToBoss();
        }
        else if (roll < challengeChance)
        {
            Debug.Log("<<ET>> Sending to Challenge");
            RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.Challenge;
            RunHandler.PlayChallenge();
        }
        else if (!bossInterval && bossElegible && roll < bossNum)
        {
            Debug.Log("<<ET>> Sending to Boss via Chance");
            GoToBoss();
        }
        else if ((roll) < shopChance)
        {
            Debug.Log("<<ET>> Sending to Shop");
            shopCount++;
            RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.Shop;
            RunHandler.TransitionToShop();
        }
        else if ((roll) < restChance)
        {
            Debug.Log("<<ET>> Sending to Rest");
            restCount++;
            RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.RestStop;
            RunHandler.PlayRestScene();
        }
        else
        {
            Debug.Log("<<ET>> Sending to Default");
            //RunHandler.selectedBiome = RunHandler.config.GetBiome(RunHandler.GetCurrentLevelRandomInstance());
                RunHandler.selectedBiome = GetBiomeReflect();
                RunHandler.configOverride = null;
            //RunHandler.LoadLevelScene();
                LoadLevelSceneReflect();
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
public class ItemSettingsCollapsible : CollapsibleSetting, IExposedSetting
{
    public string GetCategory() => "EndlessTweaker";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Item Settings");
    public ItemsEnabledSetting ItemsEnabledSetting = new ItemsEnabledSetting();
    public ImmediateItemSetting ImmediateItemSetting = new ImmediateItemSetting();
    public FrequencySetting FrequencySetting = new FrequencySetting();
    public MaxItemSetting MaxItemSetting = new MaxItemSetting();
    public RewardOptionsSetting RewardOptionsSetting = new RewardOptionsSetting();
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
    public StageHealSetting StageHeal = new StageHealSetting();
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