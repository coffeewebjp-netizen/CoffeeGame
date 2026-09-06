# Forest clearing V7

Task: ORC-20260905-001 / IN17 / WP21. The Owner requested a richer forest background. This is a reversible presentation change around the accepted red-haori heroine, including the V6 sheathed breathing idle and V5 combat presentation.

## Implementation and provenance

`ForestArenaVisuals` generates original mesh geometry locally: tapered trunks, branches, roots, folded leaves, shrubs, ferns, grass, fallen leaves, mossy rocks and fallen timber. No purchased assets, external downloads or Meshy generation are involved. The original project grass texture adds fine variation to a world-space soil/moss surface.

The starting fight takes place in an irregular clearing, surrounded by foreground vegetation and taller outer tree layers. Warm directional light, cool ambient fill, foliage underside lighting, canopy shadows, gentle vertex wind and distance fog provide depth. Entire foreground trees/shrubs that obstruct the heroine are hidden with matching forward/depth clipping; shadows remain. All scenery is visual only. The original floor collider, four boundary colliders, camera orbit/zoom, actor movement, spawn coordinates and gameplay random stream are preserved.

Generation uses its own fixed-seed `System.Random`. Meshes share one scenery material and are combined into spatial sectors for culling, with a separate ground material. Only the cached heroine focus uniform is updated per frame; wind runs on the GPU. Generated meshes/materials are released when the scenery is destroyed, including editor inspection.

## Verification and delivery

Final scenery contains 77 trees, 61 rocks, 216 ferns, 1,504 grass clumps, shrubs, leaf litter and fallen timber: 473,636 triangles in 43 renderers, including the eight-triangle floor extension. All nine forest/lifetime/random-stream and existing terrain/camera EditMode tests pass on the final source. Unity 6000.5.7f1 no-setup build and player rendering pass. All six camera views were inspected; a visible outer-floor edge was fixed before delivery.

The explicit development option `-captureForest <directory>` records six camera views and times explicit 1280 x 720 URP offscreen renders, with a synchronous one-pixel readback to ensure GPU completion. It samples 180 renders per view after twelve warmups. `-legacyGrassland` selects the previous scenery for comparison without changing saved settings. The measurement includes submission/readback overhead and is not normal-play FPS or a combat/mobile performance test. On the RTX 4070 Laptop GPU, forest per-view medians were 3.07–3.34 ms versus 2.63–3.81 ms for the old grassland. Forest p95 values were 3.71–4.11 ms. Background applications were not controlled, so this supports a bounded local cost, not a claim that the forest is faster.

The first hidden-window sample measured the player update loop without guaranteed rendering and is invalid as a rendering benchmark; it is excluded from final measurements. First-pass screenshots also revealed distracting circular dither cutouts. The final implementation replaces these with per-tree visibility, rounds trunk normals and uses fuller six-triangle leaves. The earlier files remain only as local iteration evidence.

Use `CoffeeGame.Editor.BuildCoffeeGame.BuildForestV7NoSetup` to build the current resources without regenerating shared controllers or project setup. Output: `unity/CoffeeGame/Builds/Windows-ForestV7/CoffeeGAME-ForestV7.exe`.

Normal-player delivery uses a complete hash-verified backup, followed by replacement of runtime files only. Once delivered, close the game and run `tools/restore-pre-forest-v7-player.cmd` to return to the immediately preceding V6 player. Current character selection and save progress are retained. Public source push remains subject to the outstanding Owner approval; local delivery does not imply public disclosure authorization.

Delivered at 11:04:17 JST on 2026-09-06: all 348 old files / 588,781,789 bytes were backed up; 297 new files / 573,207,294 bytes were verified in the normal Windows folder. Fifty-one unrelated files remain. Runtime DLL SHA256: `969e29df1f140b6dea7d0145890cc39415546bd8412fb9a6136500d67f4f0461`. Non-mutating rollback verification passes; an actual rollback was not run.

Steam's existing shortcut URI launched the normal executable without game arguments at 11:06:04 JST (PID 53224). Its native window showed the forest, accepted heroine and first slime after selecting keyboard/mouse and starting the run. The game was then paused for Owner review. Steam's library window could not be surfaced, so this is a verified Steam URI launch, not a claimed Play-button click. Shortcut settings are byte-equivalent after excluding LastPlayTime, and 437 protected source/assets/settings files and the player profile match their backups.

Visibility deliberately hides whole foreground trees/shrubs; an appearance change can be visible when crossing the visibility threshold. Scenery has no collision/navigation. Full combat, mobile performance and final artistic acceptance remain outside these checks.

Evidence: [verification record](forest-v7-verification.json), [front view](../../art/environments/forest-v7/previews/final/center-0.png), [side view](../../art/environments/forest-v7/previews/final/center-90.png), [outer view](../../art/environments/forest-v7/previews/final/west.png), [previous grassland](../../art/environments/forest-v7/previews/baseline/center-0.png).
