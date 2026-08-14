# CoffeeLearning CoffeeGAME v1 skeleton

This is a non-production consumer skeleton for CoffeeLearning's frozen
`contractVersion: "1.0"` boundary. The provider fixture copied into the focused
EditMode tests is sourced from:

- `../../CoffeeLearning/docs/coffee-game-v1.fixture.json`
- `../../CoffeeLearning/docs/COFFEEGAME_INTEGRATION_V1.md`

## Extension seams

- `Integration/CoffeeGameV1Dtos.cs` contains public-field serializable DTOs. Field
  names and string values intentionally follow provider JSON exactly, including
  `typed`, `speechTranscript`, semantic difficulty, `resultId`, and `grantId`.
- JSON deserialization may ignore additive unknown fields, matching Unity
  `JsonUtility`; every consumed response must still pass the explicit contract
  version gate. Unsupported versions are rejected rather than guessed.
- `ILearningBridge` keeps the existing sign-in/daily-claim members and adds weak
  sync, challenge issue, answer submit, and result recovery with cancellation.
  `NullLearningBridge` leaves the game playable. `MockLearningBridge` is a
  deterministic in-process v1 provider with stable retry IDs and pending-to-
  completed recovery.
- `Domain/RivalEncounterSession.cs` owns the pure encounter flow. A transcript
  becomes editable final text; it never submits from a speech callback. Submission
  is created only after explicit confirmation.
- `Domain/SafeCheckpointCadencePolicy.cs` requires an explicit safe checkpoint and
  rejects combat or blocked states. Runtime code must not invoke encounters mid-
  combat.
- `Domain/LearningSyncSchedulePolicy.cs` is an immutable startup/periodic due-time
  policy. Callers provide time and the provider's bounded `syncAfterSeconds`; it
  starts no background work and performs no network access.
- `Domain/DeterministicRivalSelector.cs` uses an injected bounded integer source
  and suppresses the last-seen rival when another valid candidate exists.
- `CoffeeGameDomainMapper` is the single transport-to-domain projection. The game
  reward aggregate consumes only the provider's authoritative completed result,
  learning mutation, eligibility, semantic difficulty, and stable grant ID. It
  rejects completed responses unless learning state is exactly `ok` for correct
  judgment or `mistake` for incorrect judgment.
- `ProvisionalLearningRewardPolicyV1` is versioned
  `coffee-game-rival-reward-v0.1-provisional`. Its talent point, XP, Gold, and
  affinity values are skeleton values, not final balance. The independent grant
  ledger makes replay a no-op; affinity threshold crossing recruits once.

## Deliberate exclusions

There is no real HTTP, authentication/token handling, cloud save/profile migration,
microphone or permission request, speech SDK, raw audio storage, AI grading,
player-facing UI, scene/prefab/controller hookup, combat-run coupling, package
installation, final economy tuning, deployment, or production enablement in this
skeleton.
