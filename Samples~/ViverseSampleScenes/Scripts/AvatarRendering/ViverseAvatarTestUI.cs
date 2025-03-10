#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[Serializable]
public class ViverseAvatarTestUI
{
	private VRMPreviewController _vrmPreviewController;
	private VisualElement _vrmViewport;
	private VisualElement _animationControls;
	private Label _currentAnimationLabel;
	private ScrollView _animationList;
	private Button _resetViewButton;
	private Button _toggleRotationButton;
	[SerializeField] private string _currentVrmUrl;
	[FormerlySerializedAs("_sampleAnimatorController")] public RuntimeAnimatorController SampleAnimatorController;

	public void Initialize(VisualElement vrmViewport, VisualElement animationControls, Label currentAnimationLabel, ScrollView animationList, Button resetViewButton, Button toggleRotationButton, RuntimeAnimatorController sampleAnimatorController)
	{
		_vrmViewport = vrmViewport;
		_animationControls = animationControls;
		_currentAnimationLabel = currentAnimationLabel;
		_animationList = animationList;
		_resetViewButton = resetViewButton;
		_toggleRotationButton = toggleRotationButton;
		SampleAnimatorController = sampleAnimatorController;

		_resetViewButton.clicked += () => _vrmPreviewController.ResetView();
		_toggleRotationButton.clicked += () => _vrmPreviewController.ToggleAutoRotate();
	}

	private async Task<(bool,string)> SetVRMUrl(string vrmUrl)
	{
		if (string.IsNullOrEmpty(vrmUrl)) return(false, "Invalid VRM URL");
		_currentVrmUrl = vrmUrl;
		bool success = await _vrmPreviewController.LoadVRM(vrmUrl);
		if (!success) return (false, "Failed to load VRM model");
		// No need to call SetupAnimationControls() here as it will be handled
		// by the OnTriggersDiscovered event via SetupAnimationButtons()
		return(true, "VRM loaded successfully");
	}

	public void PlayAnimation(string animationName)
	{
		if (_vrmPreviewController == null) return;
		_vrmPreviewController.PlayAnimation(animationName);
		_currentAnimationLabel.text = animationName;
	}
	public async Task<(bool, string)> PreviewVRM(string vrmUrl)
	{
		// Ensure we have a controller
		InitializeVRMPreviewController();

		// Show loading state in the viewport
		Label placeholderLabel = _vrmViewport.Q<Label>("viewport-placeholder");
		if (placeholderLabel != null)
		{
			placeholderLabel.style.display = DisplayStyle.None;
		}

		if (SampleAnimatorController != null)
		{
			_vrmPreviewController.SetAnimatorController(SampleAnimatorController);
		}

		// Subscribe to the triggers discovered event
		_vrmPreviewController.OnTriggersDiscovered += SetupAnimationButtons;

		// Load the VRM
		(bool success, string message) = await SetVRMUrl(vrmUrl);

		// Update UI based on result
		if (success)
		{
			// Make sure the viewport is visible
			_vrmViewport.style.display = DisplayStyle.Flex;
			_animationControls.style.display = DisplayStyle.Flex;
		}
		else
		{
			if (placeholderLabel != null)
			{
				placeholderLabel.style.display = DisplayStyle.Flex;
			}
		}

		return (success, message);
	}
	private void SetupAnimationButtons(List<string> triggers)
	{
		// Clear existing buttons
		_animationList.Clear();

		// Create buttons for each discovered trigger
		foreach (string trigger in triggers)
		{
			Button button = new Button(() => PlayAnimation(trigger))
			{
				text = trigger
			};

			button.AddToClassList("animation-button");
			_animationList.Add(button);
		}

		// If there are triggers, select the first one by default
		if (triggers.Count > 0)
		{
			_currentAnimationLabel.text = triggers[0];
		}
	}

	public void InitializeVRMPreviewController()
	{
	    // Initialize VRM preview controller if not already done
	    if (_vrmPreviewController != null) return;

	    GameObject previewObj = new GameObject("VRM_Preview");
	    _vrmPreviewController = previewObj.AddComponent<VRMPreviewController>();
	    _vrmPreviewController.animatorController = SampleAnimatorController;
	    _vrmPreviewController.SetAnimatorController(SampleAnimatorController);

	    // Create a render texture for the camera
	    RenderTexture renderTexture = new RenderTexture(512, 512, 24);
	    //renderTexture.antiAliasing = 4;

	    // Add a camera for the preview
	    Camera previewCamera = previewObj.AddComponent<Camera>();
	    previewCamera.clearFlags = CameraClearFlags.SolidColor;
	    previewCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
	    previewCamera.transform.position = new Vector3(0, 1, -3);
	    previewCamera.transform.LookAt(Vector3.zero);
	    previewCamera.targetTexture = renderTexture;

	    // Add the render texture to the UI
	    Image previewImage = new Image
	    {
		    image = renderTexture,
		    style =
		    {
			    width = new StyleLength(new Length(100, LengthUnit.Percent)),
			    height = new StyleLength(new Length(100, LengthUnit.Percent))
		    }
	    };

	    // Clear any existing content in the viewport
	    _vrmViewport.Clear();
	    _vrmViewport.Add(previewImage);

	    // Add a placeholder label that will be hidden when a model is loaded
	    Label placeholderLabel = new Label("No VRM model loaded");
	    placeholderLabel.name = "viewport-placeholder";
	    placeholderLabel.AddToClassList("viewport-placeholder");
	    _vrmViewport.Add(placeholderLabel);

	    // Create avatar root
	    GameObject avatarRoot = new GameObject("Avatar_Root");
	    avatarRoot.transform.SetParent(previewObj.transform);

	    // Setup VRM Preview Controller
	    _vrmPreviewController.previewCamera = previewCamera;
	    _vrmPreviewController.avatarRoot = avatarRoot.transform;
	}

	public void Cleanup()
	{
		if (_vrmPreviewController == null) return;
		if (_vrmPreviewController.previewCamera != null &&
		    _vrmPreviewController.previewCamera.targetTexture != null)
		{
			_vrmPreviewController.previewCamera.targetTexture.Release();
		}

		Object.Destroy(_vrmPreviewController.gameObject);
		_vrmPreviewController = null;
	}
	public string[] GetAvailableAnimations()
	{
		if (_vrmPreviewController == null) return Array.Empty<string>();
		return _vrmPreviewController.GetAvailableAnimations();
	}

}

#endif
