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
    private static int bossCount = 0;
    private static bool inAward = false;
    //private static string targetBiome = "Forest";

    static Program()
    { 
        Debug.Log("[Endless Tweaker] Initializing!");

        On.RunHandler.StartNewRun += (orig, setConfig, shardID, seed) =>
        {
            resetEndlessBossStats();
            orig(setConfig, shardID, seed);
            Debug.Log("<<ET>> Diff Start: " + RunHandler.config.startDifficulty);
            Debug.Log("<<ET>> Diff End: " + RunHandler.config.endDifficulty);
            Debug.Log("<<ET>> Diff Bump: " + RunHandler.config.keepRunningDifficultyIncreasePerLevel);
            Debug.Log("<<ET>> Speed Bump: " + RunHandler.config.keepRunningSpeedIncreasePerLevel);
        };

        On.RunHandler.WinRun += (orig, transitionOutOverride) =>
        {
            if (RunHandler.config.isEndless)
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
            if (RunHandler.config.isEndless)
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

        //On.RunConfig.GetBiome += (orig, self, rand) =>
        //{
        //    if (self.isEndless)
        //    {
        //        orig(self, rand); // Per @Stevelion's suggestion, still calling the function for purposes of letting other mods hook it if they want to
        //        return self.categories.Find(lgc => lgc.name == targetBiome).biome;
        //    }
        //    else
        //    {
        //        return orig(self, rand);
        //    }
        //};

        On.EndlessAward.Start += (orig, self) =>
        {
            inAward = true;
            OptionsCollector options = new();
            int itemCount = RunHandler.RunData.itemData.Count();
            bool canGetMoreItems = itemCount + 1 < options.maxItems;

            System.Random currentLevelRandomInstance = new System.Random(RunHandler.GetCurrentLevelSeed(extraReward));
            for (int i = 0; i < options.rewardOptions; i++)
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
            if (RunHandler.config.isEndless)
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
        bossCount = 0;
    }

    private static void GoToNormal()
    {
        Debug.Log("<<ET>> Sending to Normal Fragment");
        RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.Default;
        //RunHandler.LoadLevelScene();
            LoadLevelSceneReflect();
    }
    private static void GoToChallenge()
    {
        Debug.Log("<<ET>> Sending to Challenge Fragment");
        RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.Challenge;
        //RunHandler.PlayChallenge();
            RunHandler.configOverride = (LevelGenConfig)Resources.Load("Ethereal");
            LoadLevelSceneReflect();
    }
    private static void GoToShop()
    {
        Debug.Log("<<ET>> Sending to Shop");
        shopCount++;
        RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.Shop;
        //RunHandler.TransitionToShop();
            HasteStats.AddStat(HasteStatType.STAT_SHOPS_VISITED, 1);
            SceneManager.LoadScene("ShopScene", LoadSceneMode.Single);
    }
    private static void GoToRest()
    {
        Debug.Log("<<ET>> Sending to Rest");
        restCount++;
        RunHandler.RunData.currentNodeType = LevelSelectionNode.NodeType.RestStop;
        //RunHandler.PlayRestScene();
            HasteStats.AddStat(HasteStatType.STAT_REST_VISITED, 1);
            SceneManager.LoadScene("RestScene_Current", LoadSceneMode.Single);
    }
    private static void GoToBoss(bool interval = false)
    {
        if(interval) Debug.Log("<<ET>> Sending to Boss via Interval");
        else Debug.Log("<<ET>> Sending to Boss via Chance");
        bossCount++;
        OptionsCollector options = new();

        string[] bossScenes = ["Challenge_ForestBoss", "Challenge_DesertBoss", "Challenge_SnakeBoss"];

        WeightedFunc<string> wf = new();
        if (options.jumperWeight > 0) wf.Add(options.jumperWeight, () => bossScenes[0]);
        if (options.convoyWeight > 0) wf.Add(options.convoyWeight, () => bossScenes[1]);
        if (options.snakeWeight > 0) wf.Add(options.snakeWeight, () => bossScenes[2]);
        String? boss = wf.Run(RunHandler.GetCurrentLevelRandomInstance());
        int level = 1;

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
        //RunHandler.TransitionToBoss();
            SceneManager.LoadScene(RunHandler.config.bossScene, LoadSceneMode.Single);
    }
    private static void RunNextScene()
    {
        OptionsCollector options = new OptionsCollector();
        bool fromAward = inAward || RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.Shop || RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.RestStop;
            inAward = false;
        int itemCount = RunHandler.RunData.itemData.Count();
        bool canGetMoreItems = itemCount < options.maxItems;

        bool bossEligible = RunHandler.RunData.currentLevel >= options.bossMinFloors;
            if(!bossEligible && !options.bossInterval) options.bossNum = 0;

        // Determine if Giving Award, and how many
        bool giveReward = options.itemsEnabled; // Master Toggle for run-based rewards
            if (RunHandler.RunData.currentLevel == 1) giveReward = giveReward && options.immediateItem; // If just finished first stage, give item if ImmediateItem setting
            else giveReward = giveReward && (RunHandler.RunData.currentLevel % options.itemFrequency == 0); // Otherwise check if frequency
        
        if (canGetMoreItems && RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.Challenge && options.challengeReward)
        {
            if (!giveReward) giveReward = true; // If just finished Challenge frag and ChallengeReward, override true
            else extraReward++; // If already giving reward, signal to give extra reward
        } 
        if (canGetMoreItems && RunHandler.RunData.currentNodeType == LevelSelectionNode.NodeType.Boss && options.bossReward)
        {
            if (!giveReward) giveReward = true; // If just finished Boss frag and BossReward, override true
            else extraReward++; // If already giving reward, signal to give extra reward
        }

        // Determine Difficulty Modification
        if(options.diffControl)
        {
            int diff = options.initialDiff;
            if(options.diffScaleFreq > 0) switch(options.diffScale)
                {
                    case DiffScaleEnum.Stage: diff += options.diffScaleRate * (RunHandler.RunData.currentLevel / options.diffScaleFreq);
                            break;
                    case DiffScaleEnum.Item: diff += options.diffScaleRate * (itemCount / options.diffScaleFreq);
                            break;
                    case DiffScaleEnum.Boss: diff += options.diffScaleRate * (bossCount / options.diffScaleFreq);
                            break;
                }
            RunHandler.config.startDifficulty = diff;
            RunHandler.config.endDifficulty = diff;
        }

        // Determine if Giving Award
        if (canGetMoreItems && giveReward && !fromAward)
        {
            Debug.Log("<<ET>> Sending to EndlessAward");
            SceneManager.LoadScene("EndlessAwardScene");
        }
        else if (options.bossInterval && bossEligible && RunHandler.RunData.currentLevel % options.bossNum == 0)
        {
            GoToBoss(true);
        }
        else
        {
            WeightedAction wa = new WeightedAction();
            if (options.normalWeight > 0) wa.Add(options.normalWeight, GoToNormal);
            if (options.challengeWeight > 0) wa.Add(options.challengeWeight, GoToChallenge);
            if (!options.bossInterval && options.bossNum > 0) wa.Add(options.bossNum, () => GoToBoss(false));
            if (options.shopWeight > 0) wa.Add(options.shopWeight, GoToShop);
            if (options.restWeight > 0) wa.Add(options.restWeight, GoToRest);
            wa.Run(new System.Random(RunHandler.GetCurrentLevelSeed(shopCount + restCount)));
        }

        if (RunHandler.InRun && !fromAward) 
        {
            int stageHealAmount = GameHandler.Instance.SettingsHandler.GetSetting<HealingCollapsible>().StageHealSetting.Value;
            int stageLifeFrequency = GameHandler.Instance.SettingsHandler.GetSetting<HealingCollapsible>().StageLifeSetting.Value;

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

internal class WeightedAction
{
    public List<int> weights = [];
    public List<Action> funcs = [];

    public void Add(int weight, Action func)
    {
        weights.Add(weight);
        funcs.Add(func);
    }

    public void Run(System.Random rand)
    {
        int total = 0;
        weights.ForEach(i => total += i);

        int roll = rand.Next(total);
        Debug.Log("<<ET>> WeightedAction Total: " + total);
        Debug.Log("<<ET>> WeightedAction roll: " + roll);
        for (int i = 0; i < weights.Count; ++i)
        {
            roll -= weights[i];
            if(roll < 0)
            {
                funcs[i]();
                return;
            }
        }
        Debug.Log("<<ET>> Reached end of WeightedAction without running anything.");
    }
}
internal class WeightedFunc<T>
{
    public List<int> weights = [];
    public List<Func<T>> funcs = [];

    public void Add(int weight, Func<T> func)
    {
        weights.Add(weight);
        funcs.Add(func);
    }

    public T? Run(System.Random rand)
    {
        int total = 0;
        weights.ForEach(i => total += i);

        int roll = rand.Next(total);
        Debug.Log("<<ET>> WeightedFunc Total: " + total);
        Debug.Log("<<ET>> WeightedFunc roll: " + roll);
        for (int i = 0; i < weights.Count; ++i)
        {
            roll -= weights[i];
            if(roll < 0)
            {
                return funcs[i]();
            }
        }
        Debug.Log("<<ET>> Reached end of WeightedFunc without running anything.");
        return default;
    }
}
internal class OptionsCollector
{
    // Item Settings Collapsible
    public bool itemsEnabled = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().ItemsEnabledSetting.Value == OffOnMode.ON;
    public bool immediateItem = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().ImmediateItemSetting.Value == OffOnMode.ON;
    public int itemFrequency = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().FrequencySetting.Value;
    public int maxItems = GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().MaxItemSetting.Value;
    public int rewardOptions = Math.Max(GameHandler.Instance.SettingsHandler.GetSetting<ItemSettingsCollapsible>().RewardOptionsSetting.Value, 1);
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