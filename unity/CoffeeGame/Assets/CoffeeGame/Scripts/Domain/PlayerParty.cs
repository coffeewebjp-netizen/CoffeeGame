using System;
using System.Collections.Generic;

namespace CoffeeGame.Domain
{
    public static class PartyMemberIds
    {
        public const string Hero = "hero-swordsman-001";
        public const string CatMage = RivalCharacterIds.WeaknessChallenger;
    }

    public enum PartyRecoveryState
    {
        Deployed,
        Resting,
        KnockedOut
    }

    public sealed class PartyMemberResources
    {
        internal PartyMemberResources(
            double hitPoints,
            double maximumHitPoints,
            double magicPoints,
            double maximumMagicPoints,
            double special,
            double maximumSpecial)
        {
            MaximumHitPoints = RequirePositiveFinite(maximumHitPoints, nameof(maximumHitPoints));
            MaximumMagicPoints = RequireNonNegativeFinite(maximumMagicPoints, nameof(maximumMagicPoints));
            MaximumSpecial = RequireNonNegativeFinite(maximumSpecial, nameof(maximumSpecial));
            HitPoints = ClampFinite(hitPoints, MaximumHitPoints, nameof(hitPoints));
            MagicPoints = ClampFinite(magicPoints, MaximumMagicPoints, nameof(magicPoints));
            Special = ClampFinite(special, MaximumSpecial, nameof(special));
        }

        public double HitPoints { get; internal set; }
        public double MaximumHitPoints { get; internal set; }
        public double MagicPoints { get; internal set; }
        public double MaximumMagicPoints { get; internal set; }
        public double Special { get; internal set; }
        public double MaximumSpecial { get; internal set; }
        public bool IsAlive => HitPoints > 0d;

        internal static double RequirePositiveFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static double RequireNonNegativeFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static double ClampFinite(double value, double maximum, string parameterName)
        {
            RequireNonNegativeFinite(value, parameterName);
            return Math.Min(value, maximum);
        }
    }

    /// <summary>
    /// Persistent progression, combat resources, and real-time recovery for one party member.
    /// The class has no Unity clock dependency; callers inject UTC or elapsed real seconds.
    /// </summary>
    public sealed class PartyMember
    {
        public const double KnockoutRecoverySeconds = 600d;
        public const double FullRestRecoverySeconds = 900d;

        private readonly Func<string, TalentGrowthProfile> growthProfileResolver;
        internal Func<string, TalentGrowthProfile> GrowthProfileResolver => growthProfileResolver;

        public PartyMember(
            string id,
            int level,
            int experience,
            PlayerStatus status,
            double hitPoints = 1d,
            double maximumHitPoints = 1d,
            double magicPoints = 0d,
            double maximumMagicPoints = 0d,
            double special = 0d,
            double maximumSpecial = 0d,
            PartyRecoveryState recoveryState = PartyRecoveryState.Resting,
            DateTime? recoveryAnchorUtc = null,
            DateTime? recoverableAtUtc = null,
            Func<string, TalentGrowthProfile> talentGrowthProfileResolver = null,
            bool resourcesInitialized = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A stable party member ID is required.", nameof(id));
            }
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            if (experience < 0 || experience >= PlayerProgression.GetExperienceRequiredForNextLevel(level))
            {
                throw new ArgumentOutOfRangeException(nameof(experience));
            }
            if (!Enum.IsDefined(typeof(PartyRecoveryState), recoveryState))
            {
                throw new ArgumentOutOfRangeException(nameof(recoveryState));
            }

            Id = id.Trim();
            Level = level;
            Experience = experience;
            Status = status ?? throw new ArgumentNullException(nameof(status));
            Resources = new PartyMemberResources(
                hitPoints,
                maximumHitPoints,
                magicPoints,
                maximumMagicPoints,
                special,
                maximumSpecial);
            RecoveryState = recoveryState;
            ResourcesInitialized = resourcesInitialized;
            RecoveryAnchorUtc = NormalizeUtc(recoveryAnchorUtc ?? DateTime.UtcNow);
            RecoverableAtUtc = recoverableAtUtc.HasValue
                ? NormalizeUtc(recoverableAtUtc.Value)
                : (DateTime?)null;
            growthProfileResolver = talentGrowthProfileResolver ?? TalentGrowthCatalog.Resolve;

            if (RecoveryState == PartyRecoveryState.KnockedOut)
            {
                Resources.HitPoints = 0d;
                Resources.Special = 0d;
                if (!RecoverableAtUtc.HasValue)
                {
                    RecoverableAtUtc = RecoveryAnchorUtc.AddSeconds(KnockoutRecoverySeconds);
                }
            }
            else
            {
                RecoverableAtUtc = null;
            }
        }

        public string Id { get; }
        internal event Action<PartyMember> Changed;
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int ExperienceRequiredForNextLevel => PlayerProgression.GetExperienceRequiredForNextLevel(Level);
        public PlayerStatus Status { get; private set; }
        public PartyMemberResources Resources { get; }
        public PartyRecoveryState RecoveryState { get; private set; }
        public DateTime RecoveryAnchorUtc { get; private set; }
        public DateTime? RecoverableAtUtc { get; private set; }
        public bool ResourcesInitialized { get; private set; }
        public bool IsAlive => Resources.IsAlive && RecoveryState != PartyRecoveryState.KnockedOut;

        public double KnockoutRemainingSeconds(DateTime utcNow)
        {
            if (RecoveryState != PartyRecoveryState.KnockedOut || !RecoverableAtUtc.HasValue)
            {
                return 0d;
            }

            DateTime now = NormalizeUtc(utcNow);
            if (now < RecoveryAnchorUtc)
            {
                now = RecoveryAnchorUtc;
            }
            return Math.Max(0d, (RecoverableAtUtc.Value - now).TotalSeconds);
        }

        public void AddExperience(int experience)
        {
            if (experience < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(experience));
            }

            int nextExperience = checked(Experience + experience);
            int nextLevel = Level;
            while (nextExperience >= PlayerProgression.GetExperienceRequiredForNextLevel(nextLevel))
            {
                nextExperience -= PlayerProgression.GetExperienceRequiredForNextLevel(nextLevel);
                nextLevel = checked(nextLevel + 1);
            }

            int levelsGained = nextLevel - Level;
            if (levelsGained > 0)
            {
                Status = Status.ApplyLevelGrowth(levelsGained, growthProfileResolver(Status.TalentId));
            }

            Level = nextLevel;
            Experience = nextExperience;
            if (experience > 0)
            {
                Changed?.Invoke(this);
            }
        }

        internal void ValidateExperienceReward(int experience)
        {
            if (experience < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(experience));
            }

            int nextExperience = checked(Experience + experience);
            int nextLevel = Level;
            while (nextExperience >= PlayerProgression.GetExperienceRequiredForNextLevel(nextLevel))
            {
                nextExperience -= PlayerProgression.GetExperienceRequiredForNextLevel(nextLevel);
                nextLevel = checked(nextLevel + 1);
            }

            int levelsGained = nextLevel - Level;
            if (levelsGained > 0)
            {
                Status.ApplyLevelGrowth(levelsGained, growthProfileResolver(Status.TalentId));
            }
        }

        public bool Settle(DateTime utcNow)
        {
            DateTime now = NormalizeUtc(utcNow);
            if (now <= RecoveryAnchorUtc)
            {
                return false;
            }

            double elapsedSeconds = (now - RecoveryAnchorUtc).TotalSeconds;
            bool changed = ApplyElapsed(elapsedSeconds);
            RecoveryAnchorUtc = now;
            if (changed)
            {
                Changed?.Invoke(this);
            }
            return changed;
        }

        public bool SettleElapsed(double elapsedSeconds)
        {
            PartyMemberResources.RequireNonNegativeFinite(elapsedSeconds, nameof(elapsedSeconds));
            if (elapsedSeconds == 0d)
            {
                return false;
            }

            bool changed = ApplyElapsed(elapsedSeconds);
            RecoveryAnchorUtc = RecoveryAnchorUtc.AddSeconds(elapsedSeconds);
            if (changed)
            {
                Changed?.Invoke(this);
            }
            return changed;
        }

        public void SetResources(
            double hitPoints,
            double magicPoints,
            double special,
            DateTime utcNow)
        {
            Settle(utcNow);
            double nextHitPoints = ClampResource(hitPoints, Resources.MaximumHitPoints, nameof(hitPoints));
            double nextMagicPoints = ClampResource(magicPoints, Resources.MaximumMagicPoints, nameof(magicPoints));
            double nextSpecial = ClampResource(special, Resources.MaximumSpecial, nameof(special));
            if (nextHitPoints != Resources.HitPoints
                || nextMagicPoints != Resources.MagicPoints
                || nextSpecial != Resources.Special)
            {
                Resources.HitPoints = nextHitPoints;
                Resources.MagicPoints = nextMagicPoints;
                Resources.Special = nextSpecial;
                Changed?.Invoke(this);
            }
        }

        public void SetMaximumResources(
            double maximumHitPoints,
            double maximumMagicPoints,
            double maximumSpecial,
            DateTime utcNow)
        {
            Settle(utcNow);
            double nextMaximumHitPoints = PartyMemberResources.RequirePositiveFinite(
                maximumHitPoints,
                nameof(maximumHitPoints));
            double nextMaximumMagicPoints = PartyMemberResources.RequireNonNegativeFinite(
                maximumMagicPoints,
                nameof(maximumMagicPoints));
            double nextMaximumSpecial = PartyMemberResources.RequireNonNegativeFinite(
                maximumSpecial,
                nameof(maximumSpecial));
            Resources.MaximumHitPoints = nextMaximumHitPoints;
            Resources.MaximumMagicPoints = nextMaximumMagicPoints;
            Resources.MaximumSpecial = nextMaximumSpecial;
            if (!ResourcesInitialized)
            {
                Resources.HitPoints = nextMaximumHitPoints;
                Resources.MagicPoints = nextMaximumMagicPoints;
                Resources.Special = 0d;
                ResourcesInitialized = true;
            }
            else
            {
                Resources.HitPoints = Math.Min(Resources.HitPoints, nextMaximumHitPoints);
                Resources.MagicPoints = Math.Min(Resources.MagicPoints, nextMaximumMagicPoints);
                Resources.Special = Math.Min(Resources.Special, nextMaximumSpecial);
            }
            Changed?.Invoke(this);
        }

        internal bool TryDeploy(DateTime utcNow)
        {
            Settle(utcNow);
            if (!IsAlive)
            {
                return false;
            }

            if (RecoveryState != PartyRecoveryState.Deployed)
            {
                RecoveryState = PartyRecoveryState.Deployed;
                RecoverableAtUtc = null;
                Changed?.Invoke(this);
            }
            return true;
        }

        internal bool TryRest(DateTime utcNow)
        {
            Settle(utcNow);
            if (!IsAlive)
            {
                return false;
            }

            if (RecoveryState != PartyRecoveryState.Resting)
            {
                RecoveryState = PartyRecoveryState.Resting;
                RecoverableAtUtc = null;
                Changed?.Invoke(this);
            }
            return true;
        }

        internal bool KnockOut(DateTime utcNow)
        {
            Settle(utcNow);
            if (RecoveryState == PartyRecoveryState.KnockedOut)
            {
                return false;
            }
            DateTime anchor = RecoveryAnchorUtc > NormalizeUtc(utcNow)
                ? RecoveryAnchorUtc
                : NormalizeUtc(utcNow);
            Resources.HitPoints = 0d;
            Resources.Special = 0d;
            RecoveryState = PartyRecoveryState.KnockedOut;
            RecoveryAnchorUtc = anchor;
            RecoverableAtUtc = anchor.AddSeconds(KnockoutRecoverySeconds);
            Changed?.Invoke(this);
            return true;
        }

        private bool ApplyElapsed(double elapsedSeconds)
        {
            if (RecoveryState == PartyRecoveryState.Deployed)
            {
                return false;
            }

            bool changed = false;
            double restSeconds = elapsedSeconds;
            if (RecoveryState == PartyRecoveryState.KnockedOut)
            {
                double knockoutSecondsRemaining = RecoverableAtUtc.HasValue
                    ? Math.Max(0d, (RecoverableAtUtc.Value - RecoveryAnchorUtc).TotalSeconds)
                    : KnockoutRecoverySeconds;
                if (elapsedSeconds < knockoutSecondsRemaining)
                {
                    return false;
                }

                restSeconds = elapsedSeconds - knockoutSecondsRemaining;
                Resources.HitPoints = Math.Min(
                    Resources.MaximumHitPoints,
                    Math.Max(1d, Math.Ceiling(Resources.MaximumHitPoints * 0.01d)));
                RecoveryState = PartyRecoveryState.Resting;
                RecoverableAtUtc = null;
                changed = true;
            }

            if (RecoveryState != PartyRecoveryState.Resting || restSeconds <= 0d)
            {
                return changed;
            }

            double nextHitPoints = Math.Min(
                Resources.MaximumHitPoints,
                Resources.HitPoints + (Resources.MaximumHitPoints / FullRestRecoverySeconds * restSeconds));
            double nextMagicPoints = Resources.MaximumMagicPoints <= 0d
                ? 0d
                : Math.Min(
                    Resources.MaximumMagicPoints,
                    Resources.MagicPoints + (Resources.MaximumMagicPoints / FullRestRecoverySeconds * restSeconds));
            if (nextHitPoints != Resources.HitPoints || nextMagicPoints != Resources.MagicPoints)
            {
                Resources.HitPoints = nextHitPoints;
                Resources.MagicPoints = nextMagicPoints;
                changed = true;
            }

            return changed;
        }

        private static double ClampResource(double value, double maximum, string parameterName)
        {
            PartyMemberResources.RequireNonNegativeFinite(value, parameterName);
            return Math.Min(value, maximum);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    public sealed class PlayerParty
    {
        private readonly List<PartyMember> members = new List<PartyMember>();
        private readonly Dictionary<string, PartyMember> memberById =
            new Dictionary<string, PartyMember>(StringComparer.Ordinal);

        public event Action<PartyMember> Changed;

        public PlayerParty(IEnumerable<PartyMember> restoredMembers = null, string preferredControlledMemberId = null)
        {
            if (restoredMembers != null)
            {
                foreach (PartyMember member in restoredMembers)
                {
                    AddMember(member);
                }
            }

            PreferredControlledMemberId = Find(preferredControlledMemberId) != null
                ? preferredControlledMemberId
                : (Find(PartyMemberIds.Hero)?.Id ?? (members.Count > 0 ? members[0].Id : null));
        }

        public IReadOnlyList<PartyMember> Members => members.AsReadOnly();
        public string PreferredControlledMemberId { get; private set; }
        public bool IsDefeated
        {
            get
            {
                if (members.Count == 0)
                {
                    return true;
                }

                foreach (PartyMember member in members)
                {
                    if (member.IsAlive)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public PartyMember Find(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && memberById.TryGetValue(id, out PartyMember member)
                ? member
                : null;
        }

        public bool TryFind(string id, out PartyMember member)
        {
            member = Find(id);
            return member != null;
        }

        public bool TrySetPreferredControlledMember(string id)
        {
            PartyMember member = Find(id);
            if (member == null || member.RecoveryState != PartyRecoveryState.Deployed || !member.IsAlive)
            {
                return false;
            }

            if (!string.Equals(PreferredControlledMemberId, member.Id, StringComparison.Ordinal))
            {
                PreferredControlledMemberId = member.Id;
                Changed?.Invoke(member);
            }
            return true;
        }

        public bool TryDeploy(string id, DateTime utcNow)
        {
            PartyMember member = Find(id);
            return member != null && member.TryDeploy(utcNow);
        }

        public bool TryRest(string id, DateTime utcNow)
        {
            PartyMember member = Find(id);
            if (member == null)
            {
                return false;
            }

            member.Settle(utcNow);
            if (member.RecoveryState == PartyRecoveryState.KnockedOut)
            {
                return false;
            }

            if (member.RecoveryState == PartyRecoveryState.Deployed
                && FindAnotherLivingDeployed(member.Id) == null)
            {
                return false;
            }

            return member.TryRest(utcNow);
        }

        public bool KnockOut(string id, DateTime utcNow)
        {
            PartyMember member = Find(id);
            if (member == null)
            {
                return false;
            }

            if (!member.KnockOut(utcNow))
            {
                return false;
            }
            PartyMember survivor = FindFirstLivingDeployed() ?? FindFirstLivingResting();
            if (survivor != null && survivor.RecoveryState == PartyRecoveryState.Resting)
            {
                survivor.TryDeploy(utcNow);
            }
            if (survivor != null
                && (string.Equals(PreferredControlledMemberId, id, StringComparison.Ordinal)
                    || Find(PreferredControlledMemberId)?.IsAlive != true))
            {
                PreferredControlledMemberId = survivor.Id;
            }

            return true;
        }

        public bool Settle(DateTime utcNow)
        {
            bool changed = false;
            foreach (PartyMember member in members)
            {
                changed |= member.Settle(utcNow);
            }

            return changed;
        }

        public bool SettleElapsed(double elapsedSeconds)
        {
            PartyMemberResources.RequireNonNegativeFinite(elapsedSeconds, nameof(elapsedSeconds));
            bool changed = false;
            foreach (PartyMember member in members)
            {
                changed |= member.SettleElapsed(elapsedSeconds);
            }

            return changed;
        }

        public void RestAllLiving(DateTime utcNow)
        {
            Settle(utcNow);
            foreach (PartyMember member in members)
            {
                if (member.IsAlive)
                {
                    member.TryRest(utcNow);
                }
            }
        }

        internal void AddExperienceToAll(int experience)
        {
            foreach (PartyMember member in members)
            {
                member.AddExperience(experience);
            }
        }

        internal void ValidateExperienceReward(int experience)
        {
            foreach (PartyMember member in members)
            {
                member.ValidateExperienceReward(experience);
            }
        }

        internal bool EnsureCatMemberFrom(PartyMember hero)
        {
            if (Find(PartyMemberIds.CatMage) != null)
            {
                return false;
            }

            AddMember(new PartyMember(
                PartyMemberIds.CatMage,
                hero.Level,
                hero.Experience,
                hero.Status,
                hero.Resources.MaximumHitPoints,
                hero.Resources.MaximumHitPoints,
                hero.Resources.MaximumMagicPoints,
                hero.Resources.MaximumMagicPoints,
                0d,
                hero.Resources.MaximumSpecial,
                hero.RecoveryState == PartyRecoveryState.Deployed
                    ? PartyRecoveryState.Deployed
                    : PartyRecoveryState.Resting,
                hero.RecoveryAnchorUtc,
                talentGrowthProfileResolver: hero.GrowthProfileResolver,
                resourcesInitialized: hero.ResourcesInitialized));
            return true;
        }

        internal void ReplaceFrom(PlayerParty other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            foreach (PartyMember member in members)
            {
                member.Changed -= HandleMemberChanged;
            }
            members.Clear();
            memberById.Clear();
            foreach (PartyMember source in other.members)
            {
                AddMember(Clone(source));
            }
            PreferredControlledMemberId = other.PreferredControlledMemberId;
        }

        private void AddMember(PartyMember member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }
            if (memberById.ContainsKey(member.Id))
            {
                throw new ArgumentException("Party member IDs must be unique.", nameof(member));
            }

            memberById.Add(member.Id, member);
            members.Add(member);
            member.Changed += HandleMemberChanged;
        }

        private PartyMember FindFirstLivingDeployed()
        {
            return members.Find(member => member.IsAlive && member.RecoveryState == PartyRecoveryState.Deployed);
        }

        private void HandleMemberChanged(PartyMember member)
        {
            Changed?.Invoke(member);
        }

        private PartyMember FindFirstLivingResting()
        {
            return members.Find(member => member.IsAlive && member.RecoveryState == PartyRecoveryState.Resting);
        }

        private PartyMember FindAnotherLivingDeployed(string excludedId)
        {
            return members.Find(member => member.Id != excludedId
                && member.IsAlive
                && member.RecoveryState == PartyRecoveryState.Deployed);
        }

        private static PartyMember Clone(PartyMember source)
        {
            return new PartyMember(
                source.Id,
                source.Level,
                source.Experience,
                source.Status,
                source.Resources.HitPoints,
                source.Resources.MaximumHitPoints,
                source.Resources.MagicPoints,
                source.Resources.MaximumMagicPoints,
                source.Resources.Special,
                source.Resources.MaximumSpecial,
                source.RecoveryState,
                source.RecoveryAnchorUtc,
                source.RecoverableAtUtc,
                source.GrowthProfileResolver,
                resourcesInitialized: source.ResourcesInitialized);
        }
    }
}
