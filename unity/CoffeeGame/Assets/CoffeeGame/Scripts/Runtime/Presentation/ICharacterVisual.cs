using UnityEngine;

namespace CoffeeGame.Presentation
{
    public enum CharacterAction
    {
        Idle,
        Walk,
        Run,
        Jump,
        Sword,
        AirSlash,
        Plunge,
        SpinCharge,
        SpinRelease,
        MagicCharge,
        Hurt,
        Defeated,
        Fall,
        Land,
        MagicRelease,
        AttackWindup,
        Attack,
        Dodge
    }

    public interface ICharacterVisual
    {
        void ResetState(Vector3 worldDirection);
        void SetFacing(Vector3 worldDirection);
        void SetLocomotion(CharacterAction action, float normalizedSpeed);
        void PlayAction(CharacterAction action, float duration);
        void SetAirHeight(float height);
        void SetTint(Color color);
    }

    public static class CharacterVisualTransitionPolicy
    {
        public static bool IsForcedPhysicsTransition(CharacterAction current, CharacterAction next)
        {
            if (next == CharacterAction.Fall)
            {
                return current == CharacterAction.Jump;
            }

            if (next == CharacterAction.Idle)
            {
                return current == CharacterAction.Dodge;
            }

            if (next != CharacterAction.Land)
            {
                return false;
            }

            return current == CharacterAction.Jump ||
                current == CharacterAction.Fall ||
                current == CharacterAction.Plunge ||
                current == CharacterAction.AirSlash ||
                current == CharacterAction.Hurt ||
                current == CharacterAction.Dodge;
        }
    }
}
