# Combat SFX production brief — cat locomotion / elemental revision

Owner request: stronger, distinct combat effects. Dedicated audio production can proceed separately; no new service/account/subscription is authorized by this brief. Existing voice lines and BGM stay separate. Do not generate dialogue.

## Delivery

- 48 kHz WAV, mono for short impacts; stereo for broad magic tails. No music, voice, ambience bed, silence lead-in, distortion or baked long room reverb.
- Make 3 candidates per priority effect. Keep transient sharp, body audible on phone speakers (not just sub-bass), short controlled tail. Peak at or below -1 dBFS; compare perceived loudness in game, not only peak normalization. Avoid harsh 3–6 kHz build-up when several attacks overlap.
- Deliver selected files named exactly below to `unity/CoffeeGame/Assets/CoffeeGame/Resources/Audio/Combat/`. Runtime loads the enum filename, preserving authored pitch; missing files use the current fallback. Keep alternatives in `art/audio/combat-v18/candidates/` outside Resources.
- Preserve source/provider, prompt, generation date, license/use rights and edits in `art/audio/combat-v18/manifest.json`. Do not include credentials or signed URLs. Preview only after the owner is ready for sound.

## Priority effects

| Filename | Length | Audible design / trigger |
| --- | --- | --- |
| FireRelease.wav | 0.35–0.55 s | Tight ignition crack + forceful flame whoosh; cat basic projectile release, 3-hit combo must not become a continuous roar. |
| ThunderRelease.wav | 0.8–1.2 s | Immediate electric snap + layered thunder body and short rolling tail; cat surrounding lightning discharge. |
| WindRelease.wav | 0.45–0.7 s | Fast rising wind blade, sharp pressure cut, airy trailing swirl; airborne cat wind release. |
| PlungeImpact.wav | 0.6–0.9 s | Hard earth impact, low midrange thump + stones breaking and outward debris; ground contact, not jump start. Shared sword/cat plunge. |
| SwordHit.wav | 0.18–0.35 s | Dry metallic strike mixed with weighty hit body; successful sword contact. |
| SwordSwing.wav | 0.15–0.3 s | Fast blade air cut, clear transient, compact tail; sword release. |
| Parry.wav | 0.35–0.65 s | Distinct bright metal deflection crack + brief resonant shimmer, recognizable above combat. |
| PerfectDodge.wav | 0.3–0.6 s | Sudden vacuum pull + crystalline time-snap cue, short airy recovery. |
| Counter.wav | 0.25–0.5 s | Decisive heavy strike and bright accent; successful counter hit. |
| SpinRelease.wav | 0.7–1.1 s | Powerful magical pressure break; special activation. Currently shared between characters. Do not bake the heroine/cat shout. |
| MagicCharge.wav | 0.8–1.2 s | Controlled energy rising with fine sparks, enough space for voice; major-magic charging start. |
| Jump.wav | 0.15–0.25 s | Athletic push-off, brief cloth swish and air lift. |
| Land.wav | 0.2–0.35 s | Firm foot contact, light clothing/stone detail, smaller than plunge. |

Secondary filenames: `BladeBlock` (0.15–0.3 s metal), `BarrierBlock` (0.2–0.4 s magic), `Impact` (0.3–0.5 s), `IceRelease` (0.4–0.7 s), `SpinCharge` (0.6–1 s), `Reward`, `LevelUp`, `Victory` (UI/reward stingers; no reused sword sound in final set).

Prompt pattern: "Single isolated stylized action RPG [event]. [attack/body/tail from table]. Strong readable transient, rich midrange, [duration] seconds. No voices, music, ambience, clipping or long reverb. Designed to layer with combat and remain clear on mobile speakers."

## Suggested production route

Use Gemini for prompt iteration/review if desired; generate the actual one-shot SFX in a service explicitly built for sound effects, such as [ElevenLabs Sound Effects](https://elevenlabs.io/docs/overview/capabilities/sound-effects). Its official documentation covers text descriptions and sound duration. [Google Lyria](https://deepmind.google/models/lyria/) is oriented toward music, while [Veo](https://deepmind.google/models/veo/) produces video with audio; those are different outputs from a clean collection of short game SFX.

## Acceptance

Listen with BGM at the ordinary setting and on phone speakers; check 3 rapid fire shots, lightning with two enemies, sword hit + voice overlap, perfect defense cue, special with voice, and time-stop freeze/resume. No clipping, delayed transient, cut-off tails, or overwhelming voice. Runtime hooks are prepared; no new generated audio has been adopted in this revision.
