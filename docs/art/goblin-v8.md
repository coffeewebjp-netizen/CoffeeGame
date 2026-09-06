# Forest goblin V8

ORC-20260905-001 / IN18 / WP22. This adds a green, long-eared forest goblin alongside the existing slime. The accepted red-haori heroine V6, action effects V5 and forest V7 are retained.

## Creature and motion source

The private asset was created in the Owner's contracted Meshy workspace. Meshy 6 text-to-3D shape generation showed 20 credits; Meshy 7 flagship 2K PBR texturing showed 10. Remeshing, humanoid rigging and the selected motion additions showed no additional charge. No plan purchase or public asset sharing was performed. Balance changes from other activity are not treated as this task's consumption.

Rig task: `01a0749d-0706-763e-b6f9-a494f6f9c256`. Generated silhouette: compact moss-green goblin, long pointed ears, amber eyes, teal crest, leather straps and ochre ragged cloth. The requested rig height was 1.2 m. The roughly 542K-triangle source was remeshed to 31,323 triangles before skinning. All four motion donors have exactly matching bind matrices.

Meshy motion sources: Slow Orc Walk, Charged Axe Chop, Axe Breathe and Look Around, Dead. The running GLB supplies the base body/rig. Blender removes the splayed right fingers, adds a closed leather grip and a separate skinned wooden club, and bakes wood grain into a portable albedo. The imported game asset has 30,498 triangles, three skinned renderers and 24 source joints. Six clips: Idle, Walk, AttackWindup, Attack, Hurt and Defeated. Hurt is a locally authored recoil; the other movements derive from the Meshy rig. Root horizontal translation is removed because the game owns movement. The chop is divided and retimed so its impact coincides with the AI's 0.16-second strike point.

Blender source and previews: `art/3d/enemies/forest-goblin-v8/`. The five downloaded GLBs are retained locally in its ignored `downloads/` directory; input SHA256 values and action ranges are recorded in `preparation.json`. Signed download URLs and account/session data are not stored. The packed `.blend`, FBX, atlas, club texture, controller and reproduction script are retained separately from previous character work.

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' -b --python tools/blender/prepare_forest_goblin.py -- --source-dir art/3d/enemies/forest-goblin-v8/downloads --output-dir art/3d/enemies/forest-goblin-v8 --unity-dir unity/CoffeeGame/Assets/CoffeeGame/Resources/Models/Goblin
```

After reproducing the asset, run `CoffeeGame.Editor.GoblinAssetSetup.Configure` in Unity batch mode. It owns only Goblin resources; it does not regenerate shared controllers. `CoffeeGame.Editor.BuildCoffeeGame.BuildGoblinV8NoSetup` builds the current scene to `Builds/Windows-GoblinV8`.

## Combat behavior

- Encounters alternate goblin/slime, starting with a goblin. A retry restarts that sequence with a fresh reward claim.
- Goblin HP: 15. It approaches at 0.95 m/s, briefly circles at close distance, then locks its aim before raising its club.
- Windup: 0.72 seconds with an orange ground outline. Strike: 0.38 seconds, one damage check at 0.16 seconds. Range: 1.45 m; cone uses a 0.64 facing dot threshold. Side steps, distance, moving behind it and sufficient jump height avoid the committed swing. Hurt cancels the attack. Recovery is 0.75 seconds.
- Goblins award EXP 1 and Gold 1. Slimes keep their EXP 1, Gold 1 and Slime Jelly 1. Both count toward the existing five-kill rival encounter. The save schema is unchanged.

The combat run now uses a small species-neutral `CombatEnemy` wrapper for death and reward lifecycle. Slime AI itself remains unchanged. Damage checks are controlled by gameplay time, not animation events. Pausing stops AI, motion and respawn time. The goblin corpse remains briefly visible before the next encounter.

## Verification and rollback

See `goblin-v8-verification.json` for final delivery evidence. The development-only `-captureGoblin <directory>` command uses in-memory progression and suppresses profile/cloud writes. It exercises the actual factory, AI, animation and run controller, captures game views, and checks the first five kills, pause/respawn, rival continuation, retry and reward counts. It is a deterministic runtime check, not a full manual input or sustained mobile performance test.

The normal Steam route remains `Builds/Windows/CoffeeGAME.exe`. Before adoption, all old normal-player files are copied and hash-verified under `.task-local-backup/ORC-20260905-001-WP22-normal-player/`. `tools/restore-pre-goblin-v8-player.cmd` returns to forest V7 while preserving current character selection and progress. Its PowerShell companion without `-Restore` performs a non-mutating hash check. Previous rollback helpers remain available after that restoration.

This first goblin has one melee attack family and a stylized glove rather than individually animated fingers. Cloth is skinned; there is no cloth simulation. Artistic acceptance and broader balancing remain subject to play review. Public source publication remains held at the previously requested Owner approval gate.
