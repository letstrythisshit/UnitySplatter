using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnitySplatter.Gaussian.Runtime
{
    [RequireComponent(typeof(GaussianSplatRenderer))]
    public sealed class GaussianSplatSequencePlayer : MonoBehaviour
    {
        [SerializeField] private GaussianSplatSequenceAsset sequenceAsset;
        [SerializeField] private string runtimeDirectory = string.Empty;
        [SerializeField] private bool loadFromStreamingAssets = true;
        [SerializeField] private float framesPerSecond = 30f;
        [SerializeField] private bool loop = true;

        private GaussianSplatRenderer rendererComponent;
        private readonly List<GaussianSplatAsset> runtimeFrames = new List<GaussianSplatAsset>();
        private float elapsed;
        private bool isReady;

        private void Awake()
        {
            rendererComponent = GetComponent<GaussianSplatRenderer>();
        }

        private void OnEnable()
        {
            elapsed = 0f;
            if (sequenceAsset != null)
            {
                framesPerSecond = sequenceAsset.FramesPerSecond;
                loop = sequenceAsset.Loop;
                isReady = true;
            }
            else
            {
                StartCoroutine(LoadRuntimeSequence());
            }
        }

        private void Update()
        {
            if (!isReady)
            {
                return;
            }

            elapsed += Time.deltaTime;
            int index = GetFrameIndex();
            GaussianSplatAsset frame = GetFrame(index);
            if (frame != null)
            {
                rendererComponent.Asset = frame;
            }
        }

        private int GetFrameIndex()
        {
            float fps = Mathf.Max(1f, framesPerSecond);
            float frame = elapsed * fps;
            int index = Mathf.FloorToInt(frame);
            int count = GetFrameCount();
            if (count <= 0)
            {
                return 0;
            }

            if (loop)
            {
                index %= count;
            }
            else
            {
                index = Mathf.Clamp(index, 0, count - 1);
            }

            return index;
        }

        private GaussianSplatAsset GetFrame(int index)
        {
            if (sequenceAsset != null)
            {
                return sequenceAsset.GetFrame(index);
            }

            if (runtimeFrames.Count == 0)
            {
                return null;
            }

            index = Mathf.Clamp(index, 0, runtimeFrames.Count - 1);
            return runtimeFrames[index];
        }

        private int GetFrameCount()
        {
            if (sequenceAsset != null)
            {
                return sequenceAsset.FrameCount;
            }

            return runtimeFrames.Count;
        }

        private IEnumerator LoadRuntimeSequence()
        {
            runtimeFrames.Clear();
            isReady = false;

            string directory = runtimeDirectory;
            if (loadFromStreamingAssets)
            {
                directory = Path.Combine(Application.streamingAssetsPath, runtimeDirectory);
            }

            if (!Directory.Exists(directory))
            {
                yield break;
            }

            string[] files = Directory.GetFiles(directory, "*.ply");
            System.Array.Sort(files);

            foreach (string file in files)
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                var asset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
                GaussianPoint[] points = PlyReader.Load(file, out Bounds bounds);
                asset.SetData(points, bounds, file, string.Empty);
                runtimeFrames.Add(asset);
                yield return null;
            }

            isReady = runtimeFrames.Count > 0;
        }
    }
}
