using System;
using UnityEngine;

namespace CoffeeGame.Persistence
{
    public static class AndroidCloudFolder
    {
        public const string KindAndroidSaf = "android-saf";

        public static bool HasFolder
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var folder = Open())
                    using (var activity = Activity())
                    {
                        return folder.CallStatic<bool>("hasFolder", activity);
                    }
                }
                catch (Exception)
                {
                    return false;
                }
#else
                return false;
#endif
            }
        }

        public static string Label
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var folder = Open())
                    using (var activity = Activity())
                    {
                        return folder.CallStatic<string>("getFolderLabel", activity) ?? string.Empty;
                    }
                }
                catch (Exception)
                {
                    return string.Empty;
                }
#else
                return string.Empty;
#endif
            }
        }

        public static bool TryWrite(string name, string text, out string error)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var folder = Open())
                using (var activity = Activity())
                {
                    string result = folder.CallStatic<string>("writeTextResult", activity, name, text);
                    if (result == "OK")
                    {
                        error = string.Empty;
                        return true;
                    }

                    error = result ?? "UNKNOWN";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
#else
            error = "Android only";
            return false;
#endif
        }

        public static bool TryRead(string name, out string text, out string error)
        {
            text = string.Empty;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var folder = Open())
                using (var activity = Activity())
                {
                    text = folder.CallStatic<string>("readText", activity, name);
                    if (string.IsNullOrEmpty(text))
                    {
                        error = "EMPTY";
                        return false;
                    }

                    error = string.Empty;
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
#else
            error = "Android only";
            return false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass Open()
        {
            return new AndroidJavaClass("jp.coffeetools.coffeegame.androidlib.CloudFolder");
        }

        private static AndroidJavaObject Activity()
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
#endif
    }
}
