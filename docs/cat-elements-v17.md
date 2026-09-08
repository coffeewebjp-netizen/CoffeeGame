# Cat elemental actions V17 — ORC-20260909-001

Status: generated and verified after the owner restored Unity authentication on 2026-09-09 JST. The normal Windows launch directory has been updated reversibly. The ARM64 development APK is built; no Android device is currently visible to ADB, so installation and physical-device play verification remain outstanding.

## Requested behavior

The existing silver cat model keeps its identity and rig. A separate `SilverCatV17/SilverCatElementsV17` animation library adds a forward-leaning run, airborne wind cast, folded earth drop and deep crouched earth landing. V14, the original Meshy asset, sword heroine and dragon girl remain preserved. Meshy regeneration is optional; this iteration authors the current rig directly.

| Input | Cat behavior |
| --- | --- |
| Move at running speed | 30-degree chest lean, counter-swinging arms and faster alternating steps |
| Attack on ground | Orange/gold fire bolts; alternating hands, third volley fans out; existing free basic-attack cost |
| Magic | 1.2-second charge, lightning around the caster within 4 m; twice normal magic damage, 25% maximum MP, 6-second cooldown |
| Attack in air | A mint-colored piercing wind crescent, one cast per jump; uses air-attack damage |
| Jump, then press down | Folded fall, deep forward crouch, 2.6 m rock/ripple shockwave, about 1 second of landing recovery |
| Special | Existing 10-second selective time stop; only background turns over, animated over 0.65 seconds in/out; actors and HUD remain upright |

The companion AI waits until an enemy is within 3.6 m before spending MP on surrounding thunder. Mobile buttons read 炎弾 / 落雷; the attack label becomes 風刃 in the air. Existing input layout, party recovery and saved progression formats are unchanged. New fire/thunder/wind sounds use deterministic synthesized clips; existing voice assignments are unchanged.

## Rendering and timing

`TimeStopWorldEffect` renders the environment and actors into separate targets. Renderer visibility is changed only inside camera render callbacks and restored immediately afterward. Actor classification includes `Health` descendants and combat effects whose `CombatOwnership` resolves to an actor; collision layers are untouched. The compositor turns the background vertically using an eased cosine scale and leaves actor UVs upright. Both layers desaturate gradually. Cancellation and disable restore immediately; normal completion animates the return. The second camera and targets are used only during the effect and its transition.

All new projectiles, particles/lines and delayed thunder hits use `CombatClock` and caster ownership. Existing pause, frozen sources and deferred time-stop damage remain authoritative. Normal attacks are swept capsule queries; wind pierces with once-per-target damage. Earth damage occurs once on actual ground contact. Alpha separation and intermediate/full/reverse flip stages were checked on Windows D3D11. Android compositor performance and appearance still need a physical-device check.

## Verification and delivery

1. Authentication restored; `CoffeeGame.Editor.SilverCatMotionSetup.ConfigureElements` generated a separate 18-clip controller and `art/3d/trials/meshy-rival/motion-v17/audit.json`. Original V14 assets remain intact.
2. Run Domain, Input and Runtime EditMode suites, including the added V17 pose/separation tests and animated camera-target restoration checks. The original V14 tests remain.
3. Build `CoffeeGame.Editor.BuildCoffeeGame.BuildCatElementsV17NoSetup`. Its asset validation must pass before packaging; no legacy scene/model setup is called.
4. Run the development player with `-captureCatMotion <task-output>` and the accepted heroine flag `-azureMaidenUpgraded3D`. The extended memory-profile harness exercises fire, wind, earth, surrounding thunder, original evasions, actor switching and actual composite captures at intermediate/full/reverse flip stages. Inspect all images, especially forward head/chest direction, grounded crouch and upright actors. Run the mobile-control harness for label/touch regression. Check real profile hashes before/after.
5. Build Android with `CoffeeGame.Editor.BuildCoffeeGame.BuildAndroidCatElementsV17NoSetup`. Verify ARM64 package and prior signing identity. Snapshot preferences/profile hashes and use `adb install -r` on the currently discovered authorized device; retain the V16 APK.
6. Adopt the Windows build in the normal Steam launch directory only after rendered checks, first backing up its full file manifest and preparing a path-checked restoration script. No APK/data clear, source public push, production signing or store release is authorized here.

The factory falls back to V14 if V17 assets have not yet been generated, to keep an incomplete asset import from leaving the model without a controller. The V17 build entry points explicitly validate the new library and cannot silently package that fallback.

Unity EditMode suites passed **347/347**, including five V17 motion checks and two camera separation/lifecycle tests. The memory-only Windows cat harness passed **44/44**: running, three fire volleys, air wind, ground-contact earth damage, front/rear thunder without friendly fire, selective time stop, animated restoration, actual component disable, pause, party switching and defeat/recovery. Eight reviewed frames are retained in `art/3d/trials/meshy-rival/motion-v17/previews`.

The mobile harness initially passed **22/22** on Windows, but repeated hidden-player runs exposed a test-device focus issue: Unity disables a synthetic touchscreen added while the application is unfocused. The harness now registers only its synthetic device as background-capable; the real input/focus policy is unchanged. Camera swipes still must rotate the actual camera by more than ten degrees, and pause/focus/reset assertions remain required. Failed runs are retained as evidence rather than counted as passes. Final normal-directory runs without a forced model flag passed **22/22 touch checks** (`runtime-07`) and **44/44 cat checks** (`runtime-08`).

Real local/cloud profile files remained byte-identical during captures; temporary input/display preferences were restored. An initial graphics-free Unity test run crashed in offscreen rendering; graphics-enabled tests and actual player captures are the accepted evidence. A camera lifecycle test was corrected to use an explicit runtime interruption in EditMode, with actual `OnDisable` covered by the player harness.

### Start and restore

Start `unity/CoffeeGame/Builds/Windows/CoffeeGAME.exe`, the existing normal launch path used by the Steam shortcut. Close any already-running older process before relaunching. Direct launch from this path confirmed the saved accepted red-haori heroine selection; the Steam UI itself was not driven by automation.

The original normal player is preserved in `.task-local-backup/ORC-20260909-001-normal-player/previous` with **348 files** and a SHA256 manifest. The replacement payload has **297 files**. Run `tools/restore-pre-cat-elements-v17-player.ps1` without arguments to verify backup/current hashes, or `tools/restore-pre-cat-elements-v17-player.cmd` to restore after closing the game. Restoration retains the upgraded payload and does not change current character selection or save progression.

Android artifact: `unity/CoffeeGame/Builds/Android-CatElementsV17/CoffeeGAME-CatElementsV17-development.apk`, ARM64 IL2CPP, package `jp.coffeetools.coffeegame`, minimum Android SDK 26, same development certificate as the prior APK. Keep the V16 APK for rollback using `adb install -r`; never uninstall or clear app data for this update. Physical Android installation, rendering, thermal/frame-rate and touch-comfort checks are not claimed while no device is connected.

Detailed logs, hashes, profile guards and failed/successful runs are local under `C:/work/task-backups/ORC-20260909-001`. The owner requested silence during work; confirmation players are closed and subsequent automated launches must use `-noaudio`. Public source push remains held. No new Meshy model, billing change, persistence migration or production/store release was used.
