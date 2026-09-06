using System;
using CoffeeGame.Domain;
using CoffeeGame.Combat;
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
        private float acrobaticElapsed;
        private float acrobaticDuration;
        private Vector3 acrobaticDirection;
        private AcrobaticMotionPresentation acrobaticVisual;

        public event Action Jumped;
        public event Action Dodged;
        public event Action PlungeStarted;
        public event Action<Vector3> Landed;

        public Vector3 Facing { get; private set; } = Vector3.back;
        public bool IsGrounded { get; private set; }
        public bool IsPlunging { get; private set; }
        public bool IsDodging { get; private set; }
        public bool IsBackflipping { get; private set; }
        public bool IsCartwheeling { get; private set; }
        public bool IsGuardJumping => IsBackflipping || IsCartwheeling;
        public bool IsRunning { get; private set; }
        public bool IsRunningDodge { get; private set; }
        public bool UsesRunningSpinFallback => IsRunningDodge && acrobaticVisual != null && acrobaticVisual.UseRunningSpinFallback;
        public float AcrobaticProgress => acrobaticDuration > 0f ? Mathf.Clamp01(acrobaticElapsed / acrobaticDuration) : 0f;
        public Vector3 AcrobaticDirection => acrobaticDirection;
        public float PlungeRecoveryRemaining => landingLockRemaining;
        public bool IsGuarding { get; set; }
        public bool CanPlunge { get; set; } = true;
        public bool CanMove { get; set; } = true;
        public float MovementScale { get; set; } = 1f;
        public float SpeedMultiplier { get; set; } = 1f;
        public float VerticalSpeed => verticalSpeed;
        public bool CanAct => CanMove && landingLockRemaining <= 0f && !IsPlunging && !IsDodging && !IsGuardJumping;
        public bool UseCommands { get; set; }
        public ActorCommandFrame Commands { get; set; }
        public Health LockedTarget { get; set; }
        public bool HasLockedTarget => LockedTarget != null && LockedTarget.isActiveAndEnabled && LockedTarget.IsAlive;

        private void FaceLockedTarget()
        {
            // Keep each acrobatic/plunge pose stable until its landing, then reacquire facing.
            if (!HasLockedTarget || IsDodging || IsGuardJumping || IsPlunging || landingLockRemaining > 0f) return;
            Vector3 direction = Vector3.ProjectOnPlane(LockedTarget.transform.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude < .001f) return;
            Facing = direction.normalized;
            visual?.SetFacing(Facing);
        }

        public void FaceTowards(Vector3 position)
        {
            Vector3 direction = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            if (direction.sqrMagnitude < 0.001f || IsDodging || IsGuardJumping || IsGuarding) return;
            Facing = direction.normalized;
            visual?.SetFacing(Facing);
        }

        public void Initialize(GameInputReader inputReader, CombatTuning combatTuning, Camera cameraForMovement, ICharacterVisual characterVisual)
        {
            input = inputReader;
            tuning = combatTuning;
            movementCamera = cameraForMovement;
            visual = characterVisual;
            characterController = GetComponent<CharacterController>();
            IsGrounded = characterController.isGrounded;
            verticalSpeed = -1f;
            if (characterVisual is Component component)
            {
                acrobaticVisual = gameObject.AddComponent<AcrobaticMotionPresentation>();
                acrobaticVisual.Initialize(component.transform, gameObject);
            }
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
            IsDodging = false;
            IsBackflipping = IsCartwheeling = false;
            IsRunningDodge = IsRunning = false;
            acrobaticElapsed = acrobaticDuration = 0f;
            acrobaticVisual?.ClearMotion();
            IsGuarding = false;
            IsGrounded = characterController.isGrounded;
            MovementScale = 1f;
            SpeedMultiplier = 1f;
            LockedTarget = null;
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
            Tick(CombatClock.DeltaTime(gameObject));
        }

        public void Tick(float deltaTime)
        {
            if ((input == null && !UseCommands) || tuning == null || characterController == null)
            {
                return;
            }

            if (deltaTime <= 0f || CombatClock.IsPaused) return;
            var stop = TimeStopController.Instance;
            if (stop != null && stop.IsActive && stop.IsFrozen(gameObject)) return;
            if (!CanMove) { CancelAcrobatics(); return; }
            FaceLockedTarget();
            Vector2 moveInput = UseCommands ? Commands.Move : input.Move;
            Vector3 desiredDirection = GetCameraRelativeDirection(moveInput);
            if (IsDodging || IsGuardJumping) acrobaticElapsed += deltaTime;
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

            if (IsGuarding && CanAct && IsGrounded && MovementScale >= .9f &&
                (UseCommands ? Commands.Jump : input.JumpPressed) &&
                (IsBackwardInput(Facing, desiredDirection) || IsSidewaysInput(Facing, desiredDirection)))
            {
                StartGuardJump(desiredDirection);
            }
            else if (CanMove && !IsGuarding && landingLockRemaining <= 0f && IsGrounded && !IsDodging && !IsGuardJumping && MovementScale >= 0.9f && (UseCommands ? Commands.Dodge : input.DodgePressed))
            {
                StartDodge(moveInput);
            }
            else if (CanMove && !IsGuarding && landingLockRemaining <= 0f && IsGrounded && !IsDodging && !IsGuardJumping && (UseCommands ? Commands.Jump : input.JumpPressed))
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

            if (CanMove && CanPlunge && !IsGrounded && !IsPlunging && !IsDodging && !IsGuardJumping && airborneTime >= MinimumPlungeAirTime && plungeInputPressed)
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

            float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
            if (!IsDodging && !IsGuardJumping) UpdateRunState(desiredDirection, inputMagnitude, deltaTime);

            float moveSpeed = sustainedDirectionTime >= tuning.RunHoldSeconds ? tuning.RunSpeed : tuning.WalkSpeed;
            float airMultiplier = IsGrounded ? 1f : tuning.AirControl;
            float effectiveScale = CanMove && landingLockRemaining <= 0f && !IsDodging && !IsGuardJumping ? Mathf.Clamp01(MovementScale) : 0f;
            if (IsGuarding) effectiveScale *= 0.28f;
            float effectiveMoveSpeed = moveSpeed * Mathf.Clamp(SpeedMultiplier, 0.2f, 10f);
            if (!IsDodging && !IsGuardJumping)
            {
                Vector3 desiredPlanarVelocity = desiredDirection * (effectiveMoveSpeed * inputMagnitude * airMultiplier * effectiveScale);
                planarVelocity = Vector3.MoveTowards(planarVelocity, desiredPlanarVelocity, 14f * deltaTime);
            }

            if (IsPlunging)
            {
                planarVelocity *= Mathf.Pow(0.15f, deltaTime);
                verticalSpeed = -tuning.PlungeSpeed;
            }
            else
            {
                verticalSpeed -= tuning.Gravity * deltaTime;
            }

            if (!IsGrounded && !IsPlunging && !IsDodging && !IsGuardJumping && !fallVisualPlayed && verticalSpeed <= 0f)
            {
                fallVisualPlayed = true;
                visual?.PlayAction(CharacterAction.Fall, float.PositiveInfinity);
            }

            CollisionFlags flags = characterController.Move((planarVelocity + Vector3.up * verticalSpeed) * deltaTime);
            bool groundedAfterMove = (flags & CollisionFlags.Below) != 0 || characterController.isGrounded;
            bool rollingOnGround = IsDodging && !IsRunningDodge && acrobaticElapsed < acrobaticDuration;
            if (groundedAfterMove && !rollingOnGround && (!wasGrounded || IsDodging || IsGuardJumping))
            {
                bool landedFromPlunge = IsPlunging;
                bool landedFromDodge = IsDodging;
                bool landedFromBackflip = IsGuardJumping;
                IsPlunging = false;
                IsDodging = false;
                IsBackflipping = IsCartwheeling = false;
                IsRunningDodge = IsRunning = false;
                acrobaticVisual?.ClearMotion();
                IsGrounded = true;
                airborneTime = 0f;
                fallVisualPlayed = false;
                verticalSpeed = -1.5f;
                if (landedFromPlunge)
                {
                    landingLockRemaining = tuning.LandingLag;
                    planarVelocity = Vector3.zero;
                    visual?.PlayAction(CharacterAction.Land, Mathf.Max(0.18f, tuning.LandingLag));
                }
                else if (landedFromDodge || landedFromBackflip)
                {
                    landingLockRemaining = 0f;
                    planarVelocity = Vector3.zero;
                    visual?.PlayAction(CharacterAction.Idle, 0.16f);
                }
                else
                {
                    landingLockRemaining = 0f;
                    visual?.PlayAction(CharacterAction.Land, 0.18f);
                }

                Landed?.Invoke(transform.position);
            }
            else
            {
                IsGrounded = groundedAfterMove;
            }

            plungeInputWasHeld = plungeInputHeld;

            if (!HasLockedTarget && !IsDodging && !IsGuardJumping && !IsGuarding && landingLockRemaining <= 0f && desiredDirection.sqrMagnitude > 0.01f)
            {
                Facing = desiredDirection.normalized;
                visual?.SetFacing(Facing);
            }
            FaceLockedTarget();

            CharacterAction locomotion = inputMagnitude < 0.08f ? CharacterAction.Idle :
                sustainedDirectionTime >= tuning.RunHoldSeconds ? CharacterAction.Run : CharacterAction.Walk;
            if (IsGrounded && !IsPlunging && !IsDodging && !IsGuardJumping && landingLockRemaining <= 0f)
            {
                visual?.SetLocomotion(locomotion, effectiveMoveSpeed <= 0f ? 0f : planarVelocity.magnitude / effectiveMoveSpeed);
            }
            visual?.SetAirHeight(Mathf.Max(0f, transform.position.y));
            if (IsGuardJumping || (IsDodging && (!IsRunningDodge || UsesRunningSpinFallback)))
                acrobaticVisual?.SetMotion(UsesRunningSpinFallback ? AcrobaticMotionKind.RunningSpin :
                    IsCartwheeling ? AcrobaticMotionKind.Cartwheel :
                    IsBackflipping ? AcrobaticMotionKind.Backflip : AcrobaticMotionKind.GroundRoll,
                    AcrobaticProgress, acrobaticDirection);
        }

        private void StartDodge(Vector2 moveInput)
        {
            Vector3 direction = GetCameraRelativeDirection(moveInput);
            if (direction.sqrMagnitude < 0.04f)
            {
                direction = Facing.sqrMagnitude > 0.01f ? -Facing : Vector3.forward;
            }

            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector3.forward;
            }

            direction.Normalize();
            // Latch the pre-input locomotion state; changing direction during
            // the evasive action must not switch its physical/visual style.
            IsRunningDodge = IsRunning && moveInput.magnitude >= .55f;
            IsRunning = false;
            sustainedDirectionTime = 0f;
            IsDodging = true;
            IsGrounded = false;
            airborneTime = 0f;
            fallVisualPlayed = true;
            verticalSpeed = IsRunningDodge ? tuning.JumpVelocity : Mathf.Sqrt(2f * tuning.Gravity * tuning.GroundRollHopHeight);
            planarVelocity = direction * tuning.DodgeSpeed;
            Facing = direction;
            visual?.SetFacing(Facing);
            acrobaticElapsed = 0f;
            acrobaticDuration = IsRunningDodge ? tuning.ExpectedDodgeAirSeconds : tuning.GroundRollSeconds;
            acrobaticDirection = direction;
            // Walking/idle uses the low overlay; running reuses the saved
            // Meshy Dodge clip and its original jump trajectory.
            acrobaticVisual?.ClearMotion();
            visual?.PlayAction(IsRunningDodge && !UsesRunningSpinFallback ? CharacterAction.Dodge : CharacterAction.Idle, acrobaticDuration);
            Dodged?.Invoke();
        }

        public static bool IsBackwardInput(Vector3 facing, Vector3 direction)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            facing = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            float forward = Vector3.Dot(facing, direction);
            float side = Mathf.Abs(Vector3.Dot(Vector3.Cross(Vector3.up, facing), direction));
            return direction.sqrMagnitude >= .04f && forward < -side - .00001f;
        }

        public static bool IsSidewaysInput(Vector3 facing, Vector3 direction)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            facing = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            float forward = Vector3.Dot(facing, direction);
            float side = Mathf.Abs(Vector3.Dot(Vector3.Cross(Vector3.up, facing), direction));
            // The 45-degree diagonal belongs to the side sector on both sides.
            return direction.sqrMagnitude >= .04f && side > .001f && Mathf.Abs(forward) <= side + .00001f;
        }

        private void StartGuardJump(Vector3 direction)
        {
            GetComponent<PlayerDefense>()?.CancelGuard();
            IsGuarding = false;
            IsCartwheeling = IsSidewaysInput(Facing, direction);
            IsBackflipping = !IsCartwheeling;
            IsRunning = false;
            sustainedDirectionTime = 0f;
            IsGrounded = false;
            airborneTime = 0f;
            fallVisualPlayed = true;
            verticalSpeed = Mathf.Sqrt(2f * tuning.Gravity * tuning.GuardBackflipHeight);
            acrobaticElapsed = 0f;
            acrobaticDuration = 2f * verticalSpeed / tuning.Gravity;
            Vector3 sideways = Vector3.Cross(Vector3.up, Facing.normalized);
            acrobaticDirection = IsCartwheeling
                ? sideways * Mathf.Sign(Vector3.Dot(direction, sideways)) : -Facing.normalized;
            planarVelocity = acrobaticDirection * tuning.GuardBackflipSpeed;
            visual?.PlayAction(CharacterAction.Idle, acrobaticDuration);
            Jumped?.Invoke();
        }

        private void CancelAcrobatics()
        {
            if (IsDodging || IsGuardJumping) planarVelocity = Vector3.zero;
            IsDodging = IsBackflipping = IsCartwheeling = false;
            IsRunningDodge = IsRunning = false;
            acrobaticVisual?.ClearMotion();
        }

        private void OnDisable() => CancelAcrobatics();

        private Vector3 GetCameraRelativeDirection(Vector2 move)
        {
            if (UseCommands && Commands.WorldSpace) return Vector3.ClampMagnitude(new Vector3(move.x, 0f, move.y), 1f);
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
            if (!IsGrounded) { IsRunning = false; return; }
            if (IsGuarding || landingLockRemaining > 0f || MovementScale < .9f ||
                magnitude < 0.55f || desiredDirection.sqrMagnitude < 0.01f)
            {
                sustainedDirectionTime = 0f;
                previousInputDirection = Vector3.zero;
                IsRunning = false;
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
            IsRunning = sustainedDirectionTime >= tuning.RunHoldSeconds;
        }
    }
}
