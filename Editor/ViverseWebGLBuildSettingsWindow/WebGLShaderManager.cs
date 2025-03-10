using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Manages shader inclusion settings for WebGL builds
/// </summary>
public class WebGLShaderManager
{
    /// <summary>
    /// Gets a list of shaders that are always included in builds
    /// </summary>
    public List<Shader> GetAlwaysIncludedShaders()
    {
        List<Shader> shaders = new List<Shader>();
        SerializedObject gfxSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        SerializedProperty alwaysIncludedShadersProperty = gfxSettings.FindProperty("m_AlwaysIncludedShaders");

        if (alwaysIncludedShadersProperty == null) return shaders;
        for (int i = 0; i < alwaysIncludedShadersProperty.arraySize; i++)
        {
            SerializedProperty element = alwaysIncludedShadersProperty.GetArrayElementAtIndex(i);
            Shader shader = element.objectReferenceValue as Shader;
            if (shader != null)
            {
                shaders.Add(shader);
            }
        }

        return shaders;
    }

    /// <summary>
    /// Gets a list of essential shaders that should be included for WebGL builds
    /// </summary>
    public List<string> GetEssentialShaderNames()
    {
        // If UniVRM is installed, use its essential shaders
        #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
        return UniVRMEssentialShadersForPlatformHelper.EssentialShadersForRenderingPlatform();
        #else
        // Fallback to basic shaders if UniVRM is not installed
        return new List<string>
        {
            "Standard",
            "Unlit/Texture",
            "Unlit/Color",
            "Sprites/Default",
            "UI/Default",
            "Hidden/CubeBlur",
            "Hidden/CubeCopy",
            "Hidden/CubeBlend"
        };
        #endif
    }

    /// <summary>
    /// Gets the list of essential shaders that are missing from the always included shaders
    /// </summary>
    public List<string> GetMissingShaders()
    {
        var missingShaders = new List<string>();
        List<Shader> includedShaders = GetAlwaysIncludedShaders();
        HashSet<string> existingShaders = new HashSet<string>(includedShaders.Select(s => s.name));

        List<string> essentialShaders = GetEssentialShaderNames();

        // Check which essential shaders are missing
        foreach (string shaderName in essentialShaders)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                // Skip shaders that don't exist in the project
                continue;
            }

            if (!existingShaders.Contains(shader.name))
            {
                missingShaders.Add(shaderName);
            }
        }

        return missingShaders;
    }

    /// <summary>
    /// Adds all missing essential shaders to the always included shaders list
    /// </summary>
    /// <returns>True if any shaders were added, false otherwise</returns>
    public bool AddMissingShaders()
    {
        SerializedObject gfxSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        SerializedProperty alwaysIncludedShadersProperty = gfxSettings.FindProperty("m_AlwaysIncludedShaders");
        bool shadersAdded = false;

        if (alwaysIncludedShadersProperty != null)
        {
            List<string> essentialShaders = GetEssentialShaderNames();

            List<Shader> existingShaders = GetAlwaysIncludedShaders();
            HashSet<string> existingShaderNames = new HashSet<string>(existingShaders.Select(s => s.name));

            foreach (string shaderName in essentialShaders)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null || existingShaderNames.Contains(shader.name)) continue;

                // Add to always included shaders
                int index = alwaysIncludedShadersProperty.arraySize++;
                SerializedProperty element = alwaysIncludedShadersProperty.GetArrayElementAtIndex(index);
                element.objectReferenceValue = shader;
                shadersAdded = true;
                Debug.Log($"Added shader to always included: {shaderName}");
            }

            if (shadersAdded)
            {
                gfxSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(gfxSettings.targetObject);
                AssetDatabase.SaveAssets();
            }
        }

        return shadersAdded;
    }
}
