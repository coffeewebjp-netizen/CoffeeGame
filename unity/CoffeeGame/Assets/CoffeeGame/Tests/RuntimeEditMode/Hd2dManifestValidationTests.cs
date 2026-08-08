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
