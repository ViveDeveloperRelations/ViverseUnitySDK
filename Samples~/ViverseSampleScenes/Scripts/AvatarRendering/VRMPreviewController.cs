#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniGLTF;

public class VRMPreviewController : MonoBehaviour
{
    [HideInInspector] public Camera previewCamera;
    [HideInInspector] public Transform avatarRoot;

    private RuntimeGltfInstance currentModel;
    [SerializeField] private Animator animator;
    [HideInInspector] public RuntimeAnimatorController animatorController;
    private readonly List<string> availableTriggers = new List<string>();

    // Event to notify when animation triggers are discovered
    public event Action<List<string>> OnTriggersDiscovered;

    public bool autoRotate = true;
    private float rotationSpeed = 30f;

    public void OnDestroy()
    {
	    CleanupModelIfItExists();
    }

    void CleanupModelIfItExists()
    {
	    // Clean up previous model if any
	    if (currentModel == null) return;
	    Destroy(currentModel.Root);
	    currentModel = null;
	    availableTriggers?.Clear();
    }

    [ContextMenu("Load test vrm")]
    public void LoadTestVRM()
    {
	    Task.Run(async () =>
	    {
		    await LoadVRM("https://avatar.viverse.com/api/meetingareaselector/v2/newgenavatar/avatars/be13b1bc76b03d90dae55902820c31258be58ab6fae355906525f5ca70b1d4b1f3/files?filetype=model&lod=original");
	    });
    }
    public async Task<bool> LoadVRM(string vrmUrl)
    {
	    CleanupModelIfItExists();

        try
        {
            // Load VRM model
            currentModel = await SampleVRMUtility.DownloadAndLoadVRM(vrmUrl);

            if (currentModel == null) return false;
            // Position and scale the model
            currentModel.transform.SetParent(null);
            currentModel.transform.localPosition = Vector3.zero;
            currentModel.transform.localRotation = Quaternion.identity;

            // Setup animator
            animator = currentModel.GetComponent<Animator>();
            if (animator == null)
            {
	            animator = currentModel.Root.AddComponent<Animator>();
            }
            // Apply the animator controller if available
            if (animatorController != null)
            {
	            animator.runtimeAnimatorController = animatorController;
            }
            else
            {
	            Debug.Log("no animation controller set on test ui, so cannot show animations");
            }

            // Create a generic avatar if needed
            if (animator.avatar == null)
            {
	            Avatar avatar = AvatarBuilder.BuildGenericAvatar(currentModel.Root, "");
	            if (avatar != null)
	            {
		            avatar.name = "VRM Avatar";
		            animator.avatar = avatar;
	            }
            }

            // Find animation triggers on the controller
            DiscoverAnimationTriggers();

            // Set camera position to view the avatar
            SetupCamera();

            return true;

        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load VRM: {e.Message}");
            return false;
        }
    }
    public void SetAnimatorController(RuntimeAnimatorController controller)
    {
	    animatorController = controller;

	    // If the animator is already created, apply the controller immediately
	    if (animator != null)
	    {
		    animator.runtimeAnimatorController = controller;
		    // Re-discover the animation triggers with the new controller
		    DiscoverAnimationTriggers();
	    }
    }
    private void DiscoverAnimationTriggers()
    {
        availableTriggers.Clear();

        if (animator == null || animator.runtimeAnimatorController == null) return;
        try
        {
	        // Try to get parameters from the runtime controller
	        AnimatorControllerParameter[] parameters = animator.parameters;
	        if (parameters != null && parameters.Length > 0)
	        {
		        foreach (AnimatorControllerParameter parameter in parameters)
		        {
			        if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
			        availableTriggers.Add(parameter.name);
		        }
	        }
        }
        catch (Exception e)
        {
	        Debug.LogWarning($"Error getting animator parameters: {e.Message}");
        }

        // If no triggers found, add some default ones
        if (availableTriggers.Count == 0)
        {
	        availableTriggers.AddRange(new[] { "idle", "walk", "run", "wave", "jump" });
        }

        // Notify listeners
        OnTriggersDiscovered?.Invoke(availableTriggers);
    }

    private void SetupCamera()
	{
	    if (currentModel == null || previewCamera == null) return;

	    // Find the avatar's height
	    List<Renderer> renderers = new(currentModel.Renderers);
	    if (renderers.Count <= 0) return;

	    // Calculate proper bounds
	    Bounds bounds = renderers[0].bounds; // Use first renderer as initial bounds
	    for (int i = 1; i < renderers.Count; i++)
	    {
	        bounds.Encapsulate(renderers[i].bounds);
	    }

	    //Debug.Log($"Avatar bounds: Center={bounds.center}, Size={bounds.size}, " +
	    //         $"Extents={bounds.extents}, Min={bounds.min}, Max={bounds.max}");

	    // Compute distance based on height with a multiplier
	    float heightBasedDistance = bounds.size.y * 3.0f;

	    // Position camera at appropriate height and distance
	    Vector3 cameraPos = new Vector3(
	        0,                                      // Center horizontally
	        bounds.center.y + (bounds.size.y * 0.2f), // Position slightly above center (20% of height)
	        -heightBasedDistance                    // Place camera far enough to view full height
	    );

	    // Calculate look at point - slightly below center for better framing
	    Vector3 lookAtPoint = new Vector3(
	        bounds.center.x,                      // Look at center horizontally
	        bounds.center.y - (bounds.size.y * 0.1f), // Look slightly below center
	        bounds.center.z                       // Look at center depth
	    );

	    // Apply camera position and rotation
	    previewCamera.transform.position = cameraPos;
	    previewCamera.transform.LookAt(lookAtPoint);

	    // Calculate and set field of view based on the bounds
	    // This ensures the entire avatar is visible with some margin
	    float distanceToTarget = Vector3.Distance(cameraPos, lookAtPoint);
	    float requiredFOV = 2.0f * Mathf.Atan2(bounds.size.y / 1.8f, distanceToTarget) * Mathf.Rad2Deg;

	    // Add padding and ensure FOV is reasonable
	    requiredFOV += 10f; // Add padding
	    previewCamera.fieldOfView = Mathf.Clamp(requiredFOV, 40f, 90f);

	    //Debug.Log($"Camera settings: Position={cameraPos}, LookAt={lookAtPoint}, " +
	    //          $"Distance={distanceToTarget}, FOV={requiredFOV}");
	}

    public void PlayAnimation(string triggerName)
    {
        if (animator == null) return;

        // Reset all triggers first
        foreach (string trigger in availableTriggers)
        {
            animator.ResetTrigger(trigger);
        }

        // Set the new trigger
        try
        {
            animator.SetTrigger(triggerName);
            Debug.Log($"Playing animation trigger: {triggerName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error setting animation trigger '{triggerName}': {e.Message}");
        }
    }

    public void ResetView()
    {
        SetupCamera();
    }

    public void ToggleAutoRotate()
    {
        autoRotate = !autoRotate;
    }

    private void Update()
    {
        if (autoRotate && currentModel != null)
        {
            currentModel.transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        }
    }

    public string[] GetAvailableAnimations()
    {
	    return availableTriggers.ToArray();
    }
}
#endif
