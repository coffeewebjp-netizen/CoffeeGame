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
                side = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_right"),
                up = CreateFrame("Art/HD2D/Hero/Frames/hero_idle_up")
            };
            Hd2dSpriteManifest manifest = CreateManifest(true, clip);

            Assert.That(manifest.IsUsable(out string beforeReason), Is.False);
            Assert.That(beforeReason, Does.Contain("all strip"));

            manifest.NormalizeJsonPlaceholders();

            Assert.That(clip.all, Is.Null);
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
                side = new Hd2dSpriteStripDefinition(),
                up = new Hd2dSpriteStripDefinition()
            };
            Hd2dSpriteManifest manifest = CreateManifest(true, clip);

            manifest.NormalizeJsonPlaceholders();

            Assert.That(clip.all, Is.Not.Null);
            Assert.That(clip.down, Is.Null);
            Assert.That(clip.side, Is.Null);
            Assert.That(clip.up, Is.Null);
            Assert.That(manifest.IsUsable(out string reason), Is.True, reason);
        }

        [Test]
        public void HeroLocomotion_AlternatesFeetInEveryDirection()
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

            AssertAlternatingPair(walk.down.resourcePaths, "v2");
            AssertAlternatingPair(walk.side.resourcePaths, "v2");
            AssertAlternatingPair(walk.up.resourcePaths, "v2");
            AssertAlternatingPair(run.down.resourcePaths, "v3");
            AssertAlternatingPair(run.side.resourcePaths, "v3");
            AssertAlternatingPair(run.up.resourcePaths, "v3");
        }

        [Test]
        public void HeroRun_NormalizesDirectionSpecificSourceScale()
        {
            Assert.That(
                Hd2dSpriteManifestLoader.TryLoad(
                    "Art/HD2D/hero-hd2d",
                    out Hd2dSpriteManifest manifest,
                    out string error),
                Is.True,
                error);

            Hd2dSpriteClipDefinition run = FindClip(manifest, "Run");

            Assert.That(run.down.pixelsPerUnit, Is.EqualTo(644f));
            Assert.That(run.side.pixelsPerUnit, Is.EqualTo(482f));
            Assert.That(run.up.pixelsPerUnit, Is.EqualTo(507f));
        }

        [Test]
        public void HeroMagic_UsesSafeChargeFramingAndDistinctReleaseFrame()
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

            Assert.That(charge.all.pixelsPerUnit, Is.GreaterThan(manifest.pixelsPerUnit));
            Assert.That(charge.all.usePivotOverride, Is.True);
            Assert.That(release.all, Is.Null);
            Assert.That(release.down.resourcePaths, Has.Length.EqualTo(2));
            Assert.That(release.side.resourcePaths, Has.Length.EqualTo(2));
            Assert.That(release.up.resourcePaths, Has.Length.EqualTo(2));
            Assert.That(release.down.resourcePaths[^1], Does.EndWith("hero_magic_release_v2"));
            Assert.That(release.side.resourcePaths[^1], Does.EndWith("hero_magic_release_v2"));
            Assert.That(release.up.resourcePaths[^1], Does.EndWith("hero_magic_release_v2"));
        }

        [TestCase("Jump", "hero_jump_down", "hero_jump_right_v2", "hero_jump_up_v2")]
        [TestCase("Fall", "hero_fall_down", "hero_fall_right_v2", "hero_fall_up_v2")]
        [TestCase("Land", "hero_land_down", "hero_land_right_v2", "hero_land_up_v2")]
        [TestCase("Sword", "hero_sword_down", "hero_sword_right_long_v2", "hero_sword_up_v2")]
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

        private static void AssertAlternatingPair(string[] resourcePaths, string version)
        {
            Assert.That(resourcePaths, Has.Length.EqualTo(2));
            Assert.That(resourcePaths[0], Is.Not.EqualTo(resourcePaths[1]));
            Assert.That(resourcePaths[0], Does.EndWith($"_a_{version}"));
            Assert.That(resourcePaths[1], Does.EndWith($"_b_{version}"));
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
