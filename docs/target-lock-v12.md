# Target Lock V12 — 2026-09-06

ORC-20260906-006. This revision continues the accepted sword heroine model, plunge crouch, walking roll/running spin and paired enemies from Acrobatics V11.

V13 supersedes the running-facing, perfect-dodge detection and pending finisher notes below. The Owner's current request assigns the finisher to the existing charged iai; a staged redesign is not a prerequisite. See [Combat Polish V13](combat-polish-v13.md).

## Controls and behavior

- Guard + jump: both side sectors extend to the exact rear 45-degree diagonal (inclusive); farther backward selects the backflip. Both rear quadrants have continuous coverage. The lateral sector also tolerates forward diagonals through 45 degrees; directly forward remains guarded without a jump. Direction is relative to the actor's facing. This does not award dodge immunity or perfect-dodge stamina.
- R3 toggles the nearest live enemy within 18 metres. Keyboard and Steam Desktop fallback: R. The target has gold corner marks. The body faces it while walking, strafing and guarding; acrobatics/plunge keep their pose until landing. Camera orbit remains manual. Death, disappearance, 24-metre separation, actor switch and leaving combat clear the lock. Press again to release.
- Controller/Steam Desktop settings include target-lock rebinding. Existing semantic save formats remain v2/v1 and old custom mappings remain intact. If an old action already uses the new default control, lock stays unassigned with an explanatory settings message instead of triggering both actions.
- A successful perfect guard/dodge by the controlled actor causes a 0.65-second moment: world saturation reaches grayscale and time scale starts at 0.18, returning smoothly over the final 0.35 seconds. HUD remains readable in color. Duplicate hits do not prolong it. Ordinary damage/block does not trigger it. Pause clears the moment without unpausing; cat time stop retains its independent freeze and inverted image.

## Heroine voice ownership

Owner explicitly assigns the dragon-named handoff files to the sword heroine, separate from the dragon girl. The three adopted damage reactions are copied unchanged into `Resources/Audio/Voices/Heroine`: `hurt_01_u.wav`, `hurt_02_ita.wav`, `hurt_03_chi.wav`. Source package: `C:/work/media-assets/dragon_girl_hurt_20260906_v1`, corresponding `dragon_hurt_u/ita/chi.wav`. Original files remain. Runtime rotates one line per accepted unguarded hit, with an 0.85-second minimum interval; the special voice takes priority. Voice pauses with combat and stops when the actor is disabled. No generation, pitch or time modification.

The accepted stage-1, stage-2, v15 stage-3 and finisher files remain in the Owner media packages. Their gameplay activation mapping is pending the Owner answer: one special press for the whole sequence versus a press per stage. Current gameplay has one charged iai, so this delivery does not invent a staged ability or place the finisher at an incorrect early event. Existing ice/dodge/sword calls and the cat's optional special hook remain.

## Verification and delivery

Unity 6000.5.7f1, no model/controller setup regeneration. Full Domain/Input/Runtime EditMode suite: 305/305. First runtime capture stopped because its synthetic guard input reached only the motor; the harness was corrected to match PartyRuntime's motor/combat inputs. Final `runtime-02` passed 21 checks and produced 7 images; fixed target, both jump types, grayscale recovery and retained inverted time-stop images were reviewed. Diagnostic input/display preferences restored; cloud/local profile bytes unchanged. The 100-HP diagnostic values are memory-only.

Evidence: `C:/work/task-backups/ORC-20260906-006` (`tests-02.xml`, `build-02.log`, `runtime-02`). Normal executable remains `unity/CoffeeGame/Builds/Windows/CoffeeGAME.exe`; dedicated reviewed build is `Builds/Windows-TargetLockV12/CoffeeGAME-TargetLockV12.exe`. Local adoption uses a distinct hash-verified backup, `.task-local-backup/ORC-20260906-006-normal-player`. Steam shortcut and real progress are not modified. This revision has not been launched from the Steam UI; normal payload comparison to the reviewed build is the delivery check.

Adopted at 21:21 JST: all 297 new files match the reviewed build; 348 previous files are retained. Rollback dry run passed, normal DLL matches runtime-02, and the live save stayed byte-identical. Unity automatically normalized two render-setting files during validation; their original source hashes were restored after the build. Final 1,928-file preservation check has no out-of-scope difference.

Exit CoffeeGAME and run `tools/restore-pre-target-lock-v12-player.cmd` to restore the preceding V11. The PowerShell script without `-Restore` validates only. Restoration preserves current character selection and progress. No public source/audio push, main merge or server deployment.
