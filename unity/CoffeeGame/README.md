# CoffeeGAME Unity migration

This is the production successor to the browser prototype in `../../haxslasher`.
The project keeps movement, collision, jump height, and combat in a 3D world,
while the active presentation is directional HD-2D artwork. Rigged Blender/FBX
models remain available as a future-3D fallback. Combat, progression, save data,
and CoffeeLearning integration do not depend on either visual implementation.

## Required editor

- Unity `6000.5.7f1`
- Universal 3D / URP
- Input System 1.20
- Android Build Support, Android SDK & NDK Tools, and OpenJDK for Android builds

Open this folder from Unity Hub. On the first successful script import,
`Assets/CoffeeGame/Scenes/CombatSandbox.unity` is created automatically. The same
operation is available from `CoffeeGAME > Setup first combat slice`.

If setup reports that it changed input handling, restart the Unity Editor once
before pressing Play. The project currently uses **Both**: gameplay, controller
navigation, and the uGUI combat/pause interface use the new Input System. The
launch-time input-mode chooser retains a small IMGUI compatibility surface.

## First milestone

- A 38.4 x 21.6 metre grassland combat boundary in XZ space, with Y used for jump height
- A continuous scrolling stage organized as a 4 x 4 visual chunk grid; the first slice keeps all chunks loaded and reserves nearby-only streaming for a later performance pass
- A generated painterly grass floor and distant rolling-hill panorama, kept visual-only outside the existing collision boundary
- Directional HD-2D heroine and animated HD-2D slime rendered inside the 3D scene
- Walk, run, jump, sword, air slash, plunge, spin slash, and ice magic
- Exactly-once EXP 1, Gold 1, and Slime Jelly 1 per defeated slime
- Level 2 after three defeated slimes; result after five
- Level, EXP, Gold, and materials survive a retry; HP, MP, ST, position, enemies,
  charge state, and projectiles reset with the run
- A transparent portrait HUD and a true pause menu with Status, Items, System, and Party tabs
- A two-axis battle camera with 360-degree yaw and safely clamped vertical pitch
- Versioned local profile persistence with extensible keyed attributes and talent-selected growth
- Runtime input diagnostics for Steam Controller / standard gamepads

Default controls:

| Action | Keyboard | Standard gamepad | Steam Desktop fallback |
| --- | --- | --- | --- |
| Move | WASD / arrows | Left stick / D-pad | Stick -> arrows |
| Rotate camera horizontally | Z / C or hold right mouse and drag horizontally | Right stick X | Z / C |
| Rotate camera vertically | V / R or hold right mouse and drag vertically | Right stick Y | V / R |
| Jump | Space | South face button | A -> Enter |
| Sword | F | Right trigger | RT -> Mouse Left |
| Iai slash | Q | West face button | X -> PageUp |
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
- **タッチ（画面操作）**: Android / touch overlay. Left virtual stick moves,
  the right pad fires Jump / Sword / Iai / Ice, and a right-side drag orbits
  the camera. This is the default chooser cursor on mobile.

The selection press is release-gated, so the Enter/South press used to choose a
mode cannot also start the run or trigger a battle action in the same frame. Open
`システム` and choose `入力方式を選び直す` to change modes without restarting.
Keyboard/mouse combat bindings are currently fixed; interactive rebinding is
available only for the Controller/Gamepad and Steam Desktop compatibility modes.

The pause tabs are ordered `ステータス` → `持ち物` → `システム` → `仲間`.
`システム` contains input-mode selection, battle-button rebinding, reset, a
manual save command, performance presets, and an FPS-display toggle. Manual save
writes the player profile and the current button overrides together and reports the
result in the same panel. Performance defaults to `Keep Current`; Balanced, Smooth,
and Quality are explicit opt-in presets, and the upper-right FPS/frame-time readout
can be hidden independently.
The Status and System surfaces share one persistent scroll area so switching tabs
does not destroy their viewport. Status enumerates the keyed attribute snapshot and
derived combat values; System visibly separates controller settings and save actions.
The permanent scrollbar is draggable and accepts the mouse wheel/touch drag. On
keyboard or controller, Up/Down scrolls Status, Items, and Party; System keeps
Up/Down for selectable commands and automatically reveals the selected row.

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

Runtime loads the manifests below and creates sprites from fixed-size transparent
PNGs or multi-row atlases. Hero v5 resolves eight camera-relative sectors from five authored
views (`down`, `downSide`, `side`, `upSide`, `up`) and mirrors the three left-facing
sectors. A small angular hysteresis prevents diagonal/cardinal flicker. It preserves
the final frame of one-shot animations and sorts actors by projected camera depth.

The active heroine strips use one 768x768 / 540 PPU / pivot Y 0.0625 scale contract.
Walk and Run v5 have six frames per authored direction; Jump v5 has four, including
dedicated 45-degree front-right and rear-right art. Mirroring completes all eight
runtime directions. Sword v4 has four frames and MagicCharge/MagicRelease v4 have
three each. The 80 v5 cells are packed into 15 textures (3x2 for Walk/Run and 2x2
for Jump) instead of loading 80 additional per-frame textures. No action-specific
or direction-specific PPU compensation is permitted. Fall, Land, AirSlash, Plunge,
spin, Hurt, and Defeated retain their existing art and use the same direction policy.

Walk plays at 7.5 fps (a 0.8-second six-frame cycle) so opposing foot contacts do
not flash past. Down and DownRight Run use corrected source sheets whose head and
torso scale is locked to the matching Walk view. Export applies authored anatomical
scale multipliers to crouched Run directions instead of stretching every pose to an
upright 680px height; the runtime still uses the shared PPU/pivot contract rather
than direction-specific runtime scale compensation.

- `Assets/CoffeeGame/Resources/Art/HD2D/hero-hd2d.json`
- `Assets/CoffeeGame/Resources/Art/HD2D/slime-hd2d.json`
- `Assets/CoffeeGame/Resources/Art/HD2D/Hero/Frames`
- `Assets/CoffeeGame/Resources/Art/HD2D/Hero/Atlases`
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
- `Runtime/Presentation/StageLayout.cs` owns the stage, actor, camera, and 4 x 4 chunk bounds used by the first scrolling-stage slice
- `Runtime/Persistence`: versioned local profile JSON and atomic replacement
- `Runtime/Run`: one-room lifecycle and reward application
- `Runtime/UI`: safe-area uGUI HUD, pause/status presentation, and menu input coordination
- `Integration`: CoffeeLearning bridge contract (offline mock for now)

Do not add game rules to `SpriteCharacterVisual` or `ModelCharacterVisual`.
Either implementation must remain replaceable as the actor's visual child.
