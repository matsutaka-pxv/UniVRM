#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// <summary>
/// Exports each UPM package in this repository as a .tgz tarball using
/// UnityEditor.PackageManager.Client.Pack.
///
/// The output directory is {projectRoot}/build/tarballs/.
///
/// Also usable from batch mode:
///   Unity.exe -batchmode -projectPath . -executeMethod UniVRM.DevOnly.PackageTgzExporter.ExportAll
/// </summary>
namespace UniVRM.DevOnly {
    public static class PackageTgzExporter {
        private const string UniVrmMenuRoot = "UniVRM";

        /// <summary>
        /// Project-relative paths to the UPM packages to export.
        /// </summary>
        private static readonly string[] PackageDirs =
        {
            "Packages/UniGLTF",
            "Packages/VRM",
            "Packages/VRM10",
        };

        private static readonly string BuildDir =
            Path.Combine(
                Application.dataPath,
                "..",
                "build",
                "tarballs"
            );

        // Kept as a static field so that a re-invocation via the menu can
        // cancel the previous run before starting a new one.
        private static EditorApplication.CallbackFunction _sUpdateCallback;

        [MenuItem(UniVrmMenuRoot + "/Export Package .tgz tarballs")]
        public static void ExportAll()
        {
            // Cancel any in-progress run.
            if (_sUpdateCallback != null) {
                EditorApplication.update -= _sUpdateCallback;
            }

            var outputDir = BuildDir;
            Directory.CreateDirectory(outputDir);

            var pending = new Queue<string>(PackageDirs);
            // Client.Pack returns a PackRequest that completes asynchronously
            // on the main thread, so we poll via EditorApplication.update
            // instead of blocking with a spin loop (which would deadlock).
            PackRequest currentRequest = null;
            string currentDir = null;

            _sUpdateCallback = () =>
            {
                // Wait for the current request to finish.
                if (currentRequest != null) {
                    if (!currentRequest.IsCompleted) {
                        return;
                    }
                    if (currentRequest.Status == StatusCode.Success) {
                        Debug.Log($"[PackageTgzExporter] Created: {currentRequest.Result.tarballPath}");
                    } else {
                        Debug.LogError(
                            $"[PackageTgzExporter] Failed to pack {currentDir}: {currentRequest.Error?.message}"
                        );
                    }
                }

                // Start the next package, skipping directories that don't exist.
                while (pending.Count > 0) {
                    currentDir = pending.Dequeue();
                    var fullPath = Path.GetFullPath(currentDir);
                    if (!Directory.Exists(fullPath)) {
                        Debug.LogWarning($"[PackageTgzExporter] Package directory not found: {fullPath}");
                        continue;
                    }
                    Debug.Log($"[PackageTgzExporter] Packing {currentDir} ...");
                    currentRequest = Client.Pack(fullPath, outputDir);
                    return;
                }

                // Queue empty — done.
                EditorApplication.update -= _sUpdateCallback;
                _sUpdateCallback = null;
                Debug.Log($"[PackageTgzExporter] All packages exported to: {outputDir}");
                EditorUtility.RevealInFinder(outputDir);
            };

            EditorApplication.update += _sUpdateCallback;
        }
    }
}
#endif
