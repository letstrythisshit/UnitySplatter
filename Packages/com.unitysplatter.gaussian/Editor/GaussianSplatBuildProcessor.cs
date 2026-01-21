using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnitySplatter.Gaussian.Playback;

namespace UnitySplatter.Gaussian.Editor
{
    public sealed class GaussianSplatBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:GaussianSplatSequenceAsset");
            var missingFrames = new List<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sequence = AssetDatabase.LoadAssetAtPath<GaussianSplatSequenceAsset>(path);
                if (sequence == null)
                {
                    continue;
                }

                if (sequence.Frames == null || sequence.Frames.Count == 0)
                {
                    missingFrames.Add(path);
                }
            }

            if (missingFrames.Count > 0)
            {
                Debug.LogWarning("Gaussian Splat sequences without frames detected:\n" + string.Join("\n", missingFrames));
            }
        }
    }
}
