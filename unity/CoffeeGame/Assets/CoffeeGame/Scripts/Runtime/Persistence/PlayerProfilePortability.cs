using System;
using System.IO;
using System.Text;
using CoffeeGame.Domain;
using UnityEngine;

namespace CoffeeGame.Persistence
{
    public static class PlayerProfilePortability
    {
        public const string PortableFileName = "CoffeeGAME-player-profile.json";

        public static string PortablePath =>
            Path.Combine(Application.persistentDataPath, "CoffeeGAME", PortableFileName);

        public static bool TryExport(PlayerProfileStore store, PlayerProgression progression, out string message)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (!store.TrySave(progression, out string localMessage))
            {
                message = localMessage;
                return false;
            }

            var portableStore = new PlayerProfileStore(PortablePath);
            if (!portableStore.TrySave(progression, out string portableMessage))
            {
                message = portableMessage;
                return false;
            }

            TryCopyToSharedDriveFolder(PortablePath);
            ShareExportedFile(PortablePath);
            message = "セーブを書き出しました。共有先で Google Drive を選べます。 " + PortablePath;
            return true;
        }

        public static bool TryImport(PlayerProfileStore store, out PlayerProgression progression, out string message)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            progression = null;
            string json = GUIUtility.systemCopyBuffer;
            if (LooksLikeProfile(json))
            {
                string clipboardPath = PortablePath + ".clipboard.json";
                File.WriteAllText(clipboardPath, json, new UTF8Encoding(false));
                return TryImportFromPath(store, clipboardPath, out progression, out message);
            }

            if (File.Exists(PortablePath))
            {
                return TryImportFromPath(store, PortablePath, out progression, out message);
            }

            string shared = FindNewestSharedCopy();
            if (!string.IsNullOrEmpty(shared))
            {
                return TryImportFromPath(store, shared, out progression, out message);
            }

            message = "取り込むセーブが見つかりません。PCで書き出した JSON をコピーするか、同じファイルをこの端末へ置いてください。";
            return false;
        }

        private static bool TryImportFromPath(
            PlayerProfileStore store,
            string path,
            out PlayerProgression progression,
            out string message)
        {
            var imported = new PlayerProfileStore(path);
            progression = imported.LoadOrCreate(out string loadMessage);
            if (loadMessage.Contains("初期化"))
            {
                message = "セーブを読み込めませんでした: " + loadMessage;
                progression = null;
                return false;
            }

            if (!store.TrySave(progression, out string saveMessage))
            {
                message = saveMessage;
                return false;
            }

            message = "セーブを取り込みました。レベルや学習報酬を確実に揃えるため、アプリを一度閉じて開き直してください。";
            return true;
        }

        private static bool LooksLikeProfile(string json)
        {
            return !string.IsNullOrWhiteSpace(json)
                && json.IndexOf("\"slimeJelly\"", StringComparison.Ordinal) >= 0
                && json.IndexOf("\"version\"", StringComparison.Ordinal) >= 0;
        }

        private static void TryCopyToSharedDriveFolder(string sourcePath)
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] candidates =
                {
                    Path.Combine(userProfile, "Google Drive", "CoffeeGAME"),
                    Path.Combine(userProfile, "GoogleDrive", "CoffeeGAME"),
                    @"I:\CoffeeGAME"
                };

                foreach (string folder in candidates)
                {
                    string root = Path.GetPathRoot(folder);
                    if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(folder);
                    File.Copy(sourcePath, Path.Combine(folder, PortableFileName), true);
                    return;
                }
            }
            catch (Exception)
            {
                // Portable export still succeeded; Drive copy is best-effort.
            }
        }

        private static string FindNewestSharedCopy()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] candidates =
            {
                Path.Combine(userProfile, "Google Drive", "CoffeeGAME", PortableFileName),
                Path.Combine(userProfile, "GoogleDrive", "CoffeeGAME", PortableFileName),
                Path.Combine(@"I:\CoffeeGAME", PortableFileName)
            };

            string newest = null;
            DateTime stamp = DateTime.MinValue;
            foreach (string path in candidates)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                DateTime write = File.GetLastWriteTimeUtc(path);
                if (write > stamp)
                {
                    stamp = write;
                    newest = path;
                }
            }

            return newest;
        }

        private static void ShareExportedFile(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setType", "application/json");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.SUBJECT", "CoffeeGAME save");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", json);
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>(
                               "createChooser",
                               intent,
                               "CoffeeGAMEのセーブを共有"))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not open the Android share sheet: " + exception.Message);
            }
#endif
        }
    }
}
