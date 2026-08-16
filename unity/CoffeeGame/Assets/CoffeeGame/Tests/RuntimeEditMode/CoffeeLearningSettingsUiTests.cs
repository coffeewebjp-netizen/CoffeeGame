using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoffeeGame.Input;
using CoffeeGame.Integration;
using CoffeeGame.Run;
using CoffeeGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class CoffeeLearningSettingsUiTests
    {
        [Test]
        public void SystemTab_AppendsExplicitConfirmedConnectionActions()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("CoffeeLearning Settings UI Test");
            var store = new MemoryTokenStore();
            var service = new CountingConnectionService();
            using (var presenter = new CoffeeLearningConnectionPresenter(
                store,
                service,
                _ => new NullLearningBridge()))
            {
                try
                {
                    GameInputReader input = root.AddComponent<GameInputReader>();
                    CombatRunController run = root.AddComponent<CombatRunController>();
                    CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                    view.Initialize(input, presenter);
                    view.SetSelectedTab(CharacterMenuTab.System);
                    view.RebuildMenuContent(run);

                    Button primary = FindButton(root, "CoffeeLearning Primary");
                    Button disconnect = FindButton(root, "CoffeeLearning Disconnect");
                    Button cancel = FindButton(root, "CoffeeLearning Cancel");
                    Assert.That(primary.GetComponentInChildren<Text>().text,
                        Is.EqualTo("CoffeeLearning\u3068\u63a5\u7d9a"));
                    Assert.That(primary.interactable, Is.True);
                    Assert.That(disconnect.interactable, Is.False);
                    Assert.That(cancel.interactable, Is.False);

                    bool requested = false;
                    view.CoffeeLearningPrimaryRequested += () => requested = true;
                    primary.onClick.Invoke();
                    Assert.That(requested, Is.True);
                    Assert.That(service.ConnectCalls, Is.Zero,
                        "The settings click is routed to the HUD/presenter and cannot open a browser by itself.");

                    Assert.That(presenter.RequestPrimaryAction(), Is.True);
                    view.RebuildMenuContent(run);
                    primary = FindButton(root, "CoffeeLearning Primary");
                    cancel = FindButton(root, "CoffeeLearning Cancel");
                    Assert.That(primary.GetComponentInChildren<Text>().text,
                        Is.EqualTo("\u78ba\u8a8d: CoffeeLearning\u3068\u63a5\u7d9a\u3092\u958b\u59cb"));
                    Assert.That(cancel.interactable, Is.True);

                    Text status = root.GetComponentsInChildren<Text>(true)
                        .Single(text => text.name == "CoffeeLearning Status");
                    Assert.That(status.text, Is.EqualTo("CoffeeLearning: \u672a\u63a5\u7d9a"));
                    Assert.That(root.GetComponentsInChildren<Text>(true)
                        .All(text => !text.text.Contains("cgt_")), Is.True);
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
        }

        private static Button FindButton(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        }

        private sealed class MemoryTokenStore : ICoffeeGameAccessTokenStore
        {
            public bool HasAccessToken => false;

            public Task<string> LoadAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(string.Empty);
            }

            public Task SaveAccessTokenAsync(
                string accessToken,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class CountingConnectionService : ICoffeeLearningDesktopConnectionService
        {
            public int ConnectCalls { get; private set; }

            public Task ConnectAsync(CancellationToken cancellationToken = default)
            {
                ConnectCalls++;
                return Task.CompletedTask;
            }

            public Task DisconnectAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}
