using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    [DisallowMultipleComponent]
    public sealed class GaussianSplatSequencePlayer : MonoBehaviour
    {
        [Header("Sequence Source")]
        [SerializeField] private GaussianSplatSequenceAsset bakedSequence;
        [SerializeField] private TextAsset[] plyTextAssets;
        [SerializeField] private string[] plyFilePaths;
        [SerializeField] private bool useStreamingAssetsPath = true;

        [Header("Playback")]
        [SerializeField] private float framesPerSecond = 30f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnEnable = true;

        [Header("Renderer")]
        [SerializeField] private GaussianSplatRenderer rendererTarget;

        private readonly List<GaussianSplatFrameData> runtimeFrames = new();
        private int currentFrame;
        private float accumulatedTime;
        private bool isReady;
        private GaussianSplatAsset runtimeAsset;

        public bool IsReady => isReady;

        private void Awake()
        {
            if (rendererTarget == null)
            {
                rendererTarget = GetComponent<GaussianSplatRenderer>();
            }

            runtimeAsset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                StopAllCoroutines();
                StartCoroutine(InitializeAndPlay());
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            if (runtimeAsset != null)
            {
                Destroy(runtimeAsset);
            }
        }

        public IEnumerator InitializeAndPlay()
        {
            isReady = false;
            runtimeFrames.Clear();

            if (bakedSequence != null && bakedSequence.IsValid(out _))
            {
                runtimeFrames.AddRange(bakedSequence.Frames);
                framesPerSecond = bakedSequence.FramesPerSecond;
                loop = bakedSequence.Loop;
            }
            else
            {
                yield return LoadFromTextAssets();
                yield return LoadFromFilePaths();
            }

            if (runtimeFrames.Count == 0)
            {
                Debug.LogWarning("No Gaussian splat frames loaded for playback.", this);
                yield break;
            }

            isReady = true;
            currentFrame = 0;
            accumulatedTime = 0f;
            ApplyFrame(runtimeFrames[currentFrame]);
        }

        private IEnumerator LoadFromTextAssets()
        {
            if (plyTextAssets == null || plyTextAssets.Length == 0)
            {
                yield break;
            }

            foreach (TextAsset textAsset in plyTextAssets)
            {
                if (textAsset == null)
                {
                    continue;
                }

                GaussianSplatFrameData frame = GaussianSplatPlyLoader.LoadFromBytes(textAsset.bytes, textAsset.name);
                runtimeFrames.Add(frame);
                yield return null;
            }
        }

        private IEnumerator LoadFromFilePaths()
        {
            if (plyFilePaths == null || plyFilePaths.Length == 0)
            {
                yield break;
            }

            foreach (string path in plyFilePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string resolvedPath = useStreamingAssetsPath
                    ? Path.Combine(Application.streamingAssetsPath, path)
                    : path;

                if (!File.Exists(resolvedPath))
                {
                    Debug.LogWarning($"PLY file missing: {resolvedPath}", this);
                    continue;
                }

                GaussianSplatFrameData frame = GaussianSplatPlyLoader.LoadFromFile(resolvedPath);
                runtimeFrames.Add(frame);
                yield return null;
            }
        }

        private void Update()
        {
            if (!isReady || runtimeFrames.Count == 0)
            {
                return;
            }

            float fps = Mathf.Max(1f, framesPerSecond);
            accumulatedTime += Time.deltaTime;
            float frameDuration = 1f / fps;

            while (accumulatedTime >= frameDuration)
            {
                accumulatedTime -= frameDuration;
                currentFrame++;

                if (currentFrame >= runtimeFrames.Count)
                {
                    if (loop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        currentFrame = runtimeFrames.Count - 1;
                    }
                }

                ApplyFrame(runtimeFrames[currentFrame]);
            }
        }

        private void ApplyFrame(GaussianSplatFrameData frame)
        {
            if (rendererTarget == null)
            {
                return;
            }

            runtimeAsset.SetFrame(frame);
            rendererTarget.Asset = runtimeAsset;
        }
    }
}
