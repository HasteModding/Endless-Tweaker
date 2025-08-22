using System.Reflection;
using Landfall.Haste;
using Landfall.Haste.Music;
using Landfall.Modding;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using Zorro.Settings;
using SettingsLib.Settings;
using Steamworks;
using Mono.WebBrowser;

namespace EndlessTweaker;

[LandfallPlugin]
public class EndlessTweakerMain
{
    
    private static int extraReward = 0;
    private static int shopCount = 0;
    private static int restCount = 0;
    private static int bossCount = 0;
    private static bool inAward = false;
    //private static string targetBiome = "Forest";

    static EndlessTweakerMain()
    { 
        Debug.Log("[Endless Tweaker] Initializing!");
        Debug.Log(typeof(EndlessTweakerMain).AssemblyQualifiedName);

        //ulong SpeedDemonID = 3459964374;
        //bool SD = Modloader.LoadedItems.Where(li => li.m_PublishedFileId == SpeedDemonID).Count() > 0;
        //Debug.Log("[Endless Tweaker] Speed Demon Detected: " + SD);
        //testReflect();

        On.RunHandler.StartNewRun += (orig, setConfig, shardID, seed, setRunConfigRuntimeData) =>
        {
            if (!new OptionsCollector().modEnabled) orig(setConfig, shardID, seed, setRunConfigRuntimeData);
            else
            {
                Debug.Log("<<ET>> Captured RunHandler.StartNewRun");
                resetEndlessBossStats();
                orig(setConfig, shardID, seed, setRunConfigRuntimeData);
                if(new OptionsCollector().diffControl)
                {
                    Debug.Log("<<ET>> Diff Start: " + RunHandler.config.startDifficulty);
                    Debug.Log("<<ET>> Diff End: " + RunHandler.config.endDifficulty);
                    Debug.Log("<<ET>> Diff Bump: " + RunHandler.config.keepRunningDifficultyIncreasePerLevel);
                    Debug.Log("<<ET>> Speed Bump: " + RunHandler.config.keepRunningSpeedIncreasePerLevel);
                }
            }
        };

        On.RunHandler.CompleteRun += (orig, win, transitionOutOverride, transitionEffectDelay) =>
        {
            if (!new OptionsCollector().modEnabled || !win) orig(win, transitionOutOverride, transitionEffectDelay);
            else if (RunHandler.config.isEndless)
            {
                Debug.Log("<<ET>> Captured RunHandler.CompleteRun");
                RunHandler.TransitionOnLevelCompleted();
            }
            else
            {
                orig(win, transitionOutOverride, transitionEffectDelay);
            }
        };

        On.RunHandler.TransitionOnLevelCompleted += (orig) =>
        {
            if (!new OptionsCollector().modEnabled) orig();
            else if (RunHandler.config.isEndless)
            {
                Debug.Log("<<ET>> Captured RunHandler.TransitionOnLevelCompleted");
                //Debug.Log("<<ET>> Finished Level: " + RunHandler.RunData.currentLevel);
                //Debug.Log("<<ET>> Biome: " + RunHandler.selectedBiome);
                //Debug.Log("<<ET>> Config: " + RunHandler.configOverride);
                //Debug.Log("<<ET>> NodeType: " + RunHandler.RunData.currentNode.type);

                //RunHandler.RunData.currentLevelID++;
                //RunHandler.RunData.currentLevel++;

                RunHandler.RunData.currentNodeStatus = NGOPlayer.PlayerNodeStatus.PostLevelScreen;

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
                inAward = true;
                OptionsCollector options = new();
                int itemCount = RunHandler.RunData.itemData.Count();
                bool canGetMoreItems = itemCount + 1 < options.maxItems;

                System.Random currentLevelRandomInstance = RunHandler.GetCurrentLevelRandomInstance(itemCount);
                for (int i = 0; i < options.rewardOptions; i++)
                {
                    UnlockScreen.me.AddItem(ItemDatabase.GetRandomItem(localPlayer, currentLevelRandomInstance, GetRandomItemFlags.Major, TagInteraction.None, null, UnlockScreen.me.itemsToAdd));
                }

                UnlockScreen.me.chooseItem = true;
                //UnlockScreen.me.FinishAddingPhase(RunHandler.PlayNextLevel);
                    UnlockScreen.me.FinishAddingPhase(localPlayer, () =>
                    {
                        if (canGetMoreItems && extraReward > 0)
                        {
                            Debug.Log("<<ET>> ExtraReward: " +  extraReward);
                            extraReward--;
                            SceneManager.LoadScene("EndlessAwardScene");
                        }
                        else
                        {
                            //UI_TransitionHandler.instance.Transition(RunNextScene, "Dots", 0.3f, 0.5f);
                            RunNextScene();
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
                inAward = true;
                OptionsCollector options = new();
                int itemCount = RunHandler.RunData.itemData.Count();
                bool canGetMoreItems = itemCount + 1 < options.maxItems;

                System.Random currentLevelRandomInstance = RunHandler.GetCurrentLevelRandomInstance(itemCount);
                for (int i = 0; i < options.rewardOptions; i++)
                {
                    self.AddItem(ItemDatabase.GetRandomItem(localPlayer, currentLevelRandomInstance, GetRandomItemFlags.Major, TagInteraction.None, null, self.itemsToAdd));
                }

                self.chooseItem = true;
                //self.FinishAddingPhase(RunHandler.PlayNextLevel);
                self.FinishAddingPhase(localPlayer, () =>
                {
                    if (canGetMoreItems && extraReward > 0)
                    {
                        Debug.Log("<<ET>> ExtraReward: " + extraReward);
                        extraReward--;
                        SceneManager.LoadScene("EndlessAwardScene");
                    }
                    else
                    {
                        //UI_TransitionHandler.instance.Transition(RunNextScene, "Dots", 0.3f, 0.5f);
                        RunNextScene();
                    }
                });
                self.ActivateItemButtons();
            }
        };

        On.RunHandler.TransitionBackToLevelMap += (orig) =>
        {
            if (!new OptionsCollector().modEnabled) orig();
            else if (RunHandler.config.isEndless)
            {
                Debug.Log("<<ET>> Captured RunHandler.TransitionBackToLevelMap");
                MusicPlayer.Instance.ChangePlaylist(RunHandler.RunData.runConfig.musicPlaylist);
                UI_TransitionHandler.instance.Transition(RunNextScene, "Dots", 0.3f, 0.5f);
                //RunNextScene(true);
            }
            else orig();
        };
    }

    //private static void testReflect()
    //{
    //    var c = Type.GetType("SpeedDemon.ItemReward+RefreshCost, SpeedDemon");
    //    Debug.Log(c);
    //    var m = c.GetMethod("GetRefreshCost");
    //    Debug.Log(m);
    //    var i = Activator.CreateInstance(c);
    //    Debug.Log(i);
    //    Debug.Log(m.Invoke(i, []));
    //}
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

    private static LevelSelectionNode.Data getNextNode(LevelSelectionNode.NodeType type)
    {
        int level = RunHandler.RunData.currentLevel;
        return new LevelSelectionNode.Data(level, type, level + 1, 0);
    }
    private static void GoToNormal()
    {
        Debug.Log("<<ET>> Sending to Normal Fragment");
        LevelSelectionHandler.PlayNode(getNextNode(LevelSelectionNode.NodeType.Default));
        //RunHandler.RunData.currentNode.type = LevelSelectionNode.NodeType.Default;
        ////RunHandler.LoadLevelScene();
        //    LoadLevelSceneReflect();
    }
    private static void GoToChallenge()
    {
        Debug.Log("<<ET>> Sending to Challenge Fragment");
        LevelSelectionHandler.PlayNode(getNextNode(LevelSelectionNode.NodeType.Challenge));
        //RunHandler.RunData.currentNode.type = LevelSelectionNode.NodeType.Challenge;
        ////RunHandler.PlayChallenge();
        //    RunHandler.configOverride = (LevelGenConfig)Resources.Load("Ethereal");
        //    LoadLevelSceneReflect();
    }
    private static void GoToShop()
    {
        Debug.Log("<<ET>> Sending to Shop");
        shopCount++;
        LevelSelectionHandler.PlayNode(getNextNode(LevelSelectionNode.NodeType.Shop));
        //RunHandler.RunData.currentNode.type = LevelSelectionNode.NodeType.Shop;
        ////RunHandler.TransitionToShop();
        //    HasteStats.AddStat(HasteStatType.STAT_SHOPS_VISITED, 1);
        //    SceneManager.LoadScene("ShopScene", LoadSceneMode.Single);
    }
    private static void GoToRest()
    {
        Debug.Log("<<ET>> Sending to Rest");
        restCount++;
        LevelSelectionHandler.PlayNode(getNextNode(LevelSelectionNode.NodeType.RestStop));
        //RunHandler.RunData.currentNode.type = LevelSelectionNode.NodeType.RestStop;
        ////RunHandler.PlayRestScene();
        //    HasteStats.AddStat(HasteStatType.STAT_REST_VISITED, 1);
        //    SceneManager.LoadScene("RestScene_Current", LoadSceneMode.Single);
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

        LevelSelectionHandler.PlayNode(getNextNode(LevelSelectionNode.NodeType.Boss));

        //RunHandler.RunData.currentNode.type = LevelSelectionNode.NodeType.Boss;
        ////RunHandler.TransitionToBoss();
        //    SceneManager.LoadScene(RunHandler.config.bossScene, LoadSceneMode.Single);
    }
    private static void RunNextScene()
    {
        OptionsCollector options = new OptionsCollector();
        bool fromAward = inAward || RunHandler.RunData.currentNode.type == LevelSelectionNode.NodeType.Shop || RunHandler.RunData.currentNode.type == LevelSelectionNode.NodeType.RestStop;
            inAward = false;
        int itemCount = RunHandler.RunData.itemData.Count();
        bool canGetMoreItems = itemCount < options.maxItems;

        bool bossEligible = RunHandler.RunData.currentLevel >= options.bossMinFloors;
            if(!bossEligible && !options.bossInterval) options.bossNum = 0;

        // Determine if Giving Award, and how many
        bool giveReward = options.itemsEnabled; // Master Toggle for run-based rewards
            if (RunHandler.RunData.currentLevel == 1) giveReward = giveReward && options.immediateItem; // If just finished first stage, give item if ImmediateItem setting
            else giveReward = giveReward && (RunHandler.RunData.currentLevel % options.itemFrequency == 0); // Otherwise check if frequency
        
        if (!fromAward && canGetMoreItems && RunHandler.RunData.currentNode.type == LevelSelectionNode.NodeType.Challenge && options.challengeReward)
        {
            if (!giveReward) giveReward = true; // If just finished Challenge frag and ChallengeReward, override true
            else extraReward++; // If already giving reward, signal to give extra reward
            Debug.Log("<<ET>> Challenge Reward Flag");
        } 
        if (!fromAward && canGetMoreItems && RunHandler.RunData.currentNode.type == LevelSelectionNode.NodeType.Boss && options.bossReward)
        {
            if (!giveReward) giveReward = true; // If just finished Boss frag and BossReward, override true
            else extraReward++; // If already giving reward, signal to give extra reward
            Debug.Log("<<ET>> Boss Reward Flag");
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

        bool toAward = false;
        // Determine if Giving Award
        if (canGetMoreItems && giveReward && !fromAward)
        {
            Debug.Log("<<ET>> Sending to EndlessAward");
            toAward = true;
            SceneManager.LoadScene("EndlessAwardScene");
        }
        else if (options.bossInterval && bossEligible && (RunHandler.RunData.currentLevel + 1) % options.bossNum == 0)
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
            wa.Run(RunHandler.GetCurrentLevelRandomInstance(shopCount + restCount));
        }

        if (RunHandler.InRun && !fromAward) 
        {
            if(!toAward) RunHandler.RunData.currentNodeStatus = NGOPlayer.PlayerNodeStatus.PostLevelScreenComplete;

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
        weights.ForEach(w => total += w);

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
            } else Debug.Log("<<ET>> Passing " + funcs[i].Method.Name);
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