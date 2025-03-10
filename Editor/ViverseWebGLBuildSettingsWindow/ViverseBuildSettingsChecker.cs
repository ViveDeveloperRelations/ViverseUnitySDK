using UnityEditor;

namespace EditorHttpServer
{
    public class ViverseBuildSettingsChecker
    {
        //TODO: Turn this into a retained mode gui setup or a build pipeline step
        public static void CheckBuildSettings()
        {
#if UNITY_WEBGL
            // Check if WebGL is the current build target
            // Enable decompression fallback
            PlayerSettings.WebGL.decompressionFallback = true;
#endif
        }
    }
}