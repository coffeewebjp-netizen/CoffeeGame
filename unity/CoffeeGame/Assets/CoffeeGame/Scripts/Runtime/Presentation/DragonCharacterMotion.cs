using UnityEngine;

namespace CoffeeGame.Presentation
{
    public sealed class DragonCharacterMotion : MonoBehaviour
    {
        private Animator animator;
        public void Initialize(Animator value) => animator = value;
        public void Play(string state, float duration)
        {
            if (animator == null || !animator.HasState(0, Animator.StringToHash(state))) return;
            animator.CrossFadeInFixedTime(state, .055f, 0, 0f);
        }
    }
}
