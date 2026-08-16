using NUnit.Framework;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class Hd2dManifestValidationTests
    {
        [Test]
        public void DirectionalClip_AllStripSuppliesEveryRuntimeDirection()
        {
            Hd2dSpriteManifest manifest = CreateManifest(
                true,
                new Hd2dSpriteClipDefinition
                {
                    action = "Idle",
                    all = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_down")
                });

            Assert.That(manifest.IsUsable(out string reason), Is.True, reason);
        }

        [Test]
        public void DirectionalClip_RejectsIncompleteDirectionSetWithoutAllStrip()
        {
            Hd2dSpriteManifest manifest = CreateManifest(
                true,
                new Hd2dSpriteClipDefinition
                {
                    action = "Idle",
                    down = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_down")
                });

            Assert.That(manifest.IsUsable(out string reason), Is.False);
            Assert.That(reason, Does.Contain("down/side/up"));
        }

        [Test]
        public void EightDirectionalClip_RejectsMissingDiagonalStripsWithoutAllStrip()
        {
            Hd2dSpriteManifest manifest = CreateManifest(
                true,
                new Hd2dSpriteClipDefinition
                {
                    action = "Idle",
                    down = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_down"),
                    side = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_right"),
                    up = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_up")
                });
            manifest.eightDirectional = true;

            Assert.That(manifest.IsUsable(out string reason), Is.False);
            Assert.That(reason, Does.Contain("down/downSide/side/upSide/up"));
        }

        [Test]
        public void Strip_RejectsAmbiguousGridAndIndividualResourceContracts()
        {
            Hd2dSpriteStripDefinition strip = CreateFrame(
                "Art/HD2D/Hero/Frames/hero_idle_down");
            strip.resourcePath = "Art/HD2D/Hero/heroine-sheet";
            Hd2dSpriteManifest manifest = CreateManifest(
                false,
                new Hd2dSpriteClipDefinition
                {
                    action = "Idle",
                    all = strip
                });

            Assert.That(manifest.IsUsable(out string reason), Is.False);
            Assert.That(reason, Does.Contain("both resourcePath and resourcePaths"));
        }

        [Test]
        public void NormalizeJsonPlaceholders_RemovesPhantomAllFromDirectionalClip()
        {
            Hd2dSpriteClipDefinition clip = new Hd2dSpriteClipDefinition
            {
                action = "Idle",
                // Reproduces JsonUtility's omitted-object placeholder.
                all = new Hd2dSpriteStripDefinition(),
                down = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_down"),
                downSide = new Hd2dSpriteStripDefinition(),
                side = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_right"),
                upSide = new Hd2dSpriteStripDefinition(),
                up = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_up")
            };
            Hd2dSpriteManifest manifest = CreateManifest(true, clip);

            Assert.That(manifest.IsUsable(out string beforeReason), Is.False);
            Assert.That(beforeReason, Does.Contain("all strip"));

            manifest.NormalizeJsonPlaceholders();

            Assert.That(clip.all, Is.Null);
            Assert.That(clip.downSide, Is.Null);
            Assert.That(clip.upSide, Is.Null);
            Assert.That(manifest.IsUsable(out string afterReason), Is.True, afterReason);
        }

        [Test]
        public void NormalizeJsonPlaceholders_PreservesAllAndRemovesPhantomDirections()
        {
            Hd2dSpriteClipDefinition clip = new Hd2dSpriteClipDefinition
            {
                action = "Idle",
                all = CreateFrame("Art/HD2D/Hero/Frames/hero_jump_down"),
                down = new Hd2dSpriteStripDefinition(),
                downSide = new Hd2dSpriteStripDefinition(),
                side = new Hd2dSpriteStripDefinition(),
                upSide = new Hd2dSpriteStripDefinition(),
                up = new Hd2dSpriteStripDefinition()
            };
            Hd2dSpriteManifest manifest = CreateManifest(true, clip);

            manifest.NormalizeJsonPlaceholders();

            Assert.That(clip.all, Is.Not.Null);
            Assert.That(clip.down, Is.Null);
            Assert.That(clip.downSide, Is.Null);
            Assert.That(clip.side, Is.Null);
            Assert.That(clip.upSide, Is.Null);
            Assert.That(clip.up, Is.Null);
            Assert.That(manifest.IsUsable(out string reason), Is.True, reason);
        }

        [Test]
        public void HeroLocomotion_UsesSixAtlasFramesInEveryAuthoredDirection()
        {
            Assert.That(
                Hd2dSpriteManifestLoader.TryLoad(
                    "Art/HD2D/hero-hd2d",
                    out Hd2dSpriteManifest manifest,
                    out string error),
                Is.True,
                error);

            Hd2dSpriteClipDefinition walk = FindClip(manifest, "Walk");
            Hd2dSpriteClipDefinition run = FindClip(manifest, "Run");

            AssertFiveDirectionalAtlases(walk, 6, "hero_walk");
            AssertFiveDirectionalAtlases(run, 6, "hero_run");
            Assert.That(walk.framesPerSecond, Is.EqualTo(7.5f));
            Assert.That(run.framesPerSecond, Is.EqualTo(15f));
            Assert.That(manifest.eightDirectional, Is.True);
        }

        [Test]
        public void MultiRowAtlas_RejectsMismatchedFrameCellArrays()
        {
            Hd2dSpriteStripDefinition strip = new Hd2dSpriteStripDefinition
            {
                resourcePath = "Art/HD2D/Hero/Atlases/test",
                columns = 3,
                rows = 2,
                frameColumns = new[] { 0, 1, 2 },
                frameRows = new[] { 0, 1 }
            };
            Hd2dSpriteManifest manifest = CreateManifest(
                false,
                new Hd2dSpriteClipDefinition { action = "Idle", all = strip });

            Assert.That(manifest.IsUsable(out string reason), Is.False);
            Assert.That(reason, Does.Contain("same length"));
        }

        [Test]
        public void MultiRowAtlas_ResolvesEveryDeclaredCellInOrder()
        {
            Hd2dSpriteStripDefinition strip = new Hd2dSpriteStripDefinition
            {
                resourcePath = "Art/HD2D/Hero/Atlases/test",
                columns = 3,
                rows = 2,
                frameColumns = new[] { 0, 1, 2, 0, 1, 2 },
                frameRows = new[] { 0, 0, 0, 1, 1, 1 }
            };

            Assert.That(strip.ResolvedFrameCount, Is.EqualTo(6));
            for (int index = 0; index < 6; index++)
            {
                Assert.That(strip.GetColumn(index), Is.EqualTo(index % 3));
                Assert.That(strip.GetRowFromTop(index), Is.EqualTo(index / 3));
            }
        }

        [Test]
        public void HeroRun_InheritsOneGlobalScaleContract()
        {
            Assert.That(
                Hd2dSpriteManifestLoader.TryLoad(
                    "Art/HD2D/hero-hd2d",
                    out Hd2dSpriteManifest manifest,
                    out string error),
                Is.True,
                error);

            Hd2dSpriteClipDefinition run = FindClip(manifest, "Run");

            Assert.That(manifest.pixelsPerUnit, Is.EqualTo(540f));
            Assert.That(manifest.pivotY, Is.EqualTo(0.0625f));
            Assert.That(run.down.pixelsPerUnit, Is.Zero);
            Assert.That(run.downSide.pixelsPerUnit, Is.Zero);
            Assert.That(run.side.pixelsPerUnit, Is.Zero);
            Assert.That(run.upSide.pixelsPerUnit, Is.Zero);
            Assert.That(run.up.pixelsPerUnit, Is.Zero);
        }

        [Test]
        public void HeroMagic_UsesThreeChargeAndReleaseFramesInEveryDirection()
        {
            Assert.That(
                Hd2dSpriteManifestLoader.TryLoad(
                    "Art/HD2D/hero-hd2d",
                    out Hd2dSpriteManifest manifest,
                    out string error),
                Is.True,
                error);

            Hd2dSpriteClipDefinition charge = FindClip(manifest, "MagicCharge");
            Hd2dSpriteClipDefinition release = FindClip(manifest, "MagicRelease");

            Assert.That(charge.all, Is.Null);
            Assert.That(release.all, Is.Null);
            AssertFiveDirectionalFrames(charge, 3, "hero_magic_charge");
            AssertFiveDirectionalFrames(release, 3, "hero_magic_release");
        }

        [TestCase("Fall", "hero_fall_down", "hero_fall_right_v2", "hero_fall_up_v2")]
        [TestCase("Land", "hero_land_down", "hero_land_right_v2", "hero_land_up_v2")]
        [TestCase("Sword", "hero_sword_down_04_v4", "hero_sword_right_04_v4", "hero_sword_up_04_v4")]
        [TestCase("AirSlash", "hero_airslash_down_v2", "hero_airslash_right", "hero_airslash_up_v2")]
        public void HeroDirectionalActions_UseDistinctViewSpecificArt(
            string action,
            string expectedDown,
            string expectedSide,
            string expectedUp)
        {
            Assert.That(
                Hd2dSpriteManifestLoader.TryLoad(
                    "Art/HD2D/hero-hd2d",
                    out Hd2dSpriteManifest manifest,
                    out string error),
                Is.True,
                error);

            Hd2dSpriteClipDefinition clip = FindClip(manifest, action);

            Assert.That(clip.all, Is.Null, $"{action} must not reuse one view for every direction.");
            Assert.That(clip.down.resourcePaths[^1], Does.EndWith(expectedDown));
            Assert.That(clip.side.resourcePaths[^1], Does.EndWith(expectedSide));
            Assert.That(clip.up.resourcePaths[^1], Does.EndWith(expectedUp));
            Assert.That(clip.down.resourcePaths[^1], Is.Not.EqualTo(clip.side.resourcePaths[^1]));
            Assert.That(clip.side.resourcePaths[^1], Is.Not.EqualTo(clip.up.resourcePaths[^1]));
            Assert.That(clip.up.resourcePaths[^1], Is.Not.EqualTo(clip.down.resourcePaths[^1]));
        }

        [Test]
        public void HeroJump_UsesFourAuthoredAtlasFramesAcrossFiveDirections()
        {
            Assert.That(
                Hd2dSpriteManifestLoader.TryLoad(
                    "Art/HD2D/hero-hd2d",
                    out Hd2dSpriteManifest manifest,
                    out string error),
                Is.True,
                error);

            Hd2dSpriteClipDefinition jump = FindClip(manifest, "Jump");
            AssertFiveDirectionalAtlases(jump, 4, "hero_jump");
            Assert.That(jump.down.resourcePath, Is.Not.EqualTo(jump.downSide.resourcePath));
            Assert.That(jump.downSide.resourcePath, Is.Not.EqualTo(jump.side.resourcePath));
            Assert.That(jump.side.resourcePath, Is.Not.EqualTo(jump.upSide.resourcePath));
            Assert.That(jump.upSide.resourcePath, Is.Not.EqualTo(jump.up.resourcePath));
        }

        [Test]
        public void HeroSword_UsesFourFramesAcrossFiveDirections()
        {
            Assert.That(
                Hd2dSpriteManifestLoader.TryLoad(
                    "Art/HD2D/hero-hd2d",
                    out Hd2dSpriteManifest manifest,
                    out string error),
                Is.True,
                error);

            AssertFiveDirectionalFrames(FindClip(manifest, "Sword"), 4, "hero_sword");
        }

        private static void AssertFiveDirectionalFrames(
            Hd2dSpriteClipDefinition clip,
            int expectedCount,
            string actionPrefix)
        {
            AssertGeneratedStrip(clip.down.resourcePaths, expectedCount, actionPrefix, "down");
            AssertGeneratedStrip(clip.downSide.resourcePaths, expectedCount, actionPrefix, "down_right");
            AssertGeneratedStrip(clip.side.resourcePaths, expectedCount, actionPrefix, "right");
            AssertGeneratedStrip(clip.upSide.resourcePaths, expectedCount, actionPrefix, "up_right");
            AssertGeneratedStrip(clip.up.resourcePaths, expectedCount, actionPrefix, "up");
        }

        private static void AssertFiveDirectionalAtlases(
            Hd2dSpriteClipDefinition clip,
            int expectedCount,
            string actionPrefix)
        {
            AssertGeneratedAtlas(clip.down, expectedCount, actionPrefix, "down");
            AssertGeneratedAtlas(clip.downSide, expectedCount, actionPrefix, "down_right");
            AssertGeneratedAtlas(clip.side, expectedCount, actionPrefix, "right");
            AssertGeneratedAtlas(clip.upSide, expectedCount, actionPrefix, "up_right");
            AssertGeneratedAtlas(clip.up, expectedCount, actionPrefix, "up");
        }

        private static void AssertGeneratedAtlas(
            Hd2dSpriteStripDefinition strip,
            int expectedCount,
            string actionPrefix,
            string direction)
        {
            Assert.That(strip, Is.Not.Null);
            Assert.That(strip.resourcePaths, Is.Null.Or.Empty);
            Assert.That(strip.resourcePath, Does.EndWith($"{actionPrefix}_{direction}_v5"));
            Assert.That(strip.ResolvedFrameCount, Is.EqualTo(expectedCount));
            Assert.That(strip.frameColumns, Has.Length.EqualTo(expectedCount));
            Assert.That(strip.frameRows, Has.Length.EqualTo(expectedCount));
            for (int index = 0; index < expectedCount; index++)
            {
                Assert.That(strip.GetColumn(index), Is.InRange(0, strip.columns - 1));
                Assert.That(strip.GetRowFromTop(index), Is.InRange(0, strip.rows - 1));
            }
        }

        private static void AssertGeneratedStrip(
            string[] resourcePaths,
            int expectedCount,
            string actionPrefix,
            string direction)
        {
            Assert.That(resourcePaths, Has.Length.EqualTo(expectedCount));
            Assert.That(resourcePaths, Is.Unique);
            for (int index = 0; index < resourcePaths.Length; index++)
            {
                Assert.That(
                    resourcePaths[index],
                    Does.EndWith($"{actionPrefix}_{direction}_{index + 1:00}_v4"));
            }
        }

        private static Hd2dSpriteClipDefinition FindClip(
            Hd2dSpriteManifest manifest,
            string action)
        {
            foreach (Hd2dSpriteClipDefinition clip in manifest.clips)
            {
                if (clip != null && string.Equals(clip.action, action, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            Assert.Fail($"Missing hero {action} clip.");
            return null;
        }

        private static Hd2dSpriteManifest CreateManifest(
            bool directional,
            Hd2dSpriteClipDefinition clip)
        {
            return new Hd2dSpriteManifest
            {
                version = 2,
                characterId = "test",
                directional = directional,
                requiredActions = new[] { "Idle" },
                clips = new[] { clip }
            };
        }

        private static Hd2dSpriteStripDefinition CreateFrame(string resourcePath)
        {
            return new Hd2dSpriteStripDefinition
            {
                resourcePaths = new[] { resourcePath }
            };
        }
    }
}
