namespace CoffeeGame.Combat
{
    // Windup and committed strike are counterable; recovery and stagger are not.
    public interface IEnemyAttack
    {
        bool IsAttacking { get; }
        void Parry(float seconds);
    }
}
