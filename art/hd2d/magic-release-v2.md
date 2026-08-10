# Hero magic release v2

The `MagicRelease` action previously reused the single `MagicCharge` frame. This version adds a distinct full-body release pose while preserving the existing heroine identity and outfit.

## Generation

- Mode: Codex built-in image generation, reference-guided edit.
- Edit target: `unity/CoffeeGame/Assets/CoffeeGame/Resources/Art/HD2D/Hero/Frames/hero_magic_down.png`.
- Supporting pose reference: `hero_spincharge_down.png`.
- Chroma-key source: `art/hd2d/reference/hero-magic-release-v2-keyed.png`.
- Alpha source: `art/hd2d/reference/hero-magic-release-v2.png`.
- Normalized game frame: `art/hd2d/frames/hero/hero_magic_release_v2.png` and its Unity Resources copy.

## Final prompt

> Use case: precise-object-edit. Asset type: full-body Unity HD-2D heroine action sprite, square PNG source for chroma-key removal. Image 1 is the edit target and identity/outfit source; Image 2 is a supporting pose and anatomy reference only. Preserve the exact same blue-haired heroine identity, face, body proportions, red haori, white shirt, orange pleated skirt, black gloves, boots, belt and sheathed katana from Image 1, but change her from holding a charged ice crystal into a clearly distinct magic-release pose. She braces low with both feet fully visible, leans slightly forward, thrusts her open left palm outward, and a compact sharp cyan-white ice burst has just launched away from the palm. Her right hand draws back near her torso to show recoil. The katana remains fully sheathed and attached at her hip. Match the existing polished anime game-sprite rendering, line weight, shading, color design and facial identity exactly. Front three-quarter action pose, full body centered in a square, every hair tip, sleeve, hand, ice effect, scabbard and both boots entirely inside the canvas with at least 48 pixels of empty padding on every edge; feet share the same ground level as Image 1. Use a perfectly flat solid #00ff00 chroma-key background with no shadows, gradients, texture or floor plane. Add crisp cold cyan magic light near the forward hand. Preserve character identity and outfit; exactly two arms, two hands, two legs and one sheathed katana; no cropped anatomy or objects, motion-blurred body, drawn sword, extra weapons, extra character, cast shadow, text, UI, logo, frame or watermark; do not use #00ff00 in the subject.

The built-in output was converted with the imagegen skill's installed `remove_chroma_key.py` helper, using border auto-key sampling, soft matte, despill, and the standard 12/220 transparency thresholds. The project export pipeline then normalizes the visible subject to the established 768 x 768 hero frame contract.
