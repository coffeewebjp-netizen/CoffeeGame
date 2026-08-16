using System;
using UnityEngine;

namespace CoffeeGame.Integration
{
    public static class CoffeeGameDeepLink
    {
        public const string AppCallback = "coffeegame://coffee-game-callback";

        public static event Action<string> Received;

        public static string LastUrl { get; private set; } = string.Empty;

        public static bool TryParseCallback(string url, string expectedState, out string token, out string error)
        {
            token = null;
            error = null;
            if (string.IsNullOrWhiteSpace(url)
                || !url.StartsWith(AppCallback, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int separator = url.IndexOfAny(new[] { '?', '#' });
            string payload = separator >= 0 ? url.Substring(separator + 1) : string.Empty;
            var values = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string pair in payload.Split('&'))
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                int equals = pair.IndexOf('=');
                string key = Uri.UnescapeDataString((equals >= 0 ? pair.Substring(0, equals) : pair).Replace('+', ' '));
                string itemValue = equals >= 0
                    ? Uri.UnescapeDataString(pair.Substring(equals + 1).Replace('+', ' '))
                    : string.Empty;
                values[key] = itemValue;
            }

            if (!values.TryGetValue("state", out string receivedState)
                || receivedState != expectedState)
            {
                error = "接続stateが一致しません。CoffeeGAMEからやり直してください。";
                return false;
            }

            if (!values.TryGetValue("bearer", out string bearer) || string.IsNullOrWhiteSpace(bearer))
            {
                error = "CoffeeLearningから資格情報を受け取れませんでした。";
                return false;
            }

            try
            {
                token = CoffeeGameAccessToken.Normalize(bearer);
                return true;
            }
            catch (ArgumentException)
            {
                error = "CoffeeLearningがCoffeeGAME用の資格情報を返しませんでした。";
                return false;
            }
        }

        public static void Notify(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            LastUrl = url.Trim();
            Received?.Invoke(LastUrl);
        }
    }

    public sealed class CoffeeGameDeepLinkListener : MonoBehaviour
    {
        private void OnEnable()
        {
            Application.deepLinkActivated += CoffeeGameDeepLink.Notify;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                CoffeeGameDeepLink.Notify(Application.absoluteURL);
            }
        }

        private void OnDisable()
        {
            Application.deepLinkActivated -= CoffeeGameDeepLink.Notify;
        }
    }
}
