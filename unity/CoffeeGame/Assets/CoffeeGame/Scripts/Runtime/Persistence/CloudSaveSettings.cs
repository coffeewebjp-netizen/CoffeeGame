using System;
using System.IO;
using UnityEngine;

namespace CoffeeGame.Persistence
{
    public static class CloudSaveSettings
    {
        public const string KindPlayerPrefsKey = "CoffeeGame.Save.Kind.v1";
        public const string FolderPlayerPrefsKey = "CoffeeGame.Save.Folder.v1";
        public const string KindLocal = "local";
        public const string KindFolder = "folder";
        public const string KindAndroidSaf = AndroidCloudFolder.KindAndroidSaf;

        public static string Kind =>
            PlayerPrefs.HasKey(KindPlayerPrefsKey)
                ? PlayerPrefs.GetString(KindPlayerPrefsKey, KindLocal)
                : KindLocal;

        public static string FolderPath => PlayerPrefs.GetString(FolderPlayerPrefsKey, string.Empty);

        public static string StatusLabel
        {
            get
            {
                if (AndroidCloudFolder.HasFolder)
                {
                    return "クラウド連携: " + AndroidCloudFolder.Label;
                }

                if (Kind == KindFolder && !string.IsNullOrWhiteSpace(FolderPath))
                {
                    return "クラウド連携: " + FolderPath;
                }

                return "クラウド連携: 端末ローカル";
            }
        }

        public static string ResolveProfilePath()
        {
            if (Kind == KindFolder && Directory.Exists(FolderPath))
            {
                return Path.Combine(FolderPath, PlayerProfilePortability.PortableFileName);
            }

            return Path.Combine(Application.persistentDataPath, "CoffeeGAME", "player-profile.json");
        }

        public static void UseLocal()
        {
            PlayerPrefs.SetString(KindPlayerPrefsKey, KindLocal);
            PlayerPrefs.Save();
        }

        public static bool TryUseFolder(string folder, out string message)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                message = "フォルダパスが空です。";
                return false;
            }

            try
            {
                Directory.CreateDirectory(folder);
                PlayerPrefs.SetString(KindPlayerPrefsKey, KindFolder);
                PlayerPrefs.SetString(FolderPlayerPrefsKey, Path.GetFullPath(folder));
                PlayerPrefs.Save();
                message = "セーブ先を設定しました: " + Path.GetFullPath(folder);
                return true;
            }
            catch (Exception exception)
            {
                message = "セーブ先を設定できませんでした: " + exception.Message;
                return false;
            }
        }

        public static bool TryUseGoogleDrive(out string message)
        {
            foreach (string folder in GoogleDriveCandidates())
            {
                string root = Path.GetPathRoot(folder);
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    continue;
                }

                return TryUseFolder(folder, out message);
            }

            message = "Google Driveフォルダが見つかりません。パスをコピーして『セーブ先パスを取り込む』を押すか、スマホではフォルダ選択を使ってください。";
            return false;
        }

        public static bool TryUseClipboardFolder(out string message)
        {
            return TryUseFolder(GUIUtility.systemCopyBuffer, out message);
        }

        public static bool TryPickAndroidFolder(out string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                const int GrantRead = 1;
                const int GrantWrite = 2;
                const int GrantPersistable = 64;
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.OPEN_DOCUMENT_TREE"))
                {
                    intent.Call<AndroidJavaObject>("addFlags", GrantRead | GrantWrite | GrantPersistable);
                    activity.Call("startActivityForResult", intent, 7101);
                }

                PlayerPrefs.SetString(KindPlayerPrefsKey, KindAndroidSaf);
                PlayerPrefs.Save();
                message = "フォルダ画面を開きました。Google Drive の CoffeeGAME 用フォルダを選んでください。選んだあと、もう一度セーブするを押してください。";
                return true;
            }
            catch (Exception exception)
            {
                message = "フォルダ選択を開けませんでした: " + exception.Message;
                return false;
            }
#else
            message = "この端末ではフォルダ選択の代わりにパス指定を使います。";
            return false;
#endif
        }

        public static bool TryPublishLocalFile(string localPath, out string message)
        {
            if (!AndroidCloudFolder.HasFolder)
            {
                message = string.Empty;
                return false;
            }

            if (!File.Exists(localPath))
            {
                message = "ローカルセーブがまだないので Drive へ書けません。";
                return false;
            }

            string json = File.ReadAllText(localPath);
            if (!AndroidCloudFolder.TryWrite(PlayerProfilePortability.PortableFileName, json, out string error))
            {
                message = "Driveフォルダへ書けませんでした: " + error;
                return false;
            }

            message = "Driveフォルダへ保存しました: " + AndroidCloudFolder.Label;
            return true;
        }

        public static bool TryImportFromAndroidFolder(PlayerProfileStore store, out string message)
        {
            if (!AndroidCloudFolder.HasFolder)
            {
                message = "Driveフォルダがまだ選ばれていません。";
                return false;
            }

            if (!AndroidCloudFolder.TryRead(PlayerProfilePortability.PortableFileName, out string json, out string error))
            {
                message = "Driveフォルダから読めませんでした: " + error + " / " + AndroidCloudFolder.Label;
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(store.ProfilePath) ?? ".");
            File.WriteAllText(store.ProfilePath, json);
            message = "Driveフォルダから読みました: " + AndroidCloudFolder.Label;
            return true;
        }

        public static string[] GoogleDriveCandidates()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new[]
            {
                Path.Combine(userProfile, "Google Drive", "CoffeeGAME"),
                Path.Combine(userProfile, "GoogleDrive", "CoffeeGAME"),
                Path.Combine(userProfile, "マイドライブ", "CoffeeGAME"),
                @"I:\CoffeeGAME",
                @"G:\マイドライブ\CoffeeGAME"
            };
        }
    }
}
