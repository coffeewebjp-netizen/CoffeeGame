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
- `WeakSyncRequestDto.lookbackDays` is a CoffeeGAME-owned request setting for
  `GET /weak-items`. Contract v1 defaults it to 14 days and accepts only 1 through
  30 days; the public default/minimum/maximum constants and validation helper keep
  future game configuration on the same boundary. The provider does not choose or
  silently clamp this value.
- JSON deserialization may ignore additive unknown fields, matching Unity
  `JsonUtility`; every consumed response must still pass the explicit contract
  version gate. Unsupported versions are rejected rather than guessed.
  `JsonUtility` may also materialize an omitted reference field as an empty
  object. The HTTP bridge therefore treats `error` as present only when its
  provider-owned `code` is nonblank; successful weak-sync responses are not
  rejected merely because an empty error DTO was constructed locally.
- `ILearningBridge` keeps the existing sign-in/daily-claim members and adds
  self-account identity, weak sync, challenge issue, answer submit, and result
  recovery with cancellation.
  `NullLearningBridge` leaves the game playable. `MockLearningBridge` is a
  deterministic in-process v1 provider with stable retry IDs and pending-to-
  completed recovery.
- `CoffeeLearningHttpBridge` is the real contract-v1 client. Its production
  integration base is
  `https://www.coffeewebjp.com/api/integrations/coffee-game/v1`, with a
  constructor override for tests or an explicitly configured environment. It
  uses `UnityWebRequest`, sends the `cgt_` credential only as an Authorization
  Bearer header, preserves caller-generated request/attempt IDs, handles 200,
  201, and 202 responses, maps provider and HTTP failures to safe contract
  errors, and aborts on caller cancellation. The default timeout is 30 seconds.
  Raw response bodies and credentials are never logged or included in transport
  errors. The inherited `ClaimTodayAsync` is a local zero-result compatibility
  method because provider v1 has no daily-claim endpoint.
- `ICoffeeGameAccessTokenProvider` and `ICoffeeGameAccessTokenStore` isolate
  credential ownership from HTTP and gameplay. On Windows Editor and Windows
  Standalone (including a Windows Steam build),
  `WindowsDpapiAccessTokenStore` protects the canonical raw `cgt_` token with
  DPAPI CurrentUser plus application entropy and atomically replaces an encrypted
  file below `Application.persistentDataPath/CoffeeLearning`. There is no
  PlayerPrefs, profile JSON, environment-variable, hardcoded-token, or plaintext
  fallback. Deletion removes the local encrypted file but is not a remote token
  revocation; users can revoke issued tokens in CoffeeLearning's browser UI.
- `CoffeeLearningDesktopConnectService` implements the Windows browser handoff.
  It opens the production connect endpoint
  `https://www.coffeewebjp.com/api/coffee-game/connect` with an override seam,
  binds only a random `127.0.0.1` port, uses the exact
  `/coffee-game-callback` redirect path, creates 256 bits of URL-safe state,
  relays fragment fields `state` and `bearer` through a same-origin local POST,
  verifies state before saving, and stops on success, cancellation, timeout, or
  its bounded request limit. The bearer is removed from browser history after
  relay and is never put in the provider connect URL or application logs. No
  the settings presenter is the only runtime UI seam that invokes this service.
- `CoffeeLearningConnectionPresenter` is the settings-only connection state
  machine and bridge composition seam. The System tab appends three commands
  after its existing ten rows: connect/reconnect, disconnect, and cancel. A
  connect or reconnect needs two deliberate activations before the browser can
  open. For display only, it decodes the user subject already embedded in the
  dedicated token and shows `接続済み（user@example.com）`; it does not make an
  additional account or Word request. Provider calls still authenticate the
  bearer server-side. Invalid/unavailable display data is shown only as
  `アカウント確認不可`. It never displays a bearer or exception message. Leaving the System tab,
  closing the menu, or quitting cancels a pending confirmation or active
  handoff, but never deletes a completed credential. Disconnect also needs
  confirmation and deletes only the local encrypted credential. A failed
  reconnect keeps the prior usable bridge and credential.
- `CombatSliceBootstrap` owns the production presenter for the game lifetime
  and exposes its current `ILearningBridge`. Construction reads only secure
  credential availability and never launches a browser. The real HTTP bridge
  becomes available only after a stored token exists; callers otherwise receive
  the fail-closed `NullLearningBridge`.
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
- Account identity shown in settings comes from the user subject already embedded
  in the dedicated token. This avoids an extra account-display request; it never
  authorizes a provider call. Every weak-item/challenge/answer request still sends
  the bearer and is authenticated server-side. The self-only `/account` contract
  remains available for diagnostics but is not required for the display.
- The first runtime rival presentation is connected at a post-battle checkpoint.
  Every configured five slime defeats, combat stops before the next spawn and the
  approved rival portrait modal is shown. `RivalLearningQuestionSession` requests
  only the configured 14-day weak-attempt window, issues one server challenge,
  and exposes an explicit typed flow: loading -> editing -> confirming ->
  submitting -> completed or pending recovery. Text entry never submits directly.
  The answer editor uses multiline-newline behavior, and gameplay confirm is
  suppressed while that editor owns focus, so Japanese IME conversion-confirm
  Enter cannot advance to confirmation or lock the field. The player must select
  the explicit confirmation control after finishing the draft.
  Loading, no-item, provider-error, and pending states all retain a bounded retry
  or return-to-battle path, so the checkpoint cannot trap the run. The first usable
  provider item is selected from the bounded response; there is no Word scan or
  fallback query in the game.
- `CoffeeGameDomainMapper` is the single transport-to-domain projection. The game
  reward aggregate consumes only the provider's authoritative completed result,
  learning mutation, eligibility, semantic difficulty, and stable grant ID. It
  rejects completed responses unless learning state is exactly `ok` for correct
  judgment or `mistake` for incorrect judgment.
- Completed v1 results may add bounded `judgment.feedback`. The session accepts
  older responses without it, removes control characters, caps it at 600
  characters, and neutralizes rich-text angle brackets before the result view
  displays it as `AI判定`. It never consumes provider `comment` or canonical answer.
- `LearningRewardPolicyV1` is the Owner-approved
  `coffee-game-rival-reward-v1` balance. A correct authoritative result is mapped
  once into existing Gold and EXP plus spendable talent points and per-rival
  affinity. The stable provider `grantId` is namespaced in the existing reward
  ledger, so replay after profile save is a no-op. Affinity 100 recruits the rival
  once. Profile v1 loads with empty learning fields and the next save writes v2.
  The exact table and ownership are recorded in
  [`rival-learning-reward-design.md`](rival-learning-reward-design.md).

## Deliberate exclusions

There is no Android secure-token adapter yet: the Android Unity module is not
installed, and non-Windows platforms fail closed through
`UnsupportedCoffeeGameAccessTokenStore`. A production Android adapter must use an
Android Keystore-backed store; PlayerPrefs is not acceptable. There is also no
cloud save/profile migration, microphone or permission request, speech SDK, raw
audio storage, local AI grading, controller-native text keyboard, final economy tuning,
deployment, or provider production enablement in this skeleton. Runtime
configuration must explicitly keep integration disabled until CoffeeLearning's
server-side integration switch and token issuance UI are enabled.
