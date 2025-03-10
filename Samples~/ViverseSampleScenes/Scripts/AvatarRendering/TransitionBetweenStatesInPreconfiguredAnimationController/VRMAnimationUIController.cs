#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class VRMAnimationUIController : MonoBehaviour
{
    [SerializeField]
    private UIDocument uiDocument;
    [SerializeField]
    private VRMAnimatorController vrmAnimator;

    private VisualElement root;
    private ScrollView animationList;
    private Label statusLabel;
    private Label currentAnimationName;
    private List<Button> animationButtons = new List<Button>();
    private string currentState = "";

    private void OnEnable()
    {
        InitializeUI();
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void InitializeUI()
    {
        root = uiDocument.rootVisualElement.Q("root-container");
        animationList = root.Q<ScrollView>("animation-list");
        statusLabel = root.Q<Label>("status-label");
        currentAnimationName = root.Q<Label>("current-animation-name");

        // Setup ScrollView
        animationList.verticalScrollerVisibility = ScrollerVisibility.Auto;
        animationList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        animationList.style.display = DisplayStyle.None;
    }

    private void SubscribeToEvents()
    {
        if (vrmAnimator != null)
        {
            if (vrmAnimator.IsInitialized)
            {
                HandleAnimationStatesLoaded(vrmAnimator.GetAvailableStates());
            }
            else
            {
                vrmAnimator.OnAnimationStatesLoaded += HandleAnimationStatesLoaded;
                vrmAnimator.OnAnimationStateChanged += HandleAnimationStateChanged;
            }
        }
        else
        {
            Debug.LogError("VRMAnimatorController reference is missing!");
            statusLabel.text = "Error: Animation controller not found";
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (vrmAnimator == null) return;
        vrmAnimator.OnAnimationStatesLoaded -= HandleAnimationStatesLoaded;
        vrmAnimator.OnAnimationStateChanged -= HandleAnimationStateChanged;
    }

    private void HandleAnimationStatesLoaded(string[] states)
    {
        if (states == null || states.Length == 0)
        {
            statusLabel.text = "No animation states found";
            return;
        }

        statusLabel.text = "VRM Loaded Successfully";
        animationList.style.display = DisplayStyle.Flex;
        CreateAnimationButtons(states);

        if (states.Length > 0)
        {
            currentState = states[0];
            currentAnimationName.text = currentState;
        }
    }

    private void HandleAnimationStateChanged(string newState)
    {
        currentState = newState;
        currentAnimationName.text = newState;
        UpdateButtonStates(newState);
    }

    private void CreateAnimationButtons(string[] states)
    {
        // Clear existing content
        animationList.Clear();
        animationButtons.Clear();

        // Create a container for the buttons
        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Column;
        buttonContainer.style.width = new StyleLength(new Length(100, LengthUnit.Percent));

        foreach (string state in states)
        {
            Button button = new Button(() => OnAnimationButtonClicked(state))
            {
                text = state,
                name = $"button-{state}"
            };

            button.AddToClassList("animation-button");
            buttonContainer.Add(button);
            animationButtons.Add(button);
        }

        // Add the container to the ScrollView
        animationList.Add(buttonContainer);

        // Update initial button states
        UpdateButtonStates(currentState);
    }

    private void OnAnimationButtonClicked(string stateName)
    {
        if (vrmAnimator != null)
        {
            vrmAnimator.TransitionToState(stateName);
        }
        else
        {
            Debug.LogWarning("VRMAnimatorController is not available");
        }
    }

    private void UpdateButtonStates(string activeState)
    {
        foreach (var button in animationButtons)
        {
            if (button.name == $"button-{activeState}")
            {
                button.AddToClassList("selected");
            }
            else
            {
                button.RemoveFromClassList("selected");
            }
        }
    }

    public void RefreshUI()
    {
        if (vrmAnimator != null)
        {
            var states = vrmAnimator.GetAvailableStates();
            HandleAnimationStatesLoaded(states);
        }
    }

    public void HandleError(string errorMessage)
    {
        statusLabel.text = $"Error: {errorMessage}";
        animationList.style.display = DisplayStyle.None;
        currentAnimationName.text = "None";
    }
}
#endif
