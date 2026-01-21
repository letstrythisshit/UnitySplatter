using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnitySplatter.Gaussian.IO;
using UnitySplatter.Gaussian.Rendering;
using UnitySplatter.Gaussian.Utilities;

namespace UnitySplatter.Gaussian.Playback
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GaussianSplatRenderer))]
    public sealed class GaussianSplatSequencePlayer : MonoBehaviour
    {
        [Header("Sequence Source")]
        [SerializeField] private GaussianSplatSequenceAsset bakedSequence;
        [SerializeField] private string runtimeDirectory;
        [SerializeField] private bool loadFromRuntimeDirectory = true;

        [Header("Playback")]
        [SerializeField, Range(1f, 120f)] private float frameRate = 30f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool preloadNext = true;

        private GaussianSplatRenderer rendererComponent;
        private readonly List<GaussianSplatAsset> runtimeFrames = new();
        private int currentIndex;
        private float timer;
        private bool isLoading;

        private void Awake()
        {
            rendererComponent = GetComponent<GaussianSplatRenderer>();
        }

        private void OnEnable()
        {
            ResetPlayback();
        }

        private void Update()
        {
            if (!TryEnsureFrames())
            {
                return;
            }

            timer += Time.deltaTime;
            var frameDuration = 1f / Mathf.Max(1f, frameRate);
            if (timer >= frameDuration)
            {
                timer -= frameDuration;
                AdvanceFrame();
            }
        }

        private bool TryEnsureFrames()
        {
            if (loadFromRuntimeDirectory)
            {
                if (runtimeFrames.Count == 0 && !isLoading)
                {
                    StartCoroutine(LoadRuntimeFrames());
                }
                return runtimeFrames.Count > 0;
            }

            return bakedSequence != null && bakedSequence.Frames.Count > 0;
        }

        private void AdvanceFrame()
        {
            var frames = GetFrames();
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            if (currentIndex >= frames.Count)
            {
                if (!loop)
                {
                    currentIndex = frames.Count - 1;
                    return;
                }

                currentIndex = 0;
            }

            rendererComponent.Asset = frames[currentIndex];
            currentIndex++;

            if (preloadNext && loadFromRuntimeDirectory)
            {
                StartCoroutine(PreloadFrame(currentIndex));
            }
        }

        private IReadOnlyList<GaussianSplatAsset> GetFrames()
        {
            if (loadFromRuntimeDirectory)
            {
                return runtimeFrames;
            }

            return bakedSequence != null ? bakedSequence.Frames : null;
        }

        public void ResetPlayback()
        {
            timer = 0f;
            currentIndex = 0;
            if (!loadFromRuntimeDirectory && bakedSequence != null)
            {
                frameRate = bakedSequence.FrameRate;
            }
        }

        private IEnumerator LoadRuntimeFrames()
        {
            isLoading = true;
            runtimeFrames.Clear();

            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                isLoading = false;
                yield break;
            }

            if (!Directory.Exists(runtimeDirectory))
            {
                Debug.LogWarning($"GaussianSplatSequencePlayer: Directory not found: {runtimeDirectory}");
                isLoading = false;
                yield break;
            }

            var files = Directory.GetFiles(runtimeDirectory, "*.ply");
            System.Array.Sort(files);

            foreach (var file in files)
            {
                GaussianSplatAsset asset = null;
                try
                {
                    asset = GaussianSplatLoader.LoadFromFile(file);
                }
                catch (IOException ex)
                {
                    Debug.LogError($"Failed to load PLY: {file} - {ex.Message}");
                }

                if (asset != null)
                {
                    runtimeFrames.Add(asset);
                }

                yield return null;
            }

            isLoading = false;
            ResetPlayback();
        }

        private IEnumerator PreloadFrame(int index)
        {
            if (index < 0 || index >= runtimeFrames.Count)
            {
                yield break;
            }

            yield return null;
        }
    }
}
