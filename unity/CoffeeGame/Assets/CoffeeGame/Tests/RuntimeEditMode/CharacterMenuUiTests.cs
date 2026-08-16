using System.IO;
using System.Linq;
using CoffeeGame.Input;
using CoffeeGame.Run;
using CoffeeGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class CharacterMenuUiTests
    {
        [Test]
        public void CharacterArtwork_LoadsAndUsesOpaqueImageTint()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("Character Menu UI Test");
            try
            {
                CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                view.Initialize(null);

                Image[] images = root.GetComponentsInChildren<Image>(true);
                Image portrait = images.Single(image => image.name == "Hero Portrait");
                Image fullBody = images.Single(image => image.name == "Hero Full Body");

                Assert.That(portrait.sprite, Is.Not.Null);
                Assert.That(portrait.sprite.name, Is.EqualTo("hero_portrait_ui"));
                Assert.That(portrait.color.a, Is.EqualTo(1f).Within(0.001f));
                Assert.That(fullBody.sprite, Is.Not.Null);
                Assert.That(fullBody.sprite.name, Is.EqualTo("hero_fullbody_ui"));
                Assert.That(fullBody.color.a, Is.EqualTo(1f).Within(0.001f));
                AssertPngHasTransparentCorner("hero_portrait_ui.png");
                AssertPngHasTransparentCorner("hero_fullbody_ui.png");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (originalEventSystem == null && EventSystem.current != null)
                {
                    Object.DestroyImmediate(EventSystem.current.gameObject);
                }
            }
        }

        [Test]
        public void RunOverlay_ExposesControllerSettingsBeforeBattle()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("Pre-Battle Controller Settings UI Test");
            try
            {
                CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                view.Initialize(null);

                bool requested = false;
                view.InputSettingsRequested += () => requested = true;
                Button settings = root.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "Pre-Battle Input Settings");

                Assert.That(settings.GetComponentInChildren<Text>().text, Is.EqualTo("コントローラー設定"));
                settings.onClick.Invoke();
                Assert.That(requested, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (originalEventSystem == null && EventSystem.current != null)
                {
                    Object.DestroyImmediate(EventSystem.current.gameObject);
                }
            }
        }

        [Test]
        public void PauseTabs_AreOrderedStatusItemsSystemParty_AndSystemExposesSave()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("Character Menu System Test");
            try
            {
                GameInputReader input = root.AddComponent<GameInputReader>();
                CombatRunController run = root.AddComponent<CombatRunController>();
                CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                view.Initialize(input);

                string[] tabLabels = root.GetComponentsInChildren<Button>(true)
                    .Where(button => button.name.StartsWith("Tab "))
                    .OrderBy(button => button.transform.GetSiblingIndex())
                    .Select(button => button.GetComponentInChildren<Text>().text)
                    .ToArray();
                Assert.That(tabLabels, Is.EqualTo(new[] { "ステータス", "持ち物", "システム", "仲間" }));
                Assert.That((int)CharacterMenuTab.System, Is.EqualTo(2));

                bool saveRequested = false;
                view.SaveRequested += () => saveRequested = true;
                view.SetSelectedTab(CharacterMenuTab.System);
                view.RebuildMenuContent(run);
                Button save = root.GetComponentsInChildren<Button>(true)
                    .Single(button => button.GetComponentInChildren<Text>()?.text == "セーブする");
                save.onClick.Invoke();

                Assert.That(saveRequested, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (originalEventSystem == null && EventSystem.current != null)
                {
                    Object.DestroyImmediate(EventSystem.current.gameObject);
                }
            }
        }

        [Test]
        public void ItemsTab_ShowsCurrentMaterialCount()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("Character Menu Items Test");
            try
            {
                CombatRunController run = root.AddComponent<CombatRunController>();
                CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                view.Initialize(null);
                view.SetSelectedTab(CharacterMenuTab.Inventory);
                view.RebuildMenuContent(run);

                Text content = root.GetComponentsInChildren<Text>(true)
                    .Single(text => text.name == "Content");
                Assert.That(content.text, Does.Contain("スライムゼリー"));
                Assert.That(content.text, Does.Contain("素材"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (originalEventSystem == null && EventSystem.current != null)
                {
                    Object.DestroyImmediate(EventSystem.current.gameObject);
                }
            }
        }

        [Test]
        public void StatusAndSystem_ReuseOneVisibleScrollAreaAndExposeTheirFullContent()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("Character Menu Visible Content Test");
            try
            {
                GameInputReader input = root.AddComponent<GameInputReader>();
                CombatRunController run = root.AddComponent<CombatRunController>();
                CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                view.Initialize(input);

                view.SetSelectedTab(CharacterMenuTab.Status);
                view.RebuildMenuContent(run);
                Canvas.ForceUpdateCanvases();

                ScrollRect scroll = root.GetComponentsInChildren<ScrollRect>(true).Single();
                Assert.That(scroll.viewport.rect.width, Is.GreaterThan(100f));
                Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100f));
                Assert.That(scroll.content.rect.height, Is.GreaterThan(100f));
                Assert.That(scroll.verticalScrollbar, Is.Not.Null);
                Assert.That(scroll.verticalScrollbar.name, Is.EqualTo("Vertical Scrollbar"));
                Assert.That(scroll.verticalScrollbarVisibility, Is.EqualTo(ScrollRect.ScrollbarVisibility.Permanent));
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                float initialScroll = scroll.verticalNormalizedPosition;
                view.ScrollMenu(-1f, 0.25f);
                Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(initialScroll));
                view.ScrollMenu(1f, 5f);
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
                Text status = scroll.content.GetComponentsInChildren<Text>(false)
                    .Single(text => text.name == "Content");
                Assert.That(status.text, Does.Contain("名もなき剣士"));
                Assert.That(status.text, Does.Contain("才能"));
                Assert.That(status.text, Does.Contain("経験値"));
                Assert.That(status.text, Does.Contain("お金"));
                Assert.That(status.text, Does.Contain("力"));
                Assert.That(status.text, Does.Contain("素早さ"));
                Assert.That(status.text, Does.Contain("技"));
                Assert.That(status.text, Does.Contain("運"));
                Assert.That(status.text, Does.Contain("体力"));
                Assert.That(status.text, Does.Contain("現在の補正"));

                view.SetSelectedTab(CharacterMenuTab.System);
                view.RebuildMenuContent(run);
                Canvas.ForceUpdateCanvases();

                Assert.That(root.GetComponentsInChildren<ScrollRect>(true), Has.Length.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<ScrollRect>(true).Single(), Is.SameAs(scroll));
                string[] headings = scroll.content.GetComponentsInChildren<Text>(false)
                    .Where(text => text.name.EndsWith(" Heading"))
                    .Select(text => text.text)
                    .ToArray();
                Assert.That(headings, Does.Contain("システム"));
                Assert.That(headings, Does.Contain("コントローラー設定"));
                Assert.That(headings, Does.Contain("セーブ"));

                string[] commands = scroll.content.GetComponentsInChildren<Button>(false)
                    .Select(button => button.GetComponentInChildren<Text>().text)
                    .ToArray();
                Assert.That(commands.Any(command => command.StartsWith("描画プリセット")), Is.True);
                Assert.That(commands.Any(command => command.StartsWith("FPS表示")), Is.True);
                Text frameStats = root.GetComponentsInChildren<Text>(true)
                    .Single(text => text.name == "Frame Stats");
                Assert.That(frameStats.text, Does.StartWith("FPS"));
                Assert.That(commands.Any(command => command.StartsWith("ジャンプ")), Is.True);
                Assert.That(commands.Any(command => command.StartsWith("刀攻撃")), Is.True);
                Assert.That(commands.Any(command => command.StartsWith("居合斬り")), Is.True);
                Assert.That(commands.Any(command => command.StartsWith("氷魔法")), Is.True);
                Assert.That(commands.Any(command => command.StartsWith("入力方式を選び直す")), Is.True);
                Assert.That(commands, Does.Contain("セーブする"));
                Assert.That(commands, Does.Contain("初期配置へ戻す"));
                Assert.That(commands, Does.Contain("戦闘へ戻る"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (originalEventSystem == null && EventSystem.current != null)
                {
                    Object.DestroyImmediate(EventSystem.current.gameObject);
                }
            }
        }

        [Test]
        public void NavigationAxis_FiresOnceUntilReleased()
        {
            int latch = 0;

            Assert.That(MenuNavigationAxisLatch.Read(0.8f, ref latch), Is.EqualTo(1));
            Assert.That(MenuNavigationAxisLatch.Read(0.8f, ref latch), Is.Zero);
            Assert.That(MenuNavigationAxisLatch.Read(0.4f, ref latch), Is.Zero);
            Assert.That(MenuNavigationAxisLatch.Read(0.8f, ref latch), Is.Zero);

            Assert.That(MenuNavigationAxisLatch.Read(0f, ref latch), Is.Zero);
            Assert.That(MenuNavigationAxisLatch.Read(0.8f, ref latch), Is.EqualTo(1));
        }

        [Test]
        public void NavigationAxis_AllowsOneImmediateDirectionReversal()
        {
            int latch = 0;

            Assert.That(MenuNavigationAxisLatch.Read(0.8f, ref latch), Is.EqualTo(1));
            Assert.That(MenuNavigationAxisLatch.Read(-0.8f, ref latch), Is.EqualTo(-1));
            Assert.That(MenuNavigationAxisLatch.Read(-0.8f, ref latch), Is.Zero);
        }

        [Test]
        public void PerformanceProfiles_PreserveCurrentByDefaultAndExposeBoundedOptions()
        {
            GamePerformanceProfile current =
                GamePerformanceSettings.GetProfile(GamePerformancePreset.KeepCurrent);
            GamePerformanceProfile balanced =
                GamePerformanceSettings.GetProfile(GamePerformancePreset.Balanced1080p60);
            GamePerformanceProfile smooth =
                GamePerformanceSettings.GetProfile(GamePerformancePreset.SmoothNative120);
            GamePerformanceProfile quality =
                GamePerformanceSettings.GetProfile(GamePerformancePreset.QualityNative60);

            Assert.That(current.TargetFrameRate, Is.EqualTo(-1));
            Assert.That(current.CapAt1080p, Is.False);
            Assert.That(balanced.CapAt1080p, Is.True);
            Assert.That(balanced.TargetFrameRate, Is.EqualTo(60));
            Assert.That(smooth.TargetFrameRate, Is.EqualTo(120));
            Assert.That(quality.QualityName, Is.EqualTo("Ultra"));
        }

        private static void AssertPngHasTransparentCorner(string fileName)
        {
            string path = Path.Combine(
                Application.dataPath,
                "CoffeeGame",
                "Resources",
                "Art",
                "UI",
                "Hero",
                fileName);
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, bytes), Is.True);
                Assert.That(texture.GetPixel(0, texture.height - 1).a, Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
