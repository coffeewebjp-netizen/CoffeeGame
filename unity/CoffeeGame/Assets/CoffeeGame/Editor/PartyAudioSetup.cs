using System;
using UnityEditor;
using UnityEngine;

namespace CoffeeGame.Editor
{
    public static class PartyAudioSetup
    {
        private const string Root = "Assets/CoffeeGame/Resources/Audio/";
        public static void Configure()
        {
            AssetDatabase.Refresh();
            ConfigureClip(Root + "Music/ForestBattle20260906.ogg", true);
            foreach (string path in System.IO.Directory.GetFiles(Root + "Actions", "*.wav"))
                ConfigureClip(path.Replace('\\', '/'), false);
            foreach (string voice in new[] { "magic_02_freeze", "dodge_02_over_here", "sword_01_ya", "sword_02_ha" })
                ConfigureClip(Root + "Voices/Heroine/" + voice + ".wav", false);
            AssetDatabase.SaveAssets();
            Validate();
        }

        private static void ConfigureClip(string path, bool music)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Missing approved audio: " + path);
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = 0.8f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        public static void Validate()
        {
            var music = Resources.Load<AudioClip>("Audio/Music/ForestBattle20260906");
            if (music == null || Math.Abs(music.length - 121f) > 0.1f || music.channels != 2)
                throw new InvalidOperationException("Forest battle loop must be 121-second stereo audio.");
            foreach (string voice in new[] { "magic_02_freeze", "dodge_02_over_here", "sword_01_ya", "sword_02_ha" })
            {
                var clip = Resources.Load<AudioClip>("Audio/Voices/Heroine/" + voice);
                if (clip == null || clip.length <= 0.1f || clip.channels != 1)
                    throw new InvalidOperationException("Invalid approved heroine voice: " + voice);
            }
            foreach (string name in new[] { "sword_01_quick", "sword_02_arc", "sword_03_low", "jump_01_light", "jump_02_air", "jump_03_spark", "land_01_soft", "land_02_earth", "land_03_leaves" })
            {
                var clip = Resources.Load<AudioClip>("Audio/Actions/" + name);
                if (clip == null || clip.length <= 0.1f || clip.channels != 1)
                    throw new InvalidOperationException("Invalid approved action SFX: " + name);
            }
            Debug.Log("CoffeeGAME party audio validated: 121s forest loop, four heroine lines, nine action SFX.");
        }
    }
}
