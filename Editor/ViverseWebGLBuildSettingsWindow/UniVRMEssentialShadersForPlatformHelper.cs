using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Helper class to identify essential shaders required for proper VRM rendering on the current platform.
/// This class needs to be compiled with UNI_VRM_INSTALLED and UNI_GLTF_INSTALLED defines.
/// </summary>
public static class UniVRMEssentialShadersForPlatformHelper
{
    /// <summary>
    /// Returns a list of essential shader names for VRM rendering on the current platform.
    /// </summary>
    /// <returns>A list of shader names that should be included in the build.</returns>
    public static List<string> EssentialShadersForRenderingPlatform()
    {
        List<string> shadersForPlatform = new List<string>(EssentialShaders);
        shadersForPlatform.AddRange(IsUsingUniversalRenderPipeline() ? URPEssentialShaders : BRPOnlyShaders);
        return shadersForPlatform;
    }

    // Essential shaders that should be included for all pipelines
    private static readonly string[] EssentialShaders = new[]
    {
        "VRM/MToon",
        "UI/Default",
        "UniGLTF/UniUnlit",
    };

    // Built-in Render Pipeline only shaders
    private static readonly string[] BRPOnlyShaders = new[]
    {
        "Standard",
        "Legacy Shaders/Diffuse",
        "Hidden/CubeBlur",
        "Hidden/CubeCopy",
        "Hidden/CubeBlend",
        "Hidden/VideoDecode",
        "Hidden/VideoComposite",
        "Sprites/Default",
    };

    // Universal Render Pipeline essential shaders
    private static readonly string[] URPEssentialShaders = new[]
    {
        "VRM10/Universal Render Pipeline/MToon10",
        "Sprites/Default",
    };

    /// <summary>
    /// Checks if the project is using Universal Render Pipeline.
    /// </summary>
    public static bool IsUsingUniversalRenderPipeline()
    {
        // Check if the current render pipeline asset exists and is URP
        var currentRenderPipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        return currentRenderPipelineAsset != null &&
               currentRenderPipelineAsset.GetType().ToString().Contains("UniversalRenderPipeline");
    }
}
