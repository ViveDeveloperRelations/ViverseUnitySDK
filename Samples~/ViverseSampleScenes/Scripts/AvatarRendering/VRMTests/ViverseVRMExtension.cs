#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using ViverseWebGLAPI;
using Avatar = ViverseWebGLAPI.Avatar;

/// <summary>
/// Extension component that handles VRM avatar functionality for the VIVERSE SDK UI.
/// This component is only compiled when UniVRM and UniGLTF packages are installed.
/// </summary>
public class ViverseVRMExtension : MonoBehaviour
{
    // Reference to the main SDK UI
    private ViverseTestUIDocument mainUI;

    // UI Elements
    private VisualElement _avatarContainer;
    private VisualElement _vrmViewport;
    private VisualElement _animationControls;
    private Label _currentAnimationLabel;
    private ScrollView _animationList;
    private Button _resetViewButton;
    private Button _toggleRotationButton;
    private Button _cycleAvatarsButton;
    private Slider _cycleDurationSlider;
    private Label _cycleStatusLabel;

    // VRM Components
    private ViverseAvatarTestUI _avatarUI = new ViverseAvatarTestUI();
    [SerializeField] private RuntimeAnimatorController _sampleAnimationController;
    private AvatarCycler _avatarCycler;

    public void Initialize(ViverseTestUIDocument mainUI, VisualElement rootElement, RuntimeAnimatorController sampleAnimationController)
    {
        this.mainUI = mainUI;
        // Get references to UI elements
        _avatarContainer = rootElement.Q<VisualElement>("avatar-container");
        _vrmViewport = rootElement.Q<VisualElement>("vrm-viewport");
        _animationControls = rootElement.Q<VisualElement>("animation-controls");
        _currentAnimationLabel = rootElement.Q<Label>("current-animation");
        _animationList = rootElement.Q<ScrollView>("animation-list");
        _resetViewButton = rootElement.Q<Button>("reset-view-button");
        _toggleRotationButton = rootElement.Q<Button>("toggle-rotation-button");
        _cycleAvatarsButton = rootElement.Q<Button>("cycle-avatars-button");
        _cycleDurationSlider = rootElement.Q<Slider>("cycle-duration-slider");
        _cycleStatusLabel = rootElement.Q<Label>("cycle-status-label");

        _sampleAnimationController = sampleAnimationController;
        _avatarUI.SampleAnimatorController = _sampleAnimationController;

        // Initialize the avatar cycler
        if (_avatarCycler == null)
        {
            GameObject cyclerObj = new GameObject("AvatarCycler");
            _avatarCycler = cyclerObj.AddComponent<AvatarCycler>();
            _avatarCycler.Initialize(_avatarUI);
            _avatarCycler.OnStatusChange += UpdateCyclerStatus;
        }

        // Initialize the avatar UI helper
        _avatarUI.Initialize(
            _vrmViewport,
            _animationControls,
            _currentAnimationLabel,
            _animationList,
            _resetViewButton,
            _toggleRotationButton,
            _sampleAnimationController
        );

        // Set default value for cycle duration slider
        if (_cycleDurationSlider != null)
        {
            _cycleDurationSlider.value = 5f;
        }

        // Setup event handlers
        _cycleAvatarsButton.clicked += ToggleAvatarCycling;
        if (_cycleDurationSlider != null)
        {
            _cycleDurationSlider.RegisterValueChangedCallback(evt => {
                _avatarCycler.SetDisplayDuration(evt.newValue);
            });
        }
    }

    public async Task<(bool, string)> PreviewAvatarVRM(string vrmUrl)
    {
        try
        {
            (bool success, string message) = await _avatarUI.PreviewVRM(vrmUrl);
            return (success, message);
        }
        catch (Exception e)
        {
            return (false, $"Error previewing avatar: {e.Message}");
        }
    }

    public void DisplayAvatars(Avatar[] avatars)
    {
        _avatarContainer.Clear();

        if (avatars == null || avatars.Length == 0)
        {
            mainUI.ShowMessage("No avatars found");
            return;
        }

        foreach (Avatar avatar in avatars)
        {
            if (string.IsNullOrEmpty(avatar.vrmUrl)) continue;

            var card = new VisualElement();
            card.AddToClassList("avatar-card");

            var image = new Image();
            image.AddToClassList("avatar-image");
            if (!string.IsNullOrEmpty(avatar.snapshot))
            {
                mainUI.StartCoroutine(mainUI.LoadAvatarImage(image, avatar.snapshot));
            }

            // Add preview button
            Button previewButton = new Button(() => PreviewAvatarVRMWithLoading(avatar.vrmUrl))
            {
                text = "Preview Avatar"
            };
            previewButton.AddToClassList("preview-button");

            card.Add(image);
            card.Add(previewButton);
            _avatarContainer.Add(card);
        }
    }

    private async void PreviewAvatarVRMWithLoading(string vrmUrl)
    {
        mainUI.SetLoading(true, "Loading avatar for preview...");

        try
        {
            var (success, message) = await PreviewAvatarVRM(vrmUrl);

            if (success)
            {
                mainUI.ShowMessage(message);
            }
            else
            {
                mainUI.ShowError(message);
            }
        }
        finally
        {
            mainUI.SetLoading(false);
        }
    }

    private void ToggleAvatarCycling()
    {
        if (_avatarCycler.IsCycling)
        {
            _avatarCycler.StopCycling();
            _cycleAvatarsButton.text = "Cycle Through Avatars";
        }
        else
        {
            StartAvatarCycling();
        }
    }

    public async void StartAvatarCycling()
    {
        if (_avatarCycler.IsCycling) return;

        mainUI.SetLoading(true, "Loading avatars for cycling...");

        try
        {
            List<Avatar> allAvatars = new List<Avatar>();
#if UNITY_EDITOR
            allAvatars.Add(new Avatar()
            {
	            //vrmUrl = "https://avatar.viverse.com/api/meetingareaselector/v2/newgenavatar/avatars/be13b1bc76b03d90dae55902820c31258be58ab6fae355906525f5ca70b1d4b1f3/files?filetype=model&lod=original",
	            vrmUrl = "https://avatar.viverse.com/api/meetingareaselector/v2/newgenavatar/avatars/70b26fad0dcd7311562fb697b0c1c8938cf41dd3edfe9c098dc7c45d5525ca0cd414/files?filetype=model&lod=basic",
            });
#endif
	        if (!Application.isEditor)
	        {
		        // Load personal avatars
		        ViverseResult<AvatarListWrapper> personalResult = await mainUI.GetViverseCore().AvatarService.GetAvatarList();
		        if (personalResult.IsSuccess && personalResult.Data.avatars != null)
		        {
			        allAvatars.AddRange(personalResult.Data.avatars);
		        }

		        // Load public avatars
		        ViverseResult<AvatarListWrapper> publicResult = await mainUI.GetViverseCore().AvatarService.GetPublicAvatarList();
		        if (publicResult.IsSuccess && publicResult.Data.avatars != null)
		        {
			        allAvatars.AddRange(publicResult.Data.avatars);
		        }
	        }

            if (allAvatars.Count == 0)
            {
                mainUI.ShowError("No avatars found to cycle through");
                return;
            }

            // Set the avatars in the cycler
            _avatarCycler.SetAvatars(allAvatars);

            // Start cycling
            _avatarCycler.StartCycling();
            _cycleAvatarsButton.text = "Stop Cycling";
        }
        catch (Exception e)
        {
	        Debug.LogException(e);
            mainUI.ShowError($"Error starting avatar cycling: {e.Message}");
        }
        finally
        {
            mainUI.SetLoading(false);
        }
    }

    private void UpdateCyclerStatus(string status)
    {
        if (_cycleStatusLabel != null)
        {
            _cycleStatusLabel.text = status;
        }
        else
        {
            Debug.Log($"Cycle status: {status}");
        }
    }

    public void Cleanup()
    {
        if (_avatarCycler != null)
        {
            _avatarCycler.StopCycling();
            Destroy(_avatarCycler.gameObject);
        }

        _avatarUI.Cleanup();
    }
}

#endif
