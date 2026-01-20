using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;

namespace UnitySplatter.GaussianSplatting
{
    /// <summary>
    /// Streaming player for very large Gaussian splat sequences that don't fit in memory
    /// Implements frame prediction and async loading for smooth playback
    /// </summary>
    [RequireComponent(typeof(GaussianSplatRenderer))]
    public class GaussianSplatStreamingPlayer : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private string streamingAssetsPath = "GaussianSplatSequences/MySequence";
        [SerializeField] private bool useAbsolutePath = false;

        [Header("Playback")]
        [SerializeField] private float fps = 30f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool preloadNextFrame = true;

        [Header("Memory Management")]
        [SerializeField] private int maxCachedFrames = 5;
        [SerializeField] private int preloadFrameCount = 2; // Number of frames to preload ahead
        [SerializeField] private bool useCompression = true;

        [Header("Performance")]
        [SerializeField] private bool useBackgroundLoading = true;
        [SerializeField] private int loadThreadPriority = (int)System.Threading.ThreadPriority.Normal;

        // Runtime state
        private GaussianSplatRenderer splatRenderer;
        private GaussianSplatAsset runtimeAsset;
        private List<string> frameFilePaths = new List<string>();
        private Dictionary<int, GaussianSplatFrameData> frameCache = new Dictionary<int, GaussianSplatFrameData>();
        private Queue<int> cacheAccessOrder = new Queue<int>();

        private int currentFrameIndex = 0;
        private float frameTimer = 0f;
        private bool isPlaying = false;
        private int totalFrames = 0;

        // Async loading
        private Dictionary<int, Task<GaussianSplatFrameData>> loadingTasks = new Dictionary<int, Task<GaussianSplatFrameData>>();
        private object cacheLock = new object();

        // Statistics
        private int cacheHits = 0;
        private int cacheMisses = 0;
        private float averageLoadTime = 0f;

        public bool IsPlaying => isPlaying;
        public int CurrentFrame => currentFrameIndex;
        public int TotalFrames => totalFrames;
        public float Progress => totalFrames > 0 ? (float)currentFrameIndex / totalFrames : 0f;

        private void Awake()
        {
            splatRenderer = GetComponent<GaussianSplatRenderer>();
            runtimeAsset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
            if (splatRenderer == null)
            {
                Debug.LogError("[GaussianSplatStreamingPlayer] GaussianSplatRenderer component not found!", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            InitializeSequence();

            if (playOnEnable && totalFrames > 0)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
            ClearCache();
        }

        private void Update()
        {
            if (!isPlaying || totalFrames == 0)
                return;

            frameTimer += Time.deltaTime;

            float frameTime = 1f / fps;
            while (frameTimer >= frameTime)
            {
                frameTimer -= frameTime;
                AdvanceFrame();
            }
        }

        private void InitializeSequence()
        {
            try
            {
                string basePath = useAbsolutePath
                    ? streamingAssetsPath
                    : Path.Combine(Application.streamingAssetsPath, streamingAssetsPath);

                if (!Directory.Exists(basePath))
                {
                    Debug.LogError($"[GaussianSplatStreamingPlayer] Directory not found: {basePath}", this);
                    return;
                }

                // Find all PLY files in the directory
                frameFilePaths.Clear();
                string[] plyFiles = Directory.GetFiles(basePath, "*.ply", SearchOption.TopDirectoryOnly);

                if (plyFiles.Length == 0)
                {
                    Debug.LogError($"[GaussianSplatStreamingPlayer] No PLY files found in: {basePath}", this);
                    return;
                }

                // Sort files alphabetically for consistent ordering
                Array.Sort(plyFiles, StringComparer.Ordinal);
                frameFilePaths.AddRange(plyFiles);

                totalFrames = frameFilePaths.Count;

                Debug.Log($"[GaussianSplatStreamingPlayer] Initialized sequence with {totalFrames} frames from {basePath}");

                // Preload first frame
                if (totalFrames > 0)
                {
                    LoadFrameAsync(0).Wait(); // Synchronous load for first frame
                    UpdateRendererFrame(0);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatStreamingPlayer] Failed to initialize sequence: {e.Message}", this);
            }
        }

        private void AdvanceFrame()
        {
            int nextFrame = currentFrameIndex + 1;

            if (nextFrame >= totalFrames)
            {
                if (loop)
                {
                    nextFrame = 0;
                }
                else
                {
                    Stop();
                    return;
                }
            }

            currentFrameIndex = nextFrame;
            UpdateRendererFrame(currentFrameIndex);

            // Preload upcoming frames
            if (preloadNextFrame)
            {
                PreloadUpcomingFrames();
            }

            // Clean up old frames from cache
            ManageCache();
        }

        private void UpdateRendererFrame(int frameIndex)
        {
            GaussianSplatFrameData frameData = GetFrame(frameIndex);

            if (frameData != null && frameData.IsValid(out _))
            {
                runtimeAsset.SetFrame(frameData);
                splatRenderer.Asset = runtimeAsset;
            }
            else
            {
                Debug.LogWarning($"[GaussianSplatStreamingPlayer] Frame {frameIndex} not available");
            }
        }

        private GaussianSplatFrameData GetFrame(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= totalFrames)
                return null;

            lock (cacheLock)
            {
                // Check cache first
                if (frameCache.ContainsKey(frameIndex))
                {
                    cacheHits++;
                    return frameCache[frameIndex];
                }
            }

            cacheMisses++;

            // Check if frame is being loaded
            if (loadingTasks.ContainsKey(frameIndex))
            {
                // Wait for loading to complete
                var task = loadingTasks[frameIndex];
                task.Wait();
                return task.Result;
            }

            // Frame not in cache and not loading - load synchronously (blocking)
            Debug.LogWarning($"[GaussianSplatStreamingPlayer] Cache miss on frame {frameIndex}, loading synchronously");
            return LoadFrameSync(frameIndex);
        }

        private void PreloadUpcomingFrames()
        {
            for (int i = 1; i <= preloadFrameCount; i++)
            {
                int frameIndex = currentFrameIndex + i;

                if (frameIndex >= totalFrames)
                {
                    if (loop)
                        frameIndex = frameIndex % totalFrames;
                    else
                        break;
                }

                // Check if frame is already cached or being loaded
                bool needsLoading = false;

                lock (cacheLock)
                {
                    needsLoading = !frameCache.ContainsKey(frameIndex) && !loadingTasks.ContainsKey(frameIndex);
                }

                if (needsLoading)
                {
                    if (useBackgroundLoading)
                    {
                        LoadFrameAsync(frameIndex);
                    }
                    else
                    {
                        LoadFrameSync(frameIndex);
                    }
                }
            }
        }

        private async Task<GaussianSplatFrameData> LoadFrameAsync(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= totalFrames)
                return null;

            // Check if already loading
            if (loadingTasks.ContainsKey(frameIndex))
                return await loadingTasks[frameIndex];

            var task = Task.Run(() => LoadFrameInternal(frameIndex));
            loadingTasks[frameIndex] = task;

            var result = await task;

            // Remove from loading tasks
            loadingTasks.Remove(frameIndex);

            return result;
        }

        private GaussianSplatFrameData LoadFrameSync(int frameIndex)
        {
            return LoadFrameInternal(frameIndex);
        }

        private GaussianSplatFrameData LoadFrameInternal(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= totalFrames)
                return null;

            float startTime = Time.realtimeSinceStartup;

            try
            {
                string filePath = frameFilePaths[frameIndex];

                // Load PLY file
                GaussianSplatFrameData frameData = GaussianSplatPlyLoader.LoadFromFile(filePath);

                if (frameData == null || !frameData.IsValid(out _))
                {
                    Debug.LogError($"[GaussianSplatStreamingPlayer] Failed to load frame {frameIndex} from {filePath}");
                    return null;
                }

                // Add to cache
                lock (cacheLock)
                {
                    if (!frameCache.ContainsKey(frameIndex))
                    {
                        frameCache[frameIndex] = frameData;
                        cacheAccessOrder.Enqueue(frameIndex);
                    }
                }

                float loadTime = Time.realtimeSinceStartup - startTime;
                averageLoadTime = Mathf.Lerp(averageLoadTime, loadTime, 0.1f);

                return frameData;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatStreamingPlayer] Error loading frame {frameIndex}: {e.Message}");
                return null;
            }
        }

        private void ManageCache()
        {
            lock (cacheLock)
            {
                // Remove oldest frames if cache is too large
                while (frameCache.Count > maxCachedFrames && cacheAccessOrder.Count > 0)
                {
                    int oldestFrame = cacheAccessOrder.Dequeue();

                    // Don't remove if it's near current frame or being loaded
                    int distanceFromCurrent = Mathf.Abs(oldestFrame - currentFrameIndex);
                    if (distanceFromCurrent > preloadFrameCount + 1 && !loadingTasks.ContainsKey(oldestFrame))
                    {
                        frameCache.Remove(oldestFrame);
                    }
                    else
                    {
                        // Re-add to queue if we can't remove it
                        cacheAccessOrder.Enqueue(oldestFrame);
                    }
                }
            }
        }

        private void ClearCache()
        {
            lock (cacheLock)
            {
                frameCache.Clear();
                cacheAccessOrder.Clear();
            }

            // Cancel any pending loads
            foreach (var task in loadingTasks.Values)
            {
                // Tasks can't be cancelled, but we can ignore their results
            }
            loadingTasks.Clear();
        }

        // Public API

        public void Play()
        {
            if (totalFrames == 0)
            {
                Debug.LogWarning("[GaussianSplatStreamingPlayer] Cannot play: no frames loaded");
                return;
            }

            isPlaying = true;
            frameTimer = 0f;
        }

        public void Stop()
        {
            isPlaying = false;
            frameTimer = 0f;
        }

        public void Pause()
        {
            isPlaying = false;
        }

        public void Seek(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= totalFrames)
            {
                Debug.LogWarning($"[GaussianSplatStreamingPlayer] Invalid frame index: {frameIndex}");
                return;
            }

            currentFrameIndex = frameIndex;
            frameTimer = 0f;
            UpdateRendererFrame(currentFrameIndex);

            // Preload around new position
            if (preloadNextFrame)
            {
                PreloadUpcomingFrames();
            }
        }

        public void SetFPS(float newFPS)
        {
            fps = Mathf.Max(0.1f, newFPS);
        }

        // Statistics
        public float GetCacheHitRate()
        {
            int total = cacheHits + cacheMisses;
            return total > 0 ? (float)cacheHits / total : 1f;
        }

        public int GetCachedFrameCount()
        {
            lock (cacheLock)
            {
                return frameCache.Count;
            }
        }

        public float GetAverageLoadTime() => averageLoadTime;

#if UNITY_EDITOR
        private void OnValidate()
        {
            fps = Mathf.Max(0.1f, fps);
            maxCachedFrames = Mathf.Max(1, maxCachedFrames);
            preloadFrameCount = Mathf.Max(0, preloadFrameCount);
        }
#endif
    }
}
