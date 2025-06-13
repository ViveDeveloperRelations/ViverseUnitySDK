using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace ViverseWebGLAPI.Editor
{
    /// <summary>
    /// Post-build processor for WebGL builds that adds iframe.html wrapper
    /// </summary>
    public class ViverseWebGLPostBuildProcessor
    {
        [PostProcessBuildAttribute(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.WebGL)
            {
                return;
            }

            Debug.Log($"[Viverse] Processing WebGL build at: {pathToBuiltProject}");

            try
            {
                // Get the path to this script to locate the template
                string scriptPath = GetScriptPath();
                if (string.IsNullOrEmpty(scriptPath))
                {
                    Debug.LogError("[Viverse] Could not locate ViverseWebGLPostBuildProcessor script path");
                    return;
                }

                // Construct path to iframe template
                string editorFolder = Path.GetDirectoryName(scriptPath);
                string templatePath = Path.Combine(editorFolder, "Templates", "iframe.html");

                if (!File.Exists(templatePath))
                {
                    Debug.LogError($"[Viverse] iframe.html template not found at: {templatePath}");
                    return;
                }

                // Copy template to build directory
                string buildIframePath = Path.Combine(pathToBuiltProject, "iframe.html");
                
                // Always overwrite if exists
                if (File.Exists(buildIframePath))
                {
                    Debug.Log($"[Viverse] Overwriting existing iframe.html at: {buildIframePath}");
                }

                File.Copy(templatePath, buildIframePath, overwrite: true);
                Debug.Log($"[Viverse] Successfully created iframe.html in WebGL build: {buildIframePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Viverse] Failed to create iframe.html: {e.Message}");
            }

            // Check if auto-zip is enabled and create zip file
            CreateAutoZipIfEnabled(pathToBuiltProject);
        }

        /// <summary>
        /// Create a zip file of the build directory if auto-zip is enabled
        /// </summary>
        private static void CreateAutoZipIfEnabled(string pathToBuiltProject)
        {
            try
            {
                // Check if auto-zip is enabled via EditorPrefs
                bool autoZipEnabled = EditorPrefs.GetBool("AUTO_ZIP_BUILD_ENABLED", false);
                
                if (!autoZipEnabled)
                {
                    Debug.Log("[Viverse] Auto-zip is disabled, skipping zip creation");
                    return;
                }

                Debug.Log("[Viverse] Auto-zip is enabled, creating zip file...");

                // Get the build directory name (last folder in path)
                string buildFolderName = Path.GetFileName(pathToBuiltProject);
                
                // Get the parent directory (where we'll place the zip)
                string parentDirectory = Path.GetDirectoryName(pathToBuiltProject);
                
                // Create zip file path in parent directory
                string zipFileName = $"{buildFolderName}.zip";
                string zipFilePath = Path.Combine(parentDirectory, zipFileName);

                // Delete existing zip if it exists
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                    Debug.Log($"[Viverse] Deleted existing zip file: {zipFilePath}");
                }

                // Create the zip file
                Debug.Log($"[Viverse] Creating zip file: {zipFilePath}");
                Debug.Log($"[Viverse] Compressing contents of: {pathToBuiltProject}");

                ZipFile.CreateFromDirectory(pathToBuiltProject, zipFilePath, System.IO.Compression.CompressionLevel.Optimal, false);

                // Verify the zip was created and get its size
                if (File.Exists(zipFilePath))
                {
                    var zipInfo = new FileInfo(zipFilePath);
                    Debug.Log($"[Viverse] ✅ Successfully created zip file: {zipFilePath}");
                    Debug.Log($"[Viverse] Zip file size: {zipInfo.Length / (1024 * 1024):F2} MB");
                }
                else
                {
                    Debug.LogError("[Viverse] Zip file was not created successfully");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Viverse] Failed to create auto-zip: {e.Message}");
                Debug.LogError($"[Viverse] Stack trace: {e.StackTrace}");
            }
        }

        /// <summary>
        /// Get the file path of this script using AssetDatabase
        /// </summary>
        private static string GetScriptPath()
        {
            var scripts = AssetDatabase.FindAssets("t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonoScript>)
                .Where(ms => ms != null && ms.GetClass() == typeof(ViverseWebGLPostBuildProcessor));

            var script = scripts.FirstOrDefault();
            return script != null ? AssetDatabase.GetAssetPath(script) : null;
        }
    }
}