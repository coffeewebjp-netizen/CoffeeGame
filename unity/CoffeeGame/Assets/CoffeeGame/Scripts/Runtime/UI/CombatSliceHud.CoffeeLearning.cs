using System;
using CoffeeGame.Actors;
using CoffeeGame.Domain;
using CoffeeGame.Input;
using CoffeeGame.Integration;
using CoffeeGame.Run;
using UnityEngine;

namespace CoffeeGame.UI
{
    public sealed partial class CombatSliceHud : MonoBehaviour
    {

        private async void HandleCoffeeLearningPrimary()
        {
            if (coffeeLearningConnection == null)
            {
                SetSystemNotice("CoffeeLearning接続が初期化できていません。");
                return;
            }

            if (coffeeLearningConnection.ConfirmationIntent != CoffeeLearningConfirmationIntent.Connect
                && coffeeLearningConnection.ConfirmationIntent != CoffeeLearningConfirmationIntent.Reconnect)
            {
                coffeeLearningConnection.RequestPrimaryAction();
            }

            SetSystemNotice("ブラウザでCoffeeLearningにログインし、CoffeeGAMEを開くを押すか、接続コードをコピーしてコード貼付してください。");
            await coffeeLearningConnection.ConfirmPrimaryActionAsync();
            await ConsumePendingCoffeeLearningBearer();
            SetSystemNotice(coffeeLearningConnection.StatusLabel);
        }


        private async void HandlePasteConnection()
        {
            if (coffeeLearningConnection == null)
            {
                SetSystemNotice("CoffeeLearning接続が初期化できていません。");
                return;
            }

            string pasted = ReadClipboardText();
            if (string.IsNullOrWhiteSpace(pasted) && !string.IsNullOrWhiteSpace(CoffeeGameDeepLink.LastUrl))
            {
                pasted = CoffeeGameDeepLink.LastUrl;
            }

            string message = await coffeeLearningConnection.TryApplyPastedAccessTokenAsync(pasted);
            SetSystemNotice(message);
        }


        private async System.Threading.Tasks.Task ConsumePendingCoffeeLearningBearer()
        {
            if (coffeeLearningConnection == null || !CoffeeGameDeepLink.TryTakePendingBearer(out string token))
            {
                return;
            }

            string message = await coffeeLearningConnection.TryApplyPastedAccessTokenAsync(token);
            SetSystemNotice(message);
        }


        private static string ReadClipboardText()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var clipboard = activity.Call<AndroidJavaObject>("getSystemService", "clipboard"))
                {
                    if (clipboard == null || !clipboard.Call<bool>("hasPrimaryClip"))
                    {
                        return string.Empty;
                    }

                    using (var clip = clipboard.Call<AndroidJavaObject>("getPrimaryClip"))
                    using (var item = clip.Call<AndroidJavaObject>("getItemAt", 0))
                    {
                        return item.Call<AndroidJavaObject>("coerceToText", activity)?.Call<string>("toString") ?? string.Empty;
                    }
                }
            }
            catch (Exception)
            {
            }
#endif
            return GUIUtility.systemCopyBuffer ?? string.Empty;
        }


        private async void HandleCoffeeLearningDisconnect()
        {
            if (coffeeLearningConnection == null)
            {
                return;
            }

            if (coffeeLearningConnection.ConfirmationIntent == CoffeeLearningConfirmationIntent.Disconnect)
            {
                await coffeeLearningConnection.ConfirmDisconnectActionAsync();
                return;
            }

            coffeeLearningConnection.RequestDisconnectAction();
        }


        private void HandleCoffeeLearningCancel()
        {
            coffeeLearningConnection?.CancelPendingOrActiveAction();
        }
    }
}
