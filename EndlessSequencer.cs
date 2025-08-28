using System.Reflection;
using Landfall.Haste;
using Landfall.Haste.Music;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndlessTweaker
{
    internal class EndlessSequencer
    {
        public static bool inAward = false;
        public static int rewardCount = 0;
        public static LevelSelectionNode.NodeType currentType = LevelSelectionNode.NodeType.Default;

        private static int incrementor = 0;
        public static int counter() { return incrementor++; }
        private static void Log(string str) { Debug.Log("<<ET>> " +  str); }
        public static System.Random getRandom() { return RunHandler.GetCurrentLevelRandomInstance(counter()); }

        /**
         * After completing a Run or Challenge, HasteTransitioner.GotoTargetSceneState uses RunHandler.TransitionOnLevelCompleted()
         * After exiting a Shop or Rest, HasteTransitioner.GoToTargetSceneState uses RunHandler.TransitionBackToLevelMap();
         * 
         * Both methods should be hooked and rerouted here instead, if the mod is enabled and we're in endless mode
         * 
         * Or:
         * Hook HasteTransitioner.GoToTargetSceneState
         * Check if: UI_TransitionHandler.IsTransitioning && ((!GM_Run.instance && !Challenge.instance) || !currentState.currentNodeStatus.IsLoadingLike())
         * Else if: NGOPlayer.PlayerNodeStatus.EnteredPortalAndWaiting -> Hook into this
         * Else: orig?
         **/
        public static void HandleEndOfLevel()
        {
            RunHandler.RunData.currentNodeStatus = NGOPlayer.PlayerNodeStatus.PostLevelScreen;

            // Heal Player
            OptionsCollector options = new();
            int stageHealAmount = options.stageHeal;
            int stageLifeFrequency = options.stageLife;

            if (stageHealAmount > 0)
            {
                AddHealthReflect(Player.localPlayer, stageHealAmount);
            }
            if (stageLifeFrequency > 0 && RunHandler.RunData.currentLevel % stageLifeFrequency == 0)
            {
                Player.localPlayer.EditLives(1);
            }

            // Save Stats
            HasteStats.SetStat(HasteStatType.STAT_ENDLESS_HIGHSCORE, RunHandler.RunData.currentLevel, onlyInc: true);
            HasteStats.OnLevelComplete();
            
            // Ensure PostLevelScene doesn't hijack award
            if (currentType == LevelSelectionNode.NodeType.Challenge || currentType == LevelSelectionNode.NodeType.Boss) 
            {
                LevelSelectionNode.Data data = RunHandler.RunData.currentNode;
                RunHandler.RunData.currentNode = new LevelSelectionNode.Data(data.id, LevelSelectionNode.NodeType.Default, data.currentLevel, data.restartCounter);
            }

            // To PostLevelScene
            Log("Transitioning to Post-Level Scene");
            TransitionToScene("PostLevelScene");
        }
        private static void AddHealthReflect(Player localPlayer, float amount)
        {
            MethodInfo getAddHealthMethod = typeof(Player).GetMethod("AddHealth", BindingFlags.Instance | BindingFlags.NonPublic);
            getAddHealthMethod.Invoke(localPlayer, [amount]);
        }

        public static void TransitionToScene(String scene)
        {
            UI_TransitionHandler.instance.Transition(delegate
            {
                SceneManager.LoadScene(scene);
            }, "Dots", 0.3f, 0.5f);
        }

        public static void HandleBackToLevelMap()
        {
            MusicPlayer.Instance.ChangePlaylist(RunHandler.RunData.runConfig.musicPlaylist);

            //UI_TransitionHandler.instance.Transition(RunNextScene, "Dots", 0.3f, 0.5f);

            OptionsCollector options = new OptionsCollector();
            LevelSelectionNode.Data currentNode = RunHandler.RunData.currentNode;
            if (!inAward)
            { // Check how many Awards eligible for, if >0 transition to Award
                /*
                 * Awards:
                 * CurrentLevel % AwardFrequency == 0
                 * ChallengeFloor IFF ChallengeAward
                 * BossFloor IFF BossAward
                 * 
                 * Limits:
                 * CurrentItems >= MaxItems
                 * ShopFloor
                 * RestFloor
                 */
                rewardCount = 0;
                if (currentNode.currentLevel % options.itemFrequency == 0) { 
                    rewardCount++;
                    Log("Item Frequency Reward Flag");
                }
                if (currentType == LevelSelectionNode.NodeType.Challenge && options.challengeReward)
                {
                    rewardCount++;
                    Log("Challenge Floor Reward Flag");
                }
                if (currentType == LevelSelectionNode.NodeType.Boss && options.bossReward)
                {
                    rewardCount++;
                    Log("Boss Floor Reward Flag");
                }

                if (RunHandler.RunData.itemData.Count() >= options.maxItems)
                {
                    rewardCount = 0;
                    Log("Max Items Reward Inhibitor");
                }
                if (currentType == LevelSelectionNode.NodeType.Shop)
                {
                    rewardCount = 0;
                    Log("Shop Floor Reward Inhibitor");
                }
                if (currentType == LevelSelectionNode.NodeType.RestStop)
                {
                    rewardCount = 0;
                    Log("Rest Floor Reward Inhibitor");
                }

                if (rewardCount > 0)
                {
                    Log("Reward Counter Positive: " + rewardCount + ". Transitioning to EndlessAward");
                    TransitionToScene("EndlessAwardScene");
                    return;
                }
            }
            else inAward = false; // Came from Award, clear flag

            WeightedAction wa = new();
            wa.Add(options.normalWeight, () =>
            {
                Log("Sending to Normal Node");
                currentType = LevelSelectionNode.NodeType.Default;
                LevelSelectionHandler.PlayNode(getNextNode(currentType));
            });
            wa.Add(options.challengeWeight, () =>
            {
                Log("Sending to Challenge Node");
                currentType = LevelSelectionNode.NodeType.Challenge;
                LevelSelectionHandler.PlayNode(getNextNode(currentType));
            });
            if(currentType != LevelSelectionNode.NodeType.Shop) wa.Add(options.shopWeight, () =>
            {
                Log("Sending to Shop Node");
                currentType = LevelSelectionNode.NodeType.Shop;
                LevelSelectionHandler.PlayNode(getNextNode(currentType));
            });
            if(currentType!= LevelSelectionNode.NodeType.RestStop) wa.Add(options.restWeight, () =>
            {
                Log("Sending to Rest Node");
                currentType = LevelSelectionNode.NodeType.RestStop;
                LevelSelectionHandler.PlayNode(getNextNode(currentType));
            });

            if(currentNode.currentLevel >= options.bossMinFloors) // Only allow Boss floor if at least enough floors have been completed
                if(options.bossInterval)
                {
                    if( (currentNode.currentLevel + 1) % options.bossNum == 0) // If Interval Boss Level, go immediately, do not spin the wheel, do not collect 200 sparks
                    {
                        Log("Sending to Boss Floor (Interval)");
                        GoToBoss();
                        return;
                    }
                } else
                {
                    wa.Add(options.bossNum, () =>
                    {
                        Log("Sending to Boss Floor (Weighted)");
                        GoToBoss();
                    });
                }


            // Determine Difficulty Modification
            if (options.diffControl)
            {
                int diff = options.initialDiff;
                if (options.diffScaleFreq > 0) switch (options.diffScale)
                    {
                        case DiffScaleEnum.Stage:
                            diff += options.diffScaleRate * (RunHandler.RunData.currentLevel / options.diffScaleFreq);
                            break;
                        case DiffScaleEnum.Item:
                            diff += options.diffScaleRate * (RunHandler.RunData.itemData.Count() / options.diffScaleFreq);
                            break;
                        case DiffScaleEnum.Boss:
                            diff += options.diffScaleRate * (bossCount / options.diffScaleFreq);
                            break;
                    }
                RunHandler.config.startDifficulty = diff;
                RunHandler.config.endDifficulty = diff;
            }

            wa.Run(getRandom());
        }
        private static LevelSelectionNode.Data getNextNode(LevelSelectionNode.NodeType type)
        {
            int id = RunHandler.RunData.currentLevel;
            int level = id;
            switch(type)
            { // Only increment CurrentLevel on actual Run levels, not passive levels.
                case LevelSelectionNode.NodeType.Default:
                case LevelSelectionNode.NodeType.Challenge:
                case LevelSelectionNode.NodeType.Boss:
                    level++;
                    break;
                case LevelSelectionNode.NodeType.Shop:
                case LevelSelectionNode.NodeType.RestStop:
                case LevelSelectionNode.NodeType.Encounter: // If I ever implement Encounters, this will probably be moved to Increment
                    break;
            }
            return new LevelSelectionNode.Data(id, type, level, 0);
        }
        public static bool[] jumper = [false, false];
        public static bool[] convoy = [false, false];
        public static bool[] snake = [false, false];
        public static int bossCount = 0;
        public static void ResetEndlessBossStats()
        {
            jumper = [false, false];
            convoy = [false, false];
            snake = [false, false];
            bossCount = 0;
        }
        public static void GoToBoss()
        {
            bossCount++;
            OptionsCollector options = new();

            string[] bossScenes = ["Challenge_ForestBoss", "Challenge_DesertBoss", "Challenge_SnakeBoss"];

            WeightedFunc<string> wf = new();
            if (options.jumperWeight > 0) wf.Add(options.jumperWeight, () => bossScenes[0]);
            if (options.convoyWeight > 0) wf.Add(options.convoyWeight, () => bossScenes[1]);
            if (options.snakeWeight > 0) wf.Add(options.snakeWeight, () => bossScenes[2]);
            String? boss = wf.Run(getRandom());
            Log("Selected Boss: " + boss);
            
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
            level = (int)Math.Floor(getRandom().NextFloat() * level);
            Log("Selected Boss Tier: " +  level);

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

            Log("Sending to Selected Boss (" + boss + " " + level + ")");
            currentType = LevelSelectionNode.NodeType.Boss;
            LevelSelectionHandler.PlayNode(getNextNode(currentType));
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
                Log("WeightedAction Total: " + total);
                Log("WeightedAction roll: " + roll);
                for (int i = 0; i < weights.Count; ++i)
                {
                    roll -= weights[i];
                    if (roll < 1)
                    {
                        funcs[i]();
                        return;
                    }
                    else Log("Skipping " + funcs[i].Method.Name);
                }
                Log("Reached end of WeightedAction without running anything.");
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
                Log("WeightedFunc Total: " + total);
                Log("WeightedFunc roll: " + roll);
                for (int i = 0; i < weights.Count; ++i)
                {
                    roll -= weights[i];
                    if (roll < 1)
                    {
                        return funcs[i]();
                    }
                    else Log("Skipping " + funcs[i]());
                }
                Log("Reached end of WeightedFunc without running anything.");
                return default;
            }
        }
    }
}
