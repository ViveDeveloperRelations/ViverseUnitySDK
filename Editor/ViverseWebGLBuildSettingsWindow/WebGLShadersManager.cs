using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

/// <summary>
/// Manages shader-related settings for WebGL builds
/// </summary>
public class WebGLShadersManager
{
    /// <summary>
    /// Gets a list of essential shaders that should be included in WebGL builds
    /// </summary>
    public static List<string> GetEssentialShaders()
    {
        // In a real implementation, you might want to get these from UniVRM or another source
        // Replace with UniVRMEssentialShadersForPlatformHelper.EssentialShadersForRenderingPlatform()
        return new List<string>
        {
            "Standard",
            "Unlit/Texture",
            "Unlit/Color",
            "Sprites/Default",
            "UI/Default"
        };
    }

    /// <summary>
    /// Gets the list of shaders that are currently always included in the project
    /// </summary>
    public static List<Shader> GetAlwaysIncludedShaders()
    {
        List<Shader> shaders = new List<Shader>();
        SerializedObject gfxSettings = GetGraphicsSettingsSerializedObject();
        SerializedProperty alwaysIncludedShadersProperty = gfxSettings.FindProperty("m_AlwaysIncludedShaders");

        if (alwaysIncludedShadersProperty != null)
        {
            for (int i = 0; i < alwaysIncludedShadersProperty.arraySize; i++)
            {
                SerializedProperty element = alwaysIncludedShadersProperty.GetArrayElementAtIndex(i);
                Shader shader = element.objectReferenceValue as Shader;
                if (shader != null)
                {
                    shaders.Add(shader);
                }
            }
        }

        return shaders;
    }

    /// <summary>
    /// Gets the list of essential shaders that are not yet included in the always included shaders list
    /// </summary>
    public static List<string> GetMissingShaders()
    {
        var missingShaders = new List<string>();
        List<Shader> includedShaders = GetAlwaysIncludedShaders();
        HashSet<string> existingShaders = new HashSet<string>(includedShaders.Select(s => s.name));

        // Check which essential shaders are missing
        foreach (string shaderName in GetEssentialShaders())
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
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
    /// <returns>True if any shaders were added, false if all were already included</returns>
    public static bool AddMissingShaders()
    {
        SerializedObject gfxSettings = GetGraphicsSettingsSerializedObject();
        SerializedProperty alwaysIncludedShadersProperty = gfxSettings.FindProperty("m_AlwaysIncludedShaders");
        bool shadersAdded = false;

        if (alwaysIncludedShadersProperty != null)
        {
            List<string> essentialShaders = GetEssentialShaders();
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

    private static SerializedObject GetGraphicsSettingsSerializedObject()
    {
        return new SerializedObject(GraphicsSettings.GetGraphicsSettings());
    }
}
