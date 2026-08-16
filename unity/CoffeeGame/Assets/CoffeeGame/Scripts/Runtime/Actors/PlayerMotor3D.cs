using System;
using CoffeeGame.Domain;
using CoffeeGame.Input;
using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Actors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerMotor3D : MonoBehaviour
    {
        private const float MinimumPlungeAirTime = 0.08f;

        private CharacterController characterController;
        private GameInputReader input;
        private CombatTuning tuning;
        private Camera movementCamera;
        private ICharacterVisual visual;
        private float verticalSpeed;
        private float sustainedDirectionTime;
        private float landingLockRemaining;
        private Vector3 previousInputDirection;
        private Vector3 planarVelocity;
        private float airborneTime;
        private bool plungeInputWasHeld;
        private bool fallVisualPlayed;

        public event Action Jumped;
        public event Action PlungeStarted;
        public event Action<Vector3> Landed;

        public Vector3 Facing { get; private set; } = Vector3.back;
        public bool IsGrounded { get; private set; }
        public bool IsPlunging { get; private set; }
        public bool CanMove { get; set; } = true;
        public float MovementScale { get; set; } = 1f;
        public float SpeedMultiplier { get; set; } = 1f;
        public float VerticalSpeed => verticalSpeed;
        public bool CanAct => CanMove && landingLockRemaining <= 0f && !IsPlunging;

        public void Initialize(GameInputReader inputReader, CombatTuning combatTuning, Camera cameraForMovement, ICharacterVisual characterVisual)
        {
            input = inputReader;
            tuning = combatTuning;
            movementCamera = cameraForMovement;
            visual = characterVisual;
            characterController = GetComponent<CharacterController>();
            IsGrounded = characterController.isGrounded;
            verticalSpeed = -1f;
        }

        public void ResetMotor(Vector3 position)
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;
            verticalSpeed = -1f;
            planarVelocity = Vector3.zero;
            sustainedDirectionTime = 0f;
            landingLockRemaining = 0f;
            airborneTime = 0f;
            plungeInputWasHeld = false;
            fallVisualPlayed = false;
            IsPlunging = false;
            IsGrounded = characterController.isGrounded;
            MovementScale = 1f;
            SpeedMultiplier = 1f;
            // Start toward the fixed camera so the character's face and ready
            // pose are readable. The first movement input immediately replaces it.
            Facing = Vector3.back;
            previousInputDirection = Vector3.zero;
            visual?.ResetState(Facing);
        }

        public void AddKnockback(Vector3 worldVelocity)
        {
            planarVelocity += Vector3.ProjectOnPlane(worldVelocity, Vector3.up);
        }

        private void Update()
        {
            if (input == null || tuning == null || characterController == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector2 moveInput = input.Move;
            bool plungeInputHeld = moveInput.y <= -0.72f;
            bool plungeInputPressed = plungeInputHeld && !plungeInputWasHeld;
            landingLockRemaining = Mathf.Max(0f, landingLockRemaining - deltaTime);
            bool wasGrounded = IsGrounded;
            IsGrounded = characterController.isGrounded;
            airborneTime = IsGrounded ? 0f : airborneTime + deltaTime;

            if (IsGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -1.5f;
            }

            if (CanMove && landingLockRemaining <= 0f && IsGrounded && input.JumpPressed)
            {
                verticalSpeed = tuning.JumpVelocity;
                IsGrounded = false;
                airborneTime = 0f;
                fallVisualPlayed = false;
                Jumped?.Invoke();
                // The visual is released by the physical apex (Fall), not by a
                // guessed animation duration that can expire mid-ascent.
                visual?.PlayAction(CharacterAction.Jump, float.PositiveInfinity);
            }

            if (CanMove && !IsGrounded && !IsPlunging && airborneTime >= MinimumPlungeAirTime && plungeInputPressed)
            {
                IsPlunging = true;
                verticalSpeed = -tuning.PlungeSpeed;
                PlungeStarted?.Invoke();
                // Contact, rather than a timer, ends the plunge pose.
                // The HD-2D frame anchors its boots with a higher pivot, leaving
                // the sword tip below the actor root. Tune the perceived tip/floor
                // contact only after Play-mode QA; damage still belongs to the
                // physical CharacterController landing below.
                visual?.PlayAction(CharacterAction.Plunge, float.PositiveInfinity);
            }

            Vector3 desiredDirection = GetCameraRelativeDirection(moveInput);
            float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
            UpdateRunState(desiredDirection, inputMagnitude, deltaTime);

            float moveSpeed = sustainedDirectionTime >= tuning.RunHoldSeconds ? tuning.RunSpeed : tuning.WalkSpeed;
            float airMultiplier = IsGrounded ? 1f : tuning.AirControl;
            float effectiveScale = CanMove && landingLockRemaining <= 0f ? Mathf.Clamp01(MovementScale) : 0f;
            float effectiveMoveSpeed = moveSpeed * Mathf.Clamp(SpeedMultiplier, 0.2f, 10f);
            Vector3 desiredPlanarVelocity = desiredDirection * (effectiveMoveSpeed * inputMagnitude * airMultiplier * effectiveScale);
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredPlanarVelocity, 14f * deltaTime);

            if (IsPlunging)
            {
                planarVelocity *= Mathf.Pow(0.15f, deltaTime);
                verticalSpeed = -tuning.PlungeSpeed;
            }
            else
            {
                verticalSpeed -= tuning.Gravity * deltaTime;
            }

            if (!IsGrounded && !IsPlunging && !fallVisualPlayed && verticalSpeed <= 0f)
            {
                fallVisualPlayed = true;
                visual?.PlayAction(CharacterAction.Fall, float.PositiveInfinity);
            }

            CollisionFlags flags = characterController.Move((planarVelocity + Vector3.up * verticalSpeed) * deltaTime);
            bool groundedAfterMove = (flags & CollisionFlags.Below) != 0 || characterController.isGrounded;
            if (groundedAfterMove && !wasGrounded)
            {
                bool landedFromPlunge = IsPlunging;
                IsPlunging = false;
                IsGrounded = true;
                airborneTime = 0f;
                fallVisualPlayed = false;
                verticalSpeed = -1.5f;
                if (landedFromPlunge)
                {
                    landingLockRemaining = tuning.LandingLag;
                    visual?.PlayAction(CharacterAction.Land, Mathf.Max(0.18f, tuning.LandingLag));
                }
                else
                {
                    visual?.PlayAction(CharacterAction.Land, 0.18f);
                }
                Landed?.Invoke(transform.position);
            }
            else
            {
                IsGrounded = groundedAfterMove;
            }

            plungeInputWasHeld = plungeInputHeld;

            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                Facing = desiredDirection.normalized;
                visual?.SetFacing(Facing);
            }

            CharacterAction locomotion = inputMagnitude < 0.08f ? CharacterAction.Idle :
                sustainedDirectionTime >= tuning.RunHoldSeconds ? CharacterAction.Run : CharacterAction.Walk;
            if (IsGrounded && !IsPlunging)
            {
                visual?.SetLocomotion(locomotion, effectiveMoveSpeed <= 0f ? 0f : planarVelocity.magnitude / effectiveMoveSpeed);
            }
            visual?.SetAirHeight(Mathf.Max(0f, transform.position.y));
        }

        private Vector3 GetCameraRelativeDirection(Vector2 move)
        {
            Vector3 forward = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
            Vector3 right = movementCamera != null ? movementCamera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
            Vector3 direction = right * move.x + forward * move.y;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private void UpdateRunState(Vector3 desiredDirection, float magnitude, float deltaTime)
        {
            if (magnitude < 0.55f || desiredDirection.sqrMagnitude < 0.01f)
            {
                sustainedDirectionTime = 0f;
                previousInputDirection = Vector3.zero;
                return;
            }

            Vector3 direction = desiredDirection.normalized;
            if (previousInputDirection.sqrMagnitude > 0f && Vector3.Dot(previousInputDirection, direction) >= 0.94f)
            {
                sustainedDirectionTime += deltaTime;
            }
            else
            {
                sustainedDirectionTime = 0f;
            }
            previousInputDirection = direction;
        }
    }
}
