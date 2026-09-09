# Forest surface quality V20

Task ORC-20260910-001. Improve the existing forest's rough ground, folded foliage and hard shadows. Characters, collision, spawning, combat, camera controls and saves retain their existing behavior.

## Changes

The ground now uses a dedicated soil/leaf/moss albedo at two rotated scales, with broad irregular moss coverage. Tree leaves have narrower silhouettes and smooth curved normals; bark color variation is restrained. Grass blades bend through a middle section. The shared URP pipeline enables 2x MSAA and soft main-light shadows, retaining full render scale and the existing 2048 shadow map. Spatial batches, visual-only geometry, foreground tree visibility and combat-clock wind remain in place.

The old ground texture is retained. No Meshy credits were spent: the immediate defects were in surface shading and primitive foliage, so this pass improves those locally. The separate V20 player is built without regenerating any character, controller or scene. The explicit forest diagnostic uses memory-only progression and skips account refresh/profile load/save.

## Ground asset provenance

Built-in imagegen, one original generation on 2026-09-10; no external API key or purchased asset. Source output: `exec-144af0d6-e116-4b8d-8882-a9769d32f811.png`. Project copy: `unity/CoffeeGame/Assets/CoffeeGame/Resources/Art/Environment/ForestV20/forest-soil-albedo.png`. Imported as repeat, sRGB, mipmapped, trilinear, anisotropy 8, maximum 2048. Actual generated dimensions: 1254 x 1254, 3,174,755 bytes; the 2048 prompt request was not the output resolution.

Prompt:

> Use case: stylized-concept. Asset type: a seamless tileable albedo texture for the ground of an anime action RPG forest, not a scene or illustration. Produce one square 2048x2048 texture viewed perfectly orthographically straight down, covering about 2 metres of ground. Fine warm muted brown woodland soil with tiny grit and pebbles, small scattered dry leaf fragments, sparse low dark olive moss in irregular soft patches. Beautiful hand-painted natural detail with restrained contrast and cohesive colours, suitable for a polished stylized 3D game. Small features only; mostly traversable quiet soil, no large leaves, plants, roots, rocks, trails or compositional focal points. Uniform diffuse ambient illumination, no baked directional shadows or highlights, no perspective, no vignette, no border, no text, no watermark. Opposite edges should tile continuously in both directions. Preserve fine details that can be mipmapped at game scale.

## Verification and delivery

356/356 Domain/Input/Runtime EditMode tests pass, including texture mip/filter/Resource inclusion, collision/random isolation, finite geometry and owned-resource cleanup. Final scenery: 494,692 triangles, 43 batches; previous 473,636/43. Tree/rock/fern/grass counts remain 77/61/216/1,504.

Final diagnostic battle capture passes 51/51 checks including time-stop background separation, human input and cat/heroine switching. Six matching forest views at 1280 x 720 were inspected. Offscreen diagnostic median per view: 1.83–2.18 ms versus baseline 1.87–2.26 ms on the RTX 4070 Laptop GPU. Normal-player confirmation during Android compilation measured 2.26–3.18 ms; that concurrent load is not a controlled comparison. Both versions logged a graphics ring-buffer warning, but completed all images and timing samples. No speedup claim.

Normal Windows delivered 2026-09-10T01:38:56.4838027+09:00: 297 runtime files hash-verified; complete previous 348 files / 620,673,906 bytes retained. Rollback dry-run verifies both payloads. All captures were silent, restored input/display preferences and preserved the local profile hash. Cloud hash verification remains deferred because previous Drive reads stalled; explicit diagnostics never load or write real profiles.

Android development APK: 164232575 bytes; SHA256 `5315b2451cdefdb55bbfe8d04a1ae85a7eb91fd7ddc11e1d146cc454bf8253e0`; verified same prior development certificate, ARM64, minimum SDK 26. No connected device, so not installed or performance-tested on hardware.

[Before](../../art/environments/forest-v20/before.png) / [After](../../art/environments/forest-v20/after.png). Detailed evidence: `C:/work/task-backups/ORC-20260910-001` (baseline runtime-01, final diagnostic runtime-03/04, normal player runtime-05). Baseline target has 1 sample, V20 target has 2, so render timings include the intended antialiasing cost. The offscreen render/readback benchmark is not normal gameplay FPS or Android performance; other desktop applications are not controlled. Owner visual acceptance remains separate from automated correctness.

Windows entry: `CoffeeGame.Editor.BuildCoffeeGame.BuildForestV20NoSetup`. Android entry: `CoffeeGame.Editor.BuildCoffeeGame.BuildAndroidForestV20NoSetup`. Rollback tool: `tools/restore-pre-forest-v20-player.cmd`, once normal-player delivery is complete. Rollback preserves current character selection and save progress. Public source push remains held.
