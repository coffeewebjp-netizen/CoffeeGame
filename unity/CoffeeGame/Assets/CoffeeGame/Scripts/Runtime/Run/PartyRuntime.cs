using System;
using System.Collections.Generic;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Run
{
    [DefaultExecutionOrder(-200)]
    public sealed class PartyRuntime : MonoBehaviour
    {
        private readonly Dictionary<string, PartyActor> actors = new Dictionary<string, PartyActor>();
        private CombatRunController run;
        private CombatTuning tuning;
        private GameInputReader input;
        private Func<PartyActor> catFactory;
        private Action checkpoint;
        private Action<Transform> follow;
        private DateTime clockAnchor;
        private double realAnchor;
        private float checkpointRemaining;
        private string queuedSwitch;
        private PartyActor retreating;
        private float retreatRemaining;
        private bool synchronizing;
        private bool closing;
        public PartyActor Active { get; private set; }
        public PlayerParty State => run.Progression.Party;
        public IReadOnlyDictionary<string, PartyActor> Actors => actors;
        public string Notice { get; private set; } = string.Empty;
        public float RetreatRemaining => retreatRemaining;
        public bool TimeStopped => TimeStopController.Instance != null && TimeStopController.Instance.IsActive;
        public event Action Changed;
        public DateTime UtcNow
        {
            get
            {
                var monotonic = clockAnchor.AddSeconds(Math.Max(0, Time.realtimeSinceStartupAsDouble - realAnchor));
                if (DateTime.UtcNow > monotonic)
                {
                    clockAnchor = DateTime.UtcNow;
                    realAnchor = Time.realtimeSinceStartupAsDouble;
                    return clockAnchor;
                }
                return monotonic;
            }
        }

        public void Initialize(CombatRunController controller, CombatTuning combatTuning, GameInputReader reader,
            PartyActor hero, Func<PartyActor> createCat, Action save, Action<Transform> followActor)
        {
            run = controller; tuning = combatTuning; input = reader; catFactory = createCat;
            checkpoint = save; follow = followActor;
            clockAnchor = DateTime.UtcNow; realAnchor = Time.realtimeSinceStartupAsDouble;
            AddActor(hero);
            Active = hero;
            run.AttachParty(this);
            State.Settle(UtcNow);
            State.RestAllLiving(UtcNow);
            RefreshMembers();
            run.Progression.Changed += OnProgressionChanged;
            run.StateChanged += OnRunStateChanged;
            checkpointRemaining = 5f;
        }

        private void AddActor(PartyActor actor)
        {
            actors.Add(actor.MemberId, actor);
            actor.Health.Died += OnDied;
            actor.Health.Damaged += OnDamaged;
        }

        private void OnProgressionChanged() { Snapshot(); RefreshMembers(); }

        public void RefreshMembers()
        {
            if (synchronizing) return;
            synchronizing = true;
            try
            {
                if (State.Find(PartyMemberIds.CatMage) != null && !actors.ContainsKey(PartyMemberIds.CatMage)) AddActor(catFactory());
                foreach (var member in State.Members)
                {
                    if (!actors.TryGetValue(member.Id, out var actor)) continue;
                    ApplyTuning(member, actor);
                    Hydrate(member, actor);
                    actor.gameObject.SetActive(member.RecoveryState == PartyRecoveryState.Deployed);
                }
            }
            finally { synchronizing = false; }
            OnRunStateChanged();
            Changed?.Invoke();
        }

        private void ApplyTuning(PartyMember member, PartyActor actor)
        {
            int bonus = member.Level - 1;
            var derived = PlayerDerivedStatCalculator.Calculate(member.Status);
            int maximumHp = Mathf.Max(1, Mathf.RoundToInt((tuning.PlayerMaxHealth + 4 * bonus) * (actor.IsCat ? 0.8f : 1f)));
            float maximumMp = (tuning.PlayerMaxMp + 2 * bonus) * (actor.IsCat ? 1.5f : 1f);
            member.SetMaximumResources(maximumHp, maximumMp, tuning.MaxStamina * derived.MaxStaminaMultiplier, UtcNow);
            actor.Health.IncomingDamageMultiplier = derived.IncomingDamageMultiplier;
            actor.Health.EvasionChance = derived.EvasionChance;
            actor.Motor.SpeedMultiplier = derived.MovementSpeedMultiplier;
            actor.Combat.AttackBonus = bonus;
            actor.Combat.AttackMultiplier = actor.IsCat ? 1f + (member.Status.Technique - 10) * 0.025f : derived.AttackMultiplier;
            actor.Combat.CriticalChance = derived.CriticalChance;
            actor.Combat.SpecialChargeSpeedMultiplier = derived.SpecialChargeSpeedMultiplier;
        }

        private void Hydrate(PartyMember member, PartyActor actor)
        {
            var r = member.Resources;
            actor.HitPointFraction = r.HitPoints - Math.Floor(r.HitPoints);
            actor.Health.SetCurrentAndMaximum(r.HitPoints <= 0 ? 0 : Math.Max(1, (int)Math.Floor(r.HitPoints)), (int)r.MaximumHitPoints);
            actor.Resources.SetCurrentAndMaximum((float)r.MagicPoints, (float)r.Special, (float)r.MaximumMagicPoints, (float)r.MaximumSpecial, tuning.MagicMpRegenPerSecond);
        }

        public void Snapshot()
        {
            if (synchronizing || run == null || closing) return;
            var now = UtcNow;
            foreach (var pair in actors)
            {
                var member = State.Find(pair.Key);
                if (member == null || member.RecoveryState != PartyRecoveryState.Deployed) continue;
                var actor = pair.Value;
                member.SetResources(actor.Health.Current > 0 ? actor.Health.Current + actor.HitPointFraction : 0, actor.Resources.MagicPoints, actor.Resources.Stamina, now);
            }
            State.Settle(now);
        }

        public bool TryStartRun()
        {
            Snapshot();
            if (State.IsDefeated) { Notice = "全員が戦闘不能です。回復まで休息してください。"; return false; }
            TimeStopController.Instance?.Cancel();
            retreating = null; retreatRemaining = 0; queuedSwitch = null;
            bool deployed = false;
            foreach (var member in State.Members) deployed |= member.RecoveryState == PartyRecoveryState.Deployed;
            if (!deployed)
            {
                foreach (var member in State.Members)
                    if (State.TryDeploy(member.Id, UtcNow)) break;
            }
            RefreshMembers();
            int position = 0;
            foreach (var member in State.Members)
            {
                if (member.RecoveryState != PartyRecoveryState.Deployed || !actors.TryGetValue(member.Id, out var actor)) continue;
                actor.Motor.ResetMotor(new Vector3(-1.6f + position++ * 1.2f, 0.05f, 0f));
                actor.Combat.ResetCombat();
                ApplyTuning(member, actor);
            }
            SelectSurvivor();
            checkpoint?.Invoke();
            return Active != null && Active.Targetable;
        }

        private void Update()
        {
            if (run == null || closing) return;
            if (TimeStopController.Instance != null) TimeStopController.Instance.SetPaused(run.Mode != CombatRunMode.Playing);
            State.Settle(UtcNow);
            foreach (var member in State.Members)
                if (member.RecoveryState != PartyRecoveryState.Deployed && actors.TryGetValue(member.Id, out var resting)) Hydrate(member, resting);
            checkpointRemaining -= Time.unscaledDeltaTime;
            if (checkpointRemaining <= 0f) { checkpointRemaining = 5f; Snapshot(); checkpoint?.Invoke(); }
            bool playing = run.Mode == CombatRunMode.Playing;
            foreach (var actor in actors.Values)
            {
                actor.Motor.CanMove = playing && actor.Targetable;
                actor.Combat.IsManual = actor == Active;
                actor.Motor.Commands = default;
                actor.Combat.Commands = default;
            }
            if (!playing) return;
            if (input.SwitchCharacterPressed) RequestSwitch();
            if (queuedSwitch != null && !TimeStopped && Active != null && Active.Combat.CanSwitch)
            {
                string id = queuedSwitch; queuedSwitch = null; RequestSwitch(id);
            }
            if (retreating != null && !TimeStopped)
            {
                retreatRemaining -= Time.deltaTime;
                if (retreatRemaining <= 0f) FinishRetreat();
            }
            foreach (var actor in actors.Values)
            {
                if (!actor.Targetable || actor == retreating) continue;
                ActorCommandFrame commands = actor == Active ? HumanCommands() : AiCommands(actor);
                actor.Motor.Commands = commands;
                actor.Combat.Commands = commands;
            }
        }

        private ActorCommandFrame HumanCommands()
        {
            Vector2 move = input.Move;
            if (TimeStopped) move.y = -move.y;
            return new ActorCommandFrame { Move = move, Jump = input.JumpPressed, Dodge = input.DodgePressed,
                Sword = input.SwordPressed, Magic = input.MagicPressed, Special = input.SpecialPressed };
        }

        private ActorCommandFrame AiCommands(PartyActor actor)
        {
            var result = new ActorCommandFrame { WorldSpace = true };
            if (TimeStopController.Instance != null && TimeStopController.Instance.IsFrozen(actor.gameObject)) return result;
            var enemy = PartyTargeting.NearestEnemy(actor.transform.position);
            Vector3 destination = enemy != null ? enemy.transform.position : Active.transform.position + Vector3.right;
            Vector3 offset = Vector3.ProjectOnPlane(destination - actor.transform.position, Vector3.up);
            float distance = offset.magnitude;
            float desiredRange = enemy == null ? 1.2f : actor.IsCat ? 3.5f : 0.85f;
            Vector3 direction = offset.normalized;
            if (distance > desiredRange) result.Move = new Vector2(direction.x, direction.z);
            else if (actor.IsCat && enemy != null && distance < 1.8f) result.Move = new Vector2(-direction.x, -direction.z);
            if (enemy == null) return result;
            if (distance <= (actor.IsCat ? 6f : tuning.SwordRange * 0.95f))
            {
                actor.Motor.FaceTowards(enemy.transform.position);
                result.Sword = true;
                result.Magic = actor.Combat.CanCastMajorMagic;
                if (result.Magic) result.Sword = false;
            }
            var goblin = enemy.GetComponent<GoblinController>();
            var slime = enemy.GetComponent<SlimeController>();
            if ((goblin != null && goblin.IsWindingUp && goblin.Threatens(actor.transform.position, 1.25f)) ||
                (slime != null && slime.IsWindingUp && distance < tuning.SlimeAttackRange * 1.4f))
            {
                result.Dodge = true;
                result.Move = new Vector2(-direction.z, direction.x);
                result.Sword = result.Magic = false;
            }
            return result;
        }

        public bool RequestSwitch(string id = null)
        {
            if (run.Mode != CombatRunMode.Playing || TimeStopped || retreating != null) return false;
            foreach (var actor in actors.Values)
            {
                if (actor == Active || !actor.Targetable || (id != null && actor.MemberId != id)) continue;
                if (Active != null && !Active.Combat.CanSwitch) { queuedSwitch = actor.MemberId; return true; }
                SetActive(actor); return true;
            }
            return false;
        }

        private void SetActive(PartyActor actor)
        {
            Active = actor;
            State.TrySetPreferredControlledMember(actor.MemberId);
            follow?.Invoke(actor.transform);
            Notice = actor.IsCat ? "猫少女を操作中" : "主人公を操作中";
            Changed?.Invoke();
        }

        public bool ToggleParticipation(string id)
        {
            if (TimeStopped || retreating != null) return false;
            var member = State.Find(id);
            if (member == null || member.RecoveryState == PartyRecoveryState.KnockedOut) return false;
            if (member.RecoveryState == PartyRecoveryState.Resting)
            {
                if (!State.TryDeploy(id, UtcNow)) return false;
                var actor = actors[id];
                actor.gameObject.SetActive(true);
                actor.Motor.ResetMotor(Active != null ? Active.transform.position + Vector3.right : new Vector3(-1.6f, 0.05f, 0f));
                Hydrate(member, actor); ApplyTuning(member, actor);
                actor.Combat.ResetCombat();
                if (Active == null || !Active.Targetable) SetActive(actor);
                Notice = "出撃しました";
            }
            else
            {
                var actor = actors[id];
                if (run.Mode == CombatRunMode.Playing)
                {
                    if (!actor.Combat.CanSwitch) return false;
                    retreating = actor; retreatRemaining = 2f; Notice = "退避中… 被弾すると中断します";
                    return true;
                }
                Snapshot();
                bool last = !State.TryRest(id, UtcNow);
                if (last) State.RestAllLiving(UtcNow);
                actor.gameObject.SetActive(false);
                if (last) run.EndRunForRest(); else if (Active == actor) SelectSurvivor(false);
                Notice = "休息を開始しました";
            }
            checkpoint?.Invoke(); Changed?.Invoke(); return true;
        }

        private void FinishRetreat()
        {
            var actor = retreating; retreating = null; retreatRemaining = 0f;
            Snapshot();
            if (!State.TryRest(actor.MemberId, UtcNow)) State.RestAllLiving(UtcNow);
            actor.Combat.CancelPendingActions(); actor.gameObject.SetActive(false);
            bool any = false;
            foreach (var other in actors.Values) any |= other.Targetable;
            if (!any) run.EndRunForRest(); else if (Active == actor) SelectSurvivor(false);
            Notice = "休息中：HP・MPは約15分で全回復";
            checkpoint?.Invoke(); Changed?.Invoke();
        }

        private void OnDamaged(Health health, DamageInfo _)
        {
            if (retreating != null && retreating.Health == health)
            { retreating = null; retreatRemaining = 0; Notice = "被弾により退避を中断しました"; }
        }

        private void OnDied(Health health, DamageInfo _)
        {
            foreach (var actor in actors.Values)
            {
                if (actor.Health != health) continue;
                State.KnockOut(actor.MemberId, UtcNow);
                actor.Combat.CancelPendingActions(); actor.Motor.CanMove = false;
                actor.gameObject.SetActive(false);
                if (actor == retreating) { retreating = null; retreatRemaining = 0; }
            }
            if (TimeStopController.Instance != null && TimeStopController.Instance.Caster == health.gameObject) TimeStopController.Instance.Cancel();
            if (State.IsDefeated) run.EndRunForDefeat();
            else if (Active == null || !Active.Targetable) SelectSurvivor();
            checkpoint?.Invoke(); Changed?.Invoke();
        }

        private void SelectSurvivor(bool deployReserve = true)
        {
            if (Active != null && Active.Targetable) return;
            foreach (var actor in actors.Values) if (actor.Targetable) { SetActive(actor); return; }
            if (!deployReserve) return;
            foreach (var member in State.Members)
            {
                if (!State.TryDeploy(member.Id, UtcNow)) continue;
                var actor = actors[member.Id]; actor.gameObject.SetActive(true);
                Hydrate(member, actor); SetActive(actor); return;
            }
        }

        private void OnRunStateChanged()
        {
            bool playing = run.Mode == CombatRunMode.Playing;
            foreach (var actor in actors.Values)
            {
                actor.Motor.CanMove = playing && actor.Targetable;
                if (run.Mode == CombatRunMode.RivalEncounter || run.Mode == CombatRunMode.GameOver || run.Mode == CombatRunMode.Ready) actor.Combat.CancelPendingActions();
            }
            if (TimeStopController.Instance != null) TimeStopController.Instance.SetPaused(!playing);
        }

        public void PrepareForExit()
        {
            if (closing) return;
            Snapshot();
            State.RestAllLiving(UtcNow);
            TimeStopController.Instance?.Cancel();
            closing = true;
        }

        public void BeginProfileReplacement()
        {
            synchronizing = true;
            TimeStopController.Instance?.Cancel();
            retreating = null; queuedSwitch = null;
            foreach (var actor in actors.Values) actor.Combat.CancelPendingActions();
        }

        public void EndProfileReplacement()
        {
            synchronizing = false;
            State.Settle(UtcNow);
            State.RestAllLiving(UtcNow);
            foreach (var actor in actors.Values)
                if (State.Find(actor.MemberId) == null) actor.gameObject.SetActive(false);
            RefreshMembers();
            run.EndRunForRest();
        }
        private void OnDestroy()
        {
            if (run == null) return;
            run.Progression.Changed -= OnProgressionChanged;
            run.StateChanged -= OnRunStateChanged;
        }
    }
}
