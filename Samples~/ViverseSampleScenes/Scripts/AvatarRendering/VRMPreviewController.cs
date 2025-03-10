#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class VRMPreviewController : MonoBehaviour
{
    [HideInInspector] public Camera previewCamera;
    [HideInInspector] public Transform avatarRoot;

    private GameObject currentModel;
    [SerializeField] private Animator animator;
    [HideInInspector] public RuntimeAnimatorController animatorController;
    private readonly List<string> availableTriggers = new List<string>();

    // Event to notify when animation triggers are discovered
    public event Action<List<string>> OnTriggersDiscovered;

    private bool autoRotate = true;
    private float rotationSpeed = 30f;
    public async Task<bool> LoadVRM(string vrmUrl)
    {
        // Clean up previous model if any
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
            availableTriggers.Clear();
        }

        try
        {
            // Load VRM model
            currentModel = await SampleVRMUtility.DownloadAndLoadVRM(vrmUrl);

            if (currentModel == null) return false;
            // Position and scale the model
            currentModel.transform.SetParent(avatarRoot);
            currentModel.transform.localPosition = Vector3.zero;
            currentModel.transform.localRotation = Quaternion.identity;

            // Setup animator
            animator = currentModel.GetComponent<Animator>();
            if (animator == null)
            {
	            animator = currentModel.AddComponent<Animator>();
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
	            Avatar avatar = AvatarBuilder.BuildGenericAvatar(currentModel, "");
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
	    Renderer[] renderers = currentModel.GetComponentsInChildren<Renderer>();
	    if (renderers.Length <= 0) return;
	    Bounds bounds = new Bounds(currentModel.transform.position, Vector3.zero);
	    foreach (Renderer subRenderer in renderers)
	    {
		    bounds.Encapsulate(subRenderer.bounds);
	    }

	    // Position camera to view the full avatar
	    float distance = bounds.size.magnitude * 1.5f;

	    // Adjust camera position to make avatar appear lower on screen
	    previewCamera.transform.position = new Vector3(0, bounds.center.y + bounds.size.y * 0.3f, -distance);

	    // Look at a point slightly below the center to keep avatar lower in frame
	    previewCamera.transform.LookAt(new Vector3(0, bounds.center.y - bounds.size.y * 0.3f, 0));
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
