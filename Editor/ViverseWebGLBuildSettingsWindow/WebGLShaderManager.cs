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
    public int GetMissingShadersCount()
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

        return missingShaders.Count;
    }
    public bool IsMissingPreloadedVariants()
	{
		// Check for URP variant collection if using URP
		bool isUsingURP = false;
#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
		isUsingURP = UniVRMEssentialShadersForPlatformHelper.IsUsingUniversalRenderPipeline();
#endif
		return isUsingURP && !IsURPShaderVariantCollectionIncluded();
	}

    /// <summary>
	/// Adds all missing essential shaders to the always included shaders list
	/// and adds URP shader variant collection if using URP
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

	        // Handle URP shader variant collection
	        bool isUsingURP = false;
	        #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
	        isUsingURP = UniVRMEssentialShadersForPlatformHelper.IsUsingUniversalRenderPipeline();
	        #endif

	        if (isUsingURP)
	        {
	            bool variantAdded = AddURPShaderVariantCollection();
	            shadersAdded = shadersAdded || variantAdded;
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

	/// <summary>
	/// Adds the URP shader variant collection to Graphics Settings preloaded shader collections
	/// </summary>
	/// <returns>True if the collection was added, false if it was already included</returns>
	private bool AddURPShaderVariantCollection()
	{
	    const string URP_SHADER_VARIANT_GUID = "179d48094d5ea464da56bf6fe34974ae";

	    // Get the asset path from GUID
	    string assetPath = AssetDatabase.GUIDToAssetPath(URP_SHADER_VARIANT_GUID);
	    if (string.IsNullOrEmpty(assetPath))
	    {
	        Debug.LogWarning($"Could not find URP shader variant collection with GUID: {URP_SHADER_VARIANT_GUID}");
	        return false;
	    }

	    // Load the shader variant collection
	    var shaderVariantCollection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(assetPath);
	    if (shaderVariantCollection == null)
	    {
	        Debug.LogWarning($"Could not load shader variant collection at path: {assetPath}");
	        return false;
	    }

	    // Access Graphics Settings preloaded shader collections
	    SerializedObject gfxSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
	    SerializedProperty preloadedShadersProperty = gfxSettings.FindProperty("m_PreloadedShaders");

	    if (preloadedShadersProperty == null)
	    {
	        Debug.LogWarning("Could not find preloaded shaders property in Graphics Settings");
	        return false;
	    }

	    // Check if this collection is already included
	    bool alreadyIncluded = false;
	    for (int i = 0; i < preloadedShadersProperty.arraySize; i++)
	    {
	        SerializedProperty element = preloadedShadersProperty.GetArrayElementAtIndex(i);
	        Object existingCollection = element.objectReferenceValue;

	        if (existingCollection != null && existingCollection == shaderVariantCollection)
	        {
	            alreadyIncluded = true;
	            break;
	        }
	    }

	    if (alreadyIncluded)
	        return false;

	    // Add to preloaded shader collections
	    int index = preloadedShadersProperty.arraySize++;
	    SerializedProperty element2 = preloadedShadersProperty.GetArrayElementAtIndex(index);
	    element2.objectReferenceValue = shaderVariantCollection;

	    // Save changes
	    gfxSettings.ApplyModifiedProperties();
	    EditorUtility.SetDirty(gfxSettings.targetObject);

	    Debug.Log($"Added URP shader variant collection to Graphics Settings preloaded shaders");
	    return true;
	}

	/// <summary>
	/// Checks if the URP shader variant collection is already included in Graphics Settings preloaded shader collections
	/// </summary>
	/// <returns>True if the collection is included, false otherwise</returns>
	public bool IsURPShaderVariantCollectionIncluded()
	{
	    const string URP_SHADER_VARIANT_GUID = "179d48094d5ea464da56bf6fe34974ae";

	    // Get the asset path from GUID
	    string assetPath = AssetDatabase.GUIDToAssetPath(URP_SHADER_VARIANT_GUID);
	    if (string.IsNullOrEmpty(assetPath))
	        return false;

	    // Load the asset
	    var shaderVariantCollection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(assetPath);
	    if (shaderVariantCollection == null)
	        return false;

	    // Access Graphics Settings preloaded shader collections
	    SerializedObject gfxSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
	    SerializedProperty preloadedShadersProperty = gfxSettings.FindProperty("m_PreloadedShaders");

	    if (preloadedShadersProperty == null)
	        return false;

	    // Check if this collection is already included
	    for (int i = 0; i < preloadedShadersProperty.arraySize; i++)
	    {
	        SerializedProperty element = preloadedShadersProperty.GetArrayElementAtIndex(i);
	        Object existingCollection = element.objectReferenceValue;

	        if (existingCollection != null && existingCollection == shaderVariantCollection)
	            return true;
	    }

	    return false;
	}
}
