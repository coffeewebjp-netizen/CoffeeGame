# CoffeeGAME Unity migration

This is the production successor to the browser prototype in `../../haxslasher`.
The project keeps movement, collision, jump height, and combat in a 3D world,
while the active presentation is directional HD-2D artwork. Rigged Blender/FBX
models remain available as a future-3D fallback. Combat, progression, save data,
and CoffeeLearning integration do not depend on either visual implementation.

## Required editor

- Unity `6000.3.21f1` (Unity 6.3 LTS)
- Universal 3D / URP
- Input System 1.20
- Android Build Support, Android SDK & NDK Tools, and OpenJDK for Android builds

Open this folder from Unity Hub. On the first successful script import,
`Assets/CoffeeGame/Scenes/CombatSandbox.unity` is created automatically. The same
operation is available from `CoffeeGAME > Setup first combat slice`.

If setup reports that it changed input handling, restart the Unity Editor once
before pressing Play. The project currently uses **Both**: gameplay and controller
navigation use the new Input System, while the temporary IMGUI diagnostics panel
keeps legacy mouse-click fallback. This can return to Input System-only when the
panel is replaced by uGUI/UI Toolkit.

## First milestone

- A 9.6 x 5.4 metre grassland combat boundary in XZ space, with Y used for jump height
- A generated painterly grass floor and distant rolling-hill panorama, kept visual-only outside the existing collision boundary
- Directional HD-2D heroine and animated HD-2D slime rendered inside the 3D scene
- Walk, run, jump, sword, air slash, plunge, spin slash, and ice magic
- Exactly-once EXP 1, Gold 1, and Slime Jelly 1 per defeated slime
- Level 2 after three defeated slimes; result after five
- Level, EXP, Gold, and materials survive a retry; HP, MP, ST, position, enemies,
  charge state, and projectiles reset with the run
- Runtime input diagnostics for Steam Controller / standard gamepads

Default controls:

| Action | Keyboard | Standard gamepad | Steam Desktop fallback |
| --- | --- | --- | --- |
| Move | WASD / arrows | Left stick / D-pad | Stick -> arrows |
| Jump | Space | South face button | A -> Enter |
| Sword | F | Right trigger | RT -> Mouse Left |
| Spin slash | Q | West face button | X -> PageUp |
| Ice magic | E | North face button | Y -> PageDown |
| Pause / confirm | Escape / Enter | Start / South face button | Steam/Back -> Escape / A -> Enter |
| Open button settings | Tab | View / Select | Menu -> Tab |
| Settings cancel | Escape | East face position | B -> Space |

CoffeeGAME opens an input-mode chooser on every launch. The saved mode only
positions the initial cursor; it is never activated automatically. Choose one of:

- **Keyboard / Mouse**: accepts the keyboard/mouse group only.
- **Controller / Gamepad**: battle actions accept native Gamepad paths only. This
  cannot be selected until Unity detects at least one Gamepad. Menu recovery
  remains available through Tab, arrows/WASD, Enter, Escape, and the on-screen
  button-settings UI so an unavailable View/Select mapping cannot lock the user out.
- **Steam Desktop compatibility**: an explicit fallback for a Steam Desktop
  Layout which converts controller input into keyboard/mouse events. It is never
  selected merely because no Gamepad was found.

The selection press is release-gated, so the Enter/South press used to choose a
mode cannot also start the run or trigger a battle action in the same frame. Open
`ボタン設定` and choose `入力方式を選び直す` to change modes without restarting.
Keyboard/mouse combat bindings are currently fixed; interactive rebinding is
available only for the Controller/Gamepad and Steam Desktop compatibility modes.

The settings screen can be completed without a mouse: View/Select opens it,
Up/Down chooses a row, South confirms, and East normally cancels or closes it. Tab,
arrows/WASD, Enter, and Escape provide the equivalent recovery path even while
Controller/Gamepad is selected. When a row is chosen, the screen waits until the
confirm button is released before accepting the next press, so South is not
accidentally rebound to itself. Capture times out after ten seconds. During an
active battle-action rebind, East/B is a valid target; Start/View and movement
controls remain reserved for menu or movement.

Every Battle/UI map transition also waits for the button that caused the transition
to be released. A held Start cannot immediately pause after starting or immediately
resume after pausing, a held South cannot become an accidental jump, and a held
View/Select cannot open and close the settings screen in the same press.

The UI reads display names and the actual control path from Unity Input System.
It does not assume that a physical button is labelled A, B, X, or Y. Reusing an
occupied attack button swaps the two actions predictably. Overrides are stored as
versioned `semantic -> gamepad path` data, rather than runtime Input System binding
GUIDs, so they survive a new process and are visibly marked `保存済み` after write.
`初期配置へ戻す` removes the saved overrides.

The **Steam Desktop compatibility profile** matches the original Steam Controller
desktop mapping (A=Enter, B=Space, X=PageUp, Y=PageDown, RT=Mouse Left,
sticks=arrow keys and Menu=Tab). Desktop overrides are stored independently as versioned
`semantic -> keyboard/mouse path` data. The binding screen captures non-reserved
keyboard keys and mouse buttons; B/Space is valid during an active battle-action
rebind even though it remains the normal UI cancel key outside capture. It swaps
duplicates, waits for release, and keeps the same ten-second timeout. Existing
Keyboard combat keys remain reserved so a desktop override cannot make two combat
actions fire at once.

The diagnostic panel separately shows all detected Gamepad slots, controller-like
HID devices which cannot be rebound, and the most recent raw button path. A Steam
Controller may appear as a virtual XInput Gamepad. If pressing a control opens the
Steam keyboard and the raw Gamepad path does not update, Steam Desktop Layout has
intercepted that button. CoffeeGAME can accept ordinary Keyboard/Mouse events
from Desktop Layout, but Steam special actions such as `SHOW_KEYBOARD` are
consumed before the application receives an event and therefore cannot be
cancelled in game.

For the Steam Controller, register the local build as a **non-Steam game** before
testing native controller input:

1. In Steam, choose `Games > Add a Non-Steam Game to My Library`.
2. Add `Builds/Windows/CoffeeGAME.exe`.
3. Open CoffeeGAME's Controller Layout, enable Steam Input, and apply the
   **Gamepad** template.
4. Start CoffeeGAME from its Steam Library entry and select
   **Controller / Gamepad** in the in-game chooser.

This local registration is separate from publishing on Steam. A Steamworks AppID,
Steamworks SDK integration, and an official layout are needed later for Steam
distribution, but not for the current local build. If Unity still reports no
Gamepad, select **Steam Desktop compatibility** deliberately and use the desktop
mapping shown above.

## HD-2D presentation

Runtime loads the manifests below and creates individual sprites from fixed-size
transparent PNGs. The visual resolves front/side/back relative to the camera,
mirrors left-facing actions, preserves the final frame of one-shot animations,
and sorts actors by projected camera depth.

- `Assets/CoffeeGame/Resources/Art/HD2D/hero-hd2d.json`
- `Assets/CoffeeGame/Resources/Art/HD2D/slime-hd2d.json`
- `Assets/CoffeeGame/Resources/Art/HD2D/Hero/Frames`
- `Assets/CoffeeGame/Resources/Art/HD2D/Slime/Frames`

The runtime fallback order is HD-2D, rigged 3D model, static sprite, then a
primitive. `PlayerMotor3D` explicitly drives Jump, Fall, Plunge, and Land so the
airborne image cannot return to idle before the physical state changes. Damage
timing remains in combat code and never comes from an animation event.

Source artwork, contact sheets, prompt provenance, and the deterministic frame
export command are documented in `../../art/hd2d/README.md`.

## FBX model fallback

Place exported models at these exact resource paths:

- `Assets/CoffeeGame/Resources/Models/Hero/heroine-v4.fbx`
- `Assets/CoffeeGame/Resources/Models/Slime/slime-v2.fbx`

Then run `CoffeeGAME > Setup first combat slice`. Setup imports both rigs as
Generic, keeps embedded materials and animation clips, locks clip root motion,
and generates `HeroRuntime.controller` / `SlimeRuntime.controller` under
`Resources/Animations`. Runtime `Animator.applyRootMotion` is also disabled;
movement remains owned by `PlayerMotor3D` and `SlimeController`.

The generated controllers match the named clips documented in
`../../docs/3d-character-pipeline.md` to `CharacterAction` states. Runtime uses
these models only when the HD-2D manifest or required frame resources are not
valid. Re-run setup after replacing an FBX so clip and controller mappings are
refreshed without changing gameplay code.

## Architecture boundary

- `Domain`: progression, rewards, and tuning data
- `Input`: logical actions and rebinding persistence
- `Runtime/Actors`: 3D movement, health, and enemy behaviour
- `Runtime/Combat`: combat state and projectiles
- `Runtime/Presentation`: replaceable sprite/model visuals
- `Runtime/Run`: one-room lifecycle and reward application
- `Integration`: CoffeeLearning bridge contract (offline mock for now)

Do not add game rules to `SpriteCharacterVisual` or `ModelCharacterVisual`.
Either implementation must remain replaceable as the actor's visual child.
