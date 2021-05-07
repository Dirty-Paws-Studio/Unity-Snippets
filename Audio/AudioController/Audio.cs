using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace General
{
    [CreateAssetMenu(fileName = "New Audio", menuName = "Template/Audio")]
    public class Audio : ScriptableObject
    {
        public AudioClip AudioClip => RandomEntry() ?? audioClip;

        [Tooltip(
            "If you only have a single audio clip, place it here. You can add variety by changing the Pitch Variation below.")]
        public AudioClip audioClip;

        [Tooltip(
            "If you have multiple variations of a single audio asset, add all of them here. The Audio Clip above will be ignored.")]
        public AudioClip[] audioClips;

        public string subtitle;

        public bool loop = false;
        [Range(0f, 3f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Range(0, 1f)] public float volumeVariation = 0f;
        [Range(0, 1f)] public float pitchVariation = 0f;

        [Header("Positional Audio (Experimental)")]
        [Tooltip("To take this into account, the sounds must be played with PlayPositionalSound(...)")]
        public bool useMaxRange = false;

        [Tooltip(
            "For 2D games, the AudioListener component should be moved from the camera to your player object. Otherwise, the camera's z-position influences the range.")]
        [Min(0)]
        public float maxRange = 10;

        private AudioClip RandomEntry()
        {
            return audioClips != null && audioClips.Length > 0
                ? audioClips[Random.Range(0, audioClips.Length)]
                : default;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Context Menu entry for creating Audio Asset from one or multiple AudioClips
        /// </summary>
        [MenuItem("Assets/Create/Template/Audio from Clip")]
        public static void CreateAudioFromClip()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
                return;

            var clips = Selection.objects.Select(o => o as AudioClip).Where(o => o != null).ToArray();
            if (clips.Length == 0)
                return;

            var firstClip = clips.First();
            var path = AssetDatabase.GetAssetPath(firstClip);

            var audioAsset = CreateInstance<Audio>();

            if (clips.Length == 1)
            {
                audioAsset.audioClip = firstClip;
            }
            else
            {
                clips = clips.OrderBy(c => c.name).ToArray();
                audioAsset.audioClips = clips;
            }

            var targetPath = Path.Combine(Path.GetDirectoryName(path) ?? "Assets/Audio", firstClip.name + ".asset");
            AssetDatabase.CreateAsset(audioAsset, targetPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();

            Selection.activeObject = audioAsset;
        }

        [MenuItem("Assets/Create/Template/Audio from Clip", true)]
        public static bool ValidateCreateAudioFromClip()
        {
            return Selection.objects != null && Selection.objects.Any(o => o is AudioClip);
        }
#endif
    }
}