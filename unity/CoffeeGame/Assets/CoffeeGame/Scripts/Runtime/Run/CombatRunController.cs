using System;
using System.Collections;
using System.Collections.Generic;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using UnityEngine;

namespace CoffeeGame.Run
{
    public enum CombatRunMode
    {
        InputModeSelection,
        Ready,
        Playing,
        RivalEncounter,
        Paused,
        InputSettings,
        InputRebinding,
        GameOver
    }

    [DisallowMultipleComponent]
    public sealed class CombatRunController : MonoBehaviour
    {
        private CombatTuning tuning;
        private GameInputReader input;
        private AudioDirector audioDirector;
        private Health playerHealth;
        private PlayerResources playerResources;
        private PlayerMotor3D playerMotor;
        private PlayerCombatController playerCombat;
        private Func<string, CombatEnemy> spawnEnemy;
        private Action resetEnemySequence;
        private CombatEnemy currentEnemy;
        private readonly List<CombatEnemy> activeEnemies = new List<CombatEnemy>();
        private bool rivalEncounterPending;
        private string runId;
        private int spawnSequence;
        private CombatRunMode modeBeforeRebind;
        private string eventBeforeRebind;
        private CombatRunMode modeBeforeInputSettings;
        private string eventBeforeInputSettings;
        private CombatRunMode modeBeforeInputModeSelection;
        private string eventBeforeInputModeSelection;
        private InputMode inputModeBeforeSelection;
        private int inputModeSelectedFrame = -1;

        public event Action StateChanged;

        public CombatRunMode Mode { get; private set; } = CombatRunMode.Ready;
        public PlayerProgression Progression { get; private set; } = new PlayerProgression();
        public int Kills { get; private set; }
        public int DefaultRivalEncounterIntervalKills => tuning != null ? tuning.RivalEncounterIntervalKills : 5;
        public int RivalEncounterIntervalKills => RivalEncounterSettings.Get(DefaultRivalEncounterIntervalKills);

        public int AdjustRivalEncounterIntervalKills(int delta)
        {
            int next = delta == 0
                ? DefaultRivalEncounterIntervalKills
                : RivalEncounterIntervalKills + delta;
            int saved = RivalEncounterSettings.Set(next);
            LastEvent = $"ライバル出現まで {saved} 体に設定";
            StateChanged?.Invoke();
            return saved;
        }
        public string CurrentRivalId { get; private set; } = RivalCharacterIds.WeaknessChallenger;
        private string lastSeenRivalId;
        private readonly DeterministicRivalSelector rivalSelector =
            new DeterministicRivalSelector(new UnityBoundedIntegerSource());
        public string LastEvent { get; private set; } = "A / Enter / Startで開始";
        public PartyRuntime Party { get; private set; }
        public Health PlayerHealth => Party != null && Party.Active != null ? Party.Active.Health : playerHealth;
        public PlayerResources PlayerResources => Party != null && Party.Active != null ? Party.Active.Resources : playerResources;
        public PlayerCombatController PlayerCombat => Party != null && Party.Active != null ? Party.Active.Combat : playerCombat;
        public void AttachParty(PartyRuntime party) => Party = party;
        public CombatEnemy CurrentEnemy => currentEnemy;
        public IReadOnlyList<CombatEnemy> ActiveEnemies => activeEnemies;
        public int ActiveEnemyCount => activeEnemies.Count;
        public int AliveEnemyCount
        {
            get
            {
                int count = 0;
                foreach (CombatEnemy enemy in activeEnemies)
                {
                    if (enemy != null && enemy.Health != null && enemy.Health.IsAlive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public const int EnemiesPerEncounter = 2;

        public void Initialize(
            CombatTuning combatTuning,
            PlayerProgression playerProgression,
            GameInputReader inputReader,
            AudioDirector audio,
            Health health,
            PlayerResources resources,
            PlayerMotor3D motor,
            PlayerCombatController combat,
            Func<string, CombatEnemy> enemyFactory,
            Action resetEnemySpawnSequence)
        {
            tuning = combatTuning;
            Progression = playerProgression ?? throw new ArgumentNullException(nameof(playerProgression));
            input = inputReader;
            audioDirector = audio;
            playerHealth = health;
            playerResources = resources;
            playerMotor = motor;
            playerCombat = combat;
            spawnEnemy = enemyFactory;
            resetEnemySequence = resetEnemySpawnSequence;
            playerHealth.Died += HandlePlayerDied;
            input.RebindFinished += HandleRebindFinished;
            EnterReadyMode();
            BeginInputModeSelection();
            if (HasCommandLineArgument("-coffee-autostart"))
            {
                InputMode automationMode = input.HasConnectedGamepad
                    ? InputMode.ControllerGamepad
                    : InputMode.KeyboardMouse;
                if (TrySelectInputMode(automationMode, out _))
                {
                    StartNewRun();
                }
            }
        }

        private sealed class UnityBoundedIntegerSource : IBoundedIntegerSource
        {
            public int Next(int exclusiveUpperBound)
            {
                return UnityEngine.Random.Range(0, exclusiveUpperBound);
            }
        }

        private static bool HasCommandLineArgument(string expected)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public void StartNewRun()
        {
            if (input == null || input.SelectedInputMode == InputMode.Unselected)
            {
                BeginInputModeSelection();
                return;
            }

            Time.timeScale = 1f;
            if (Party != null && !Party.TryStartRun())
            {
                LastEvent = Party.Notice;
                StateChanged?.Invoke();
                return;
            }
            StopLifecycleCoroutines();
            ClearAllEnemies();

            runId = Guid.NewGuid().ToString("N");
            spawnSequence = 0;
            resetEnemySequence?.Invoke();
            Kills = 0;
            if (Party == null)
            {
                playerMotor.ResetMotor(new Vector3(-1.6f, 0.05f, 0f));
                playerMotor.CanMove = true;
                ApplyProgressionTuning();
                playerCombat.ResetCombat();
            }

            Mode = CombatRunMode.Playing;
            input.EnableBattle();
            LastEvent = "森の魔物を倒そう";
            audioDirector.StartMusic();
            SpawnNextEnemy();
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }

            if (Mode == CombatRunMode.InputModeSelection || Time.frameCount <= inputModeSelectedFrame)
            {
                return;
            }

            if (Mode == CombatRunMode.Ready || Mode == CombatRunMode.GameOver)
            {
                if (input.ConfirmPressed)
                {
                    StartNewRun();
                }
                return;
            }

            if (Mode == CombatRunMode.RivalEncounter)
            {
                // The rival question owns explicit edit/confirm/submit input. A held confirm
                // from combat must never skip the encounter or submit an answer implicitly.
                return;
            }

            if (Mode == CombatRunMode.Playing && input.PausePressed)
            {
                Pause();
            }
        }

        public void Pause()
        {
            if (Mode != CombatRunMode.Playing)
            {
                return;
            }

            Mode = CombatRunMode.Paused;
            playerMotor.CanMove = false;
            input.EnableUI();
            Time.timeScale = 0f;
            LastEvent = "一時停止中 — Start / Esc / 取消で再開";
            StateChanged?.Invoke();
        }

        public void Resume()
        {
            if (Mode != CombatRunMode.Paused)
            {
                return;
            }

            Time.timeScale = 1f;
            Mode = CombatRunMode.Playing;
            playerMotor.CanMove = true;
            input.EnableBattle();
            LastEvent = "戦闘再開";
            StateChanged?.Invoke();
        }

        public bool BeginInputRebind()
        {
            if (Mode == CombatRunMode.InputRebinding || input.IsRebinding)
            {
                return false;
            }

            modeBeforeRebind = Mode;
            eventBeforeRebind = LastEvent;
            Mode = CombatRunMode.InputRebinding;
            playerMotor.CanMove = false;
            Time.timeScale = 0f;
            LastEvent = "入力待機中 — 新しいボタンを押してください";
            StateChanged?.Invoke();
            return true;
        }

        public bool BeginInputSettings()
        {
            if (Mode == CombatRunMode.InputSettings ||
                Mode == CombatRunMode.InputModeSelection ||
                Mode == CombatRunMode.InputRebinding ||
                input == null ||
                input.IsRebinding)
            {
                return false;
            }

            modeBeforeInputSettings = Mode;
            eventBeforeInputSettings = LastEvent;
            Mode = CombatRunMode.InputSettings;
            playerMotor.CanMove = false;
            Time.timeScale = 0f;
            input.EnableUI();
            LastEvent = "コントローラー割当を設定中";
            StateChanged?.Invoke();
            return true;
        }

        public bool BeginInputModeSelection()
        {
            if (input == null ||
                Mode == CombatRunMode.InputModeSelection ||
                Mode == CombatRunMode.InputRebinding ||
                input.IsRebinding)
            {
                return false;
            }

            modeBeforeInputModeSelection = Mode;
            eventBeforeInputModeSelection = LastEvent;
            inputModeBeforeSelection = input.SelectedInputMode;
            Mode = CombatRunMode.InputModeSelection;
            playerMotor.CanMove = false;
            Time.timeScale = 0f;
            input.BeginInputModeSelection();
            LastEvent = "使用する入力方式を選んでください";
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySelectInputMode(InputMode mode, out string message)
        {
            if (Mode != CombatRunMode.InputModeSelection)
            {
                message = "現在は入力方式を選択できません。";
                return false;
            }

            if (!input.TrySelectInputMode(mode, out message))
            {
                return false;
            }

            inputModeSelectedFrame = Time.frameCount;
            RestoreModeAfterInputModeSelection();
            return true;
        }

        public bool CancelInputModeSelection(out string message)
        {
            if (Mode != CombatRunMode.InputModeSelection || inputModeBeforeSelection == InputMode.Unselected)
            {
                message = "起動時は入力方式を1つ選んでください。";
                return false;
            }

            return TrySelectInputMode(inputModeBeforeSelection, out message);
        }

        public bool EndInputSettings()
        {
            if (Mode != CombatRunMode.InputSettings || input.IsRebinding)
            {
                return false;
            }

            Mode = modeBeforeInputSettings;
            LastEvent = eventBeforeInputSettings;
            bool resumeBattle = Mode == CombatRunMode.Playing;
            playerMotor.CanMove = resumeBattle;
            Time.timeScale = Mode == CombatRunMode.Paused ? 0f : 1f;
            if (resumeBattle)
            {
                input.EnableBattle();
            }
            else
            {
                input.EnableUI();
            }
            StateChanged?.Invoke();
            return true;
        }

        public void CancelInputRebindMode()
        {
            if (Mode == CombatRunMode.InputRebinding)
            {
                RestoreModeAfterRebind();
            }
        }

        private void EnterReadyMode()
        {
            Time.timeScale = 1f;
            Mode = CombatRunMode.Ready;
            playerMotor.CanMove = false;
            input.EnableUI();
            LastEvent = GetReadyPrompt();
            StateChanged?.Invoke();
        }

        private string GetReadyPrompt()
        {
            return input != null
                ? input.SelectedInputMode switch
                {
                    InputMode.ControllerGamepad => "South / Startで開始",
                    InputMode.SteamDesktopCompatibility => "A（Enter）で開始",
                    InputMode.KeyboardMouse => "Enterまたは画面の開始ボタンで開始",
                    _ => "入力方式を選んでください"
                }
                : "入力方式を選んでください";
        }

        private void RestoreModeAfterInputModeSelection()
        {
            Mode = modeBeforeInputModeSelection;
            LastEvent = Mode == CombatRunMode.Ready ? GetReadyPrompt() : eventBeforeInputModeSelection;
            bool resumeBattle = Mode == CombatRunMode.Playing;
            playerMotor.CanMove = resumeBattle;
            Time.timeScale = Mode == CombatRunMode.Paused || Mode == CombatRunMode.InputSettings ? 0f : 1f;
            if (resumeBattle)
            {
                input.EnableBattle();
            }
            else
            {
                input.EnableUI();
            }
            StateChanged?.Invoke();
        }

        private void SpawnNextEnemy()
        {
            if (Mode != CombatRunMode.Playing)
            {
                return;
            }

            while (activeEnemies.Count < EnemiesPerEncounter)
            {
                int before = activeEnemies.Count;
                SpawnEnemySlot();
                if (activeEnemies.Count == before)
                {
                    break;
                }
            }

            currentEnemy = GetPrimaryEnemy();
            LastEvent = currentEnemy != null
                ? $"{currentEnemy.DisplayName} {Kills + 1}"
                : "森の魔物を倒そう";
            StateChanged?.Invoke();
        }

        private void SpawnEnemySlot()
        {
            spawnSequence++;
            string claimId = $"enemy:{runId}:encounter:{spawnSequence}";
            CombatEnemy enemy = spawnEnemy(claimId);
            if (enemy == null)
            {
                return;
            }

            enemy.Defeated += HandleEnemyDefeated;
            activeEnemies.Add(enemy);
            if (currentEnemy == null)
            {
                currentEnemy = enemy;
            }
        }

        private void HandleEnemyDefeated(CombatEnemy enemy)
        {
            if (enemy == null || !activeEnemies.Contains(enemy) || Mode != CombatRunMode.Playing)
            {
                return;
            }

            int previousLevel = Progression.Level;
            RewardBundle reward = enemy.Reward;
            bool applied = Progression.TryApplyReward(enemy.ClaimId, reward);
            if (!applied)
            {
                return;
            }

            Kills++;
            audioDirector.Play(CombatSound.Reward, 0.62f);
            LastEvent = $"+EXP {reward.Experience}  +Gold {reward.Gold}" +
                (reward.SlimeJelly > 0 ? $"  +スライムゼリー {reward.SlimeJelly}" : string.Empty);

            int gainedLevels = Progression.Level - previousLevel;
            if (gainedLevels > 0)
            {
                ApplyProgressionTuning();
                audioDirector.Play(CombatSound.LevelUp, 0.9f);
                LastEvent = $"LEVEL UP!  Lv.{Progression.Level}";
            }

            enemy.Defeated -= HandleEnemyDefeated;
            if (IsRivalEncounterMilestone(Kills, RivalEncounterIntervalKills) && !rivalEncounterPending)
            {
                rivalEncounterPending = true;
                StartCoroutine(EnterRivalEncounterAfterDelay(enemy));
            }
            else if (!rivalEncounterPending)
            {
                StartCoroutine(RespawnAfterDelay(enemy));
            }
            StateChanged?.Invoke();
        }

        public static bool IsRivalEncounterMilestone(int kills, int intervalKills)
        {
            return kills > 0 && intervalKills > 0 && kills % intervalKills == 0;
        }

        public void ContinueAfterRivalEncounter()
        {
            if (Mode != CombatRunMode.RivalEncounter)
            {
                return;
            }

            lastSeenRivalId = CurrentRivalId;
            Time.timeScale = 1f;
            Mode = CombatRunMode.Playing;
            playerMotor.CanMove = true;
            input.RestoreBattleAfterTextEntry();
            LastEvent = "ライバルは次の勝負を予告して去っていった";
            rivalEncounterPending = false;
            SpawnNextEnemy();
            StateChanged?.Invoke();
        }

        public void ApplyLoadedProgression()
        {
            if (tuning == null || playerHealth == null || playerResources == null || playerCombat == null)
            {
                return;
            }

            ApplyProgressionTuning();
            LastEvent = $"セーブを反映しました  Lv.{Progression.Level}";
            StateChanged?.Invoke();
        }

        private void ApplyProgressionTuning()
        {
            if (Party != null) { Party.RefreshMembers(); return; }
            int levelBonus = Progression.Level - 1;
            PlayerDerivedStats derived = PlayerDerivedStatCalculator.Calculate(Progression.Status);
            playerHealth.Initialize(tuning.PlayerMaxHealth + 4 * levelBonus, 0.68f);
            playerHealth.IncomingDamageMultiplier = derived.IncomingDamageMultiplier;
            playerHealth.EvasionChance = derived.EvasionChance;
            playerResources.Initialize(
                tuning.MaxStamina * derived.MaxStaminaMultiplier,
                tuning.PlayerMaxMp + 2 * levelBonus,
                tuning.MagicMpRegenPerSecond);
            playerMotor.SpeedMultiplier = derived.MovementSpeedMultiplier;
            playerCombat.AttackBonus = levelBonus;
            playerCombat.AttackMultiplier = derived.AttackMultiplier;
            playerCombat.CriticalChance = derived.CriticalChance;
            playerCombat.SpecialChargeSpeedMultiplier = derived.SpecialChargeSpeedMultiplier;
        }

        private IEnumerator RespawnAfterDelay(CombatEnemy defeated)
        {
            yield return WaitForWorld(defeated != null ? defeated.DefeatDisplaySeconds : 0.58f);
            RemoveEnemy(defeated);
            if (Mode == CombatRunMode.Playing && !rivalEncounterPending)
            {
                SpawnNextEnemy();
            }
        }

        private IEnumerator EnterRivalEncounterAfterDelay(CombatEnemy defeated)
        {
            yield return WaitForWorld(defeated != null ? defeated.DefeatDisplaySeconds : 0.58f);
            if (Mode != CombatRunMode.Playing)
            {
                yield break;
            }

            ClearAllEnemies();

            CurrentRivalId = rivalSelector.Select(
                RivalCharacterIds.EncounterCandidates(Progression.IsRivalRecruited),
                lastSeenRivalId);
            Mode = CombatRunMode.RivalEncounter;
            playerMotor.CanMove = false;
            playerCombat.CancelPendingActions();
            input.EnableUI();
            Time.timeScale = 0f;
            LastEvent = "ライバルが現れた — 苦手問題に挑戦";
            StateChanged?.Invoke();
        }

        private void HandlePlayerDied(Health _, DamageInfo damage)
        {
            if (Party != null) return;
            if (Mode != CombatRunMode.Playing)
            {
                return;
            }

            Mode = CombatRunMode.GameOver;
            playerMotor.CanMove = false;
            playerCombat.CancelPendingActions();
            input.EnableUI();
            StopLifecycleCoroutines();
            ClearAllEnemies();
            LastEvent = "GAME OVER — A / Enter / Startで再挑戦";
            StateChanged?.Invoke();
        }

        private void HandleRebindFinished(bool accepted)
        {
            if (Mode == CombatRunMode.InputRebinding)
            {
                RestoreModeAfterRebind();
            }
        }

        private void RestoreModeAfterRebind()
        {
            Mode = modeBeforeRebind;
            LastEvent = eventBeforeRebind;
            bool resumeBattle = Mode == CombatRunMode.Playing;
            playerMotor.CanMove = resumeBattle;
            Time.timeScale = Mode == CombatRunMode.Paused || Mode == CombatRunMode.InputSettings ? 0f : 1f;
            StateChanged?.Invoke();
        }

        private CombatEnemy GetPrimaryEnemy()
        {
            foreach (CombatEnemy enemy in activeEnemies)
            {
                if (enemy != null && enemy.Health != null && enemy.Health.IsAlive)
                {
                    return enemy;
                }
            }

            foreach (CombatEnemy enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    return enemy;
                }
            }

            return null;
        }

        private void RemoveEnemy(CombatEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.Defeated -= HandleEnemyDefeated;
            activeEnemies.Remove(enemy);
            if (Application.isPlaying) Destroy(enemy.gameObject);
            else DestroyImmediate(enemy.gameObject);
            if (currentEnemy == enemy)
            {
                currentEnemy = GetPrimaryEnemy();
            }
        }

        private void ClearAllEnemies()
        {
            CombatEnemy[] enemies = activeEnemies.ToArray();
            activeEnemies.Clear();
            currentEnemy = null;
            foreach (CombatEnemy enemy in enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                enemy.Defeated -= HandleEnemyDefeated;
                if (Application.isPlaying) Destroy(enemy.gameObject);
                else DestroyImmediate(enemy.gameObject);
            }
        }

        private void StopLifecycleCoroutines()
        {
            StopAllCoroutines();
            rivalEncounterPending = false;
        }

        private void OnDestroy()
        {
            TimeStopController.Instance?.Cancel();
            Time.timeScale = 1f;
            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
            }
            if (input != null)
            {
                input.RebindFinished -= HandleRebindFinished;
            }
            StopLifecycleCoroutines();
            ClearAllEnemies();
        }

        public void EndRunForRest()
        {
            StopRun();
            EnterReadyMode();
            LastEvent = "全員休息中 — 出撃する仲間を選んで再開できます";
            StateChanged?.Invoke();
        }

        public void EndRunForDefeat()
        {
            StopRun();
            Mode = CombatRunMode.GameOver;
            input.EnableUI();
            LastEvent = "全員が戦闘不能 — 10分でHP1%に回復し、休息へ移ります";
            StateChanged?.Invoke();
        }

        private void StopRun()
        {
            TimeStopController.Instance?.Cancel();
            StopLifecycleCoroutines();
            ClearAllEnemies();
        }

        private IEnumerator WaitForWorld(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += CombatClock.WorldDeltaTime) yield return null;
        }
    }
}
