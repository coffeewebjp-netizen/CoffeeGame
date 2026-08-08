using System;

namespace CoffeeGame.Domain
{
    /// <summary>
    /// The progression rewards granted by one claimable game event.
    /// </summary>
    [Serializable]
    public readonly struct RewardBundle : IEquatable<RewardBundle>
    {
        public static readonly RewardBundle None = new RewardBundle(0, 0, 0);

        public RewardBundle(int experience, int gold, int slimeJelly)
        {
            if (experience < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(experience), "Experience cannot be negative.");
            }

            if (gold < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gold), "Gold cannot be negative.");
            }

            if (slimeJelly < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slimeJelly), "Slime jelly cannot be negative.");
            }

            Experience = experience;
            Gold = gold;
            SlimeJelly = slimeJelly;
        }

        public int Experience { get; }

        public int Gold { get; }

        public int SlimeJelly { get; }

        public bool IsEmpty => this == None;

        public static RewardBundle operator +(RewardBundle left, RewardBundle right)
        {
            return new RewardBundle(
                checked(left.Experience + right.Experience),
                checked(left.Gold + right.Gold),
                checked(left.SlimeJelly + right.SlimeJelly));
        }

        public bool Equals(RewardBundle other)
        {
            return Experience == other.Experience
                && Gold == other.Gold
                && SlimeJelly == other.SlimeJelly;
        }

        public override bool Equals(object obj)
        {
            return obj is RewardBundle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Experience;
                hashCode = (hashCode * 397) ^ Gold;
                hashCode = (hashCode * 397) ^ SlimeJelly;
                return hashCode;
            }
        }

        public static bool operator ==(RewardBundle left, RewardBundle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RewardBundle left, RewardBundle right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"XP {Experience}, Gold {Gold}, Slime Jelly {SlimeJelly}";
        }
    }
}
