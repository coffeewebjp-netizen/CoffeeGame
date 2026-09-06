using System;
using CoffeeGame.Combat;
using CoffeeGame.Enemies;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class GoblinCharacterVisual : MonoBehaviour, ICharacterVisual
    {
        private Animator animator;
        private Renderer[] renderers;
        private Color[] baseColors;
        private MaterialPropertyBlock tint;
        private LineRenderer warning;
        private CharacterAction locomotion = CharacterAction.Idle;
        private CharacterAction current = CharacterAction.Idle;
        private float actionRemaining;
        private bool defeated;
        public Animator Animator => animator;

        public void Initialize()
        {
            tint = new MaterialPropertyBlock();
            var prefab = Resources.Load<GameObject>("Models/Goblin/forest-goblin");
            var controller = Resources.Load<RuntimeAnimatorController>("Animations/Goblin/GoblinRuntime");
            if (prefab == null || controller == null)
                throw new InvalidOperationException("Goblin V8 model/controller is missing.");
            var model = Instantiate(prefab, transform, false);
            model.name = "Forest Goblin Model";
            animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            renderers = model.GetComponentsInChildren<Renderer>();
            var bodyMaterial = Resources.Load<Material>("Materials/ForestGoblinLit");
            var clubMaterial = Resources.Load<Material>("Materials/GoblinClubLit");
            var gripMaterial = Resources.Load<Material>("Materials/GoblinGripLit");
            foreach (Renderer renderer in renderers)
            {
                renderer.sharedMaterial = renderer.name == "ClubGripGlove" ? gripMaterial :
                    renderer.name == "GoblinClub" ? clubMaterial : bodyMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].sharedMaterial.GetColor("_BaseColor");
            var warningRoot = new GameObject("Goblin strike warning");
            warningRoot.transform.SetParent(transform, false);
            warning = warningRoot.AddComponent<LineRenderer>();
            warning.sharedMaterial = Resources.Load<Material>("Materials/GoblinWarning");
            warning.useWorldSpace = false;
            warning.loop = true;
            warning.widthMultiplier = 0.028f;
            warning.numCornerVertices = 2;
            warning.shadowCastingMode = ShadowCastingMode.Off;
            warning.receiveShadows = false;
            warning.positionCount = 26;
            warning.SetPosition(0, new Vector3(0f, 0.065f, 0f));
            float halfAngle = Mathf.Acos(0.64f);
            for (int i = 0; i < 25; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / 24f);
                warning.SetPosition(i + 1, new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) *
                    GoblinController.AttackRange + Vector3.up * 0.065f);
            }
            warning.enabled = false;
            ResetState(Vector3.back);
        }

        public void ResetState(Vector3 direction)
        {
            defeated = false;
            actionRemaining = 0f;
            locomotion = current = CharacterAction.Idle;
            SetFacing(direction);
            SetTint(Color.white);
            if (warning != null) warning.enabled = false;
            if (animator != null) { animator.speed = 1f; animator.Play("Idle", 0, 0f); }
        }
        public void SetFacing(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction);
        }
        public void SetLocomotion(CharacterAction action, float normalizedSpeed)
        {
            locomotion = action == CharacterAction.Walk || action == CharacterAction.Run
                ? CharacterAction.Walk : CharacterAction.Idle;
            if (!defeated && actionRemaining <= 0f && current != locomotion) Play(locomotion, 0f);
        }
        public void PlayAction(CharacterAction action, float duration)
        {
            if (defeated) return;
            defeated = action == CharacterAction.Defeated;
            actionRemaining = Mathf.Max(0.01f, duration);
            if (warning != null) warning.enabled = action == CharacterAction.AttackWindup;
            Play(action, actionRemaining);
        }
        private void Play(CharacterAction action, float duration)
        {
            current = action;
            if (animator == null) return;
            float length = 1f;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == action.ToString()) { length = clip.length; break; }
            animator.speed = duration > 0f ? length / duration : 1f;
            animator.CrossFadeInFixedTime(action.ToString(), action == CharacterAction.Attack ? 0.02f : 0.055f);
        }
        private void Update()
        {
            float deltaTime = CombatClock.DeltaTime(gameObject);
            if (actionRemaining <= 0f || deltaTime <= 0f) return;
            actionRemaining -= deltaTime;
            if (actionRemaining > 0f) return;
            if (defeated) { if (animator != null) animator.speed = 0f; return; }
            if (warning != null) warning.enabled = false;
            Play(locomotion, 0f);
        }
        public void SetAirHeight(float height) { }
        public void SetTint(Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                tint.SetColor("_BaseColor", baseColors[i] * color);
                renderers[i].SetPropertyBlock(tint);
            }
        }
    }
}
