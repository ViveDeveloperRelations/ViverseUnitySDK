using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using ViverseWebGLAPI;

/// <summary>
/// Extension class to handle achievement functionality for the VIVERSE SDK
/// </summary>
public class ViverseAchievementExtension : MonoBehaviour
{
    private ViverseTestUIDocument _parent;
    private VisualElement _rootElement;

    // UI Elements
    private VisualElement _achievementContainer;
    private TextField _achievementNameInput;
    private Button _getAchievementsButton;
    private Button _unlockAchievementButton;
    private TextField _achievementResult;

    // Achievement data
    private List<UserAchievementInfo> _achievements = new List<UserAchievementInfo>();

    /// <summary>
    /// Initialize the achievement extension with parent references
    /// </summary>
    public void Initialize(ViverseTestUIDocument parent, VisualElement rootElement)
    {
        _parent = parent;
        _rootElement = rootElement;

        // Create and add UI elements to the existing UI
        CreateAchievementSection();
        BindEventHandlers();
        AddInlineStyles(); // Using inline styles instead of StyleSheet
    }

    /// <summary>
    /// Create UI elements for the achievement section
    /// </summary>
    private void CreateAchievementSection()
    {
        // Get the container that holds all cards
        var container = _rootElement.Q(className: "container");

        // Find all existing card sections
        var allCards = container.Query(className: "card").ToList();

        // Create achievement section card
        var achievementSection = new VisualElement();
        achievementSection.AddToClassList("card");

        // Add title
        var title = new Label("Achievement Service");
        title.AddToClassList("card-title");
        achievementSection.Add(title);

        // Add App ID label (reuse the one from leaderboard)
        var appIdLabel = new Label("App ID (same as Leaderboard):");
        appIdLabel.AddToClassList("section-label");
        achievementSection.Add(appIdLabel);

        // Achievement input section
        var inputContainer = new VisualElement();
        inputContainer.style.marginBottom = 10;

        _achievementNameInput = new TextField("Achievement Name:");
        _achievementNameInput.name = "achievement-name-input";
        inputContainer.Add(_achievementNameInput);

        // Buttons container
        var buttonRow = new VisualElement();
        buttonRow.AddToClassList("button-row");

        _unlockAchievementButton = new Button(() => UnlockAchievement());
        _unlockAchievementButton.text = "Unlock Achievement";
        _unlockAchievementButton.name = "unlock-achievement-button";
        buttonRow.Add(_unlockAchievementButton);

        _getAchievementsButton = new Button(() => GetAchievements());
        _getAchievementsButton.text = "Get Achievements";
        _getAchievementsButton.name = "get-achievements-button";
        buttonRow.Add(_getAchievementsButton);

        inputContainer.Add(buttonRow);
        achievementSection.Add(inputContainer);

        // Results section
        var resultsLabel = new Label("Results:");
        resultsLabel.AddToClassList("section-label");
        achievementSection.Add(resultsLabel);

        _achievementResult = new TextField();
        _achievementResult.multiline = true;
        _achievementResult.isReadOnly = true;
        _achievementResult.AddToClassList("result-field");
        _achievementResult.name = "achievement-result";
        achievementSection.Add(_achievementResult);

        // Achievement list container
        var achievementsLabel = new Label("Achievements:");
        achievementsLabel.AddToClassList("section-label");
        achievementSection.Add(achievementsLabel);

        _achievementContainer = new VisualElement();
        _achievementContainer.name = "achievement-container";
        _achievementContainer.AddToClassList("achievement-grid");
        achievementSection.Add(_achievementContainer);

        // Insert the achievement section at the end of the container
        container.Add(achievementSection);
    }

    /// <summary>
    /// Add styling for achievement UI elements using inline styles
    /// instead of creating a new StyleSheet
    /// </summary>
    private void AddInlineStyles()
    {
        // Achievement grid
        if (_achievementContainer != null)
        {
            _achievementContainer.style.display = DisplayStyle.Flex;
            _achievementContainer.style.flexDirection = FlexDirection.Column;
            _achievementContainer.style.marginTop = 10;
        }

        // Style for the achievement result text field
        if (_achievementResult != null)
        {
            _achievementResult.style.minHeight = 100;
        }

        // Note: The rest of the styles will be applied on a per-item basis
        // when elements are added to the container in UpdateAchievementUI
    }

    /// <summary>
    /// Apply styles to an achievement item
    /// </summary>
    private void ApplyAchievementItemStyles(VisualElement achievementItem)
    {
        // Achievement item
        achievementItem.style.display = DisplayStyle.Flex;
        achievementItem.style.flexDirection = FlexDirection.Row;
        achievementItem.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        // Set border radius for each corner individually
        achievementItem.style.borderTopLeftRadius = 4;
        achievementItem.style.borderTopRightRadius = 4;
        achievementItem.style.borderBottomLeftRadius = 4;
        achievementItem.style.borderBottomRightRadius = 4;
        // Set padding for each side
        achievementItem.style.paddingTop = 8;
        achievementItem.style.paddingRight = 8;
        achievementItem.style.paddingBottom = 8;
        achievementItem.style.paddingLeft = 8;
        achievementItem.style.marginBottom = 8;
        achievementItem.style.alignItems = Align.Center;

        // Apply styles to child elements
        var iconElement = achievementItem.Q(className: "achievement-icon");
        if (iconElement != null)
        {
            iconElement.style.width = 32;
            iconElement.style.height = 32;
            iconElement.style.marginRight = 8;
            iconElement.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            // Set border radius for each corner individually
            iconElement.style.borderTopLeftRadius = 4;
            iconElement.style.borderTopRightRadius = 4;
            iconElement.style.borderBottomLeftRadius = 4;
            iconElement.style.borderBottomRightRadius = 4;
        }

        var infoContainer = achievementItem.Q(className: "achievement-info");
        if (infoContainer != null)
        {
            infoContainer.style.flexGrow = 1;
        }

        var titleLabel = achievementItem.Q(className: "achievement-title");
        if (titleLabel != null)
        {
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        var descriptionLabel = achievementItem.Q(className: "achievement-description");
        if (descriptionLabel != null)
        {
            descriptionLabel.style.fontSize = 12;
            descriptionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        }

        var unlockedLabel = achievementItem.Q(className: "achievement-unlocked");
        if (unlockedLabel != null)
        {
            unlockedLabel.style.color = new Color(0.2f, 0.8f, 0.2f);
        }

        var lockedLabel = achievementItem.Q(className: "achievement-locked");
        if (lockedLabel != null)
        {
            lockedLabel.style.color = new Color(0.8f, 0.2f, 0.2f);
        }
    }

    /// <summary>
    /// Bind event handlers to UI elements
    /// </summary>
    private void BindEventHandlers()
    {
        _unlockAchievementButton.SetEnabled(false);
        _getAchievementsButton.SetEnabled(false);
    }

    /// <summary>
    /// Called to update the UI when the login state changes
    /// </summary>
    public void UpdateLoginState(bool isLoggedIn)
    {
        _unlockAchievementButton.SetEnabled(isLoggedIn);
        _getAchievementsButton.SetEnabled(isLoggedIn);

        if (!isLoggedIn)
        {
            // Clear achievements when logging out
            _achievementContainer.Clear();
            _achievementResult.value = "";
            _achievements.Clear();
        }
    }

    /// <summary>
    /// Get achievements from the API
    /// </summary>
    private async void GetAchievements()
    {
        ViverseCore core = _parent.GetViverseCore();
        if (core == null) return;

        string appId = _parent.GetAppId();
        if (string.IsNullOrEmpty(appId))
        {
            _parent.ShowError("Please enter an App ID");
            return;
        }

        _parent.SetLoading(true, "Loading achievements...");

        try
        {
            ViverseResult<UserAchievementResult> result = await core.LeaderboardService.GetUserAchievement(appId);

            if (result.IsSuccess)
            {
                // Update result field with raw data
                _achievementResult.value = JsonUtility.ToJson(result.Data, true);

                // Update achievements list
                _achievements.Clear();
                if (result.Data?.achievements != null)
                {
                    _achievements.AddRange(result.Data.achievements);
                }

                // Update UI
                UpdateAchievementUI();

                _parent.ShowMessage($"Loaded {_achievements.Count} achievements");
            }
            else
            {
                _parent.ShowError($"Failed to get achievements: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            _parent.ShowError($"Error loading achievements: {e.Message}");
            Debug.LogException(e);
        }
        finally
        {
            _parent.SetLoading(false);
        }
    }

    /// <summary>
    /// Unlock an achievement
    /// </summary>
    private async void UnlockAchievement()
    {
        ViverseCore core = _parent.GetViverseCore();
        if (core == null) return;

        string appId = _parent.GetAppId();
        if (string.IsNullOrEmpty(appId))
        {
            _parent.ShowError("Please enter an App ID");
            return;
        }

        string achievementName = _achievementNameInput.value;
        if (string.IsNullOrEmpty(achievementName))
        {
            _parent.ShowError("Please enter an achievement name");
            return;
        }

        _parent.SetLoading(true, $"Unlocking achievement {achievementName}...");

        try
        {
            // Create achievement list with a single achievement to unlock
            var achievements = new List<Achievement>
            {
                new Achievement
                {
                    api_name = achievementName,
                    unlock = true
                }
            };

            // Call the API
            ViverseResult<AchievementUploadResult> result =
                await core.LeaderboardService.UploadUserAchievement(appId, achievements);

            if (result.IsSuccess)
            {
                // Update result field with raw data
                _achievementResult.value = JsonUtility.ToJson(result.Data, true);

                int successCount = result.Data?.success?.total ?? 0;
                int failureCount = result.Data?.failure?.total ?? 0;

                if (successCount > 0)
                {
                    _parent.ShowMessage($"Achievement '{achievementName}' unlocked successfully!");

                    // Refresh achievements to update UI
                    GetAchievements();
                }
                else if (failureCount > 0 && result.Data?.failure?.achievements?.Length > 0)
                {
                    string errorMsg = result.Data.failure.achievements[0].message ?? "Unknown error";
                    _parent.ShowError($"Failed to unlock achievement: {errorMsg}");
                }
                else
                {
                    _parent.ShowError("Achievement unlock returned unknown result");
                }
            }
            else
            {
                _parent.ShowError($"Failed to unlock achievement: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            _parent.ShowError($"Error unlocking achievement: {e.Message}");
            Debug.LogException(e);
        }
        finally
        {
            _parent.SetLoading(false);
        }
    }

    /// <summary>
    /// Update the achievement UI with current achievement data
    /// </summary>
    private void UpdateAchievementUI()
    {
        _achievementContainer.Clear();

        // If no achievements, show a message
        if (_achievements.Count == 0)
        {
            var noAchievementsLabel = new Label("No achievements found");
            _achievementContainer.Add(noAchievementsLabel);
            return;
        }

        // Create UI elements for each achievement
        foreach (var achievement in _achievements)
        {
            var achievementItem = new VisualElement();
            achievementItem.AddToClassList("achievement-item");

            // Icon placeholder
            var iconElement = new VisualElement();
            iconElement.AddToClassList("achievement-icon");
            achievementItem.Add(iconElement);

            // Achievement info container
            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("achievement-info");

            // Title with status
            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;

            var titleLabel = new Label(achievement.display_name);
            titleLabel.AddToClassList("achievement-title");
            titleContainer.Add(titleLabel);

            // Status indicator
            var statusLabel = new Label(achievement.is_achieved ? " ✓" : " ×");
            statusLabel.AddToClassList(achievement.is_achieved ? "achievement-unlocked" : "achievement-locked");
            titleContainer.Add(statusLabel);

            infoContainer.Add(titleContainer);

            // Description
            if (!string.IsNullOrEmpty(achievement.description))
            {
                var descriptionLabel = new Label(achievement.description);
                descriptionLabel.AddToClassList("achievement-description");
                infoContainer.Add(descriptionLabel);
            }

            // API name
            var apiNameLabel = new Label($"API Name: {achievement.api_name}");
            apiNameLabel.style.fontSize = 10;
            apiNameLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            infoContainer.Add(apiNameLabel);

            // If the achievement is not achieved, add an unlock button
            if (!achievement.is_achieved)
            {
                var unlockButton = new Button(() => {
                    _achievementNameInput.value = achievement.api_name;
                    UnlockAchievement();
                });
                unlockButton.text = "Unlock";
                unlockButton.style.alignSelf = Align.FlexEnd;
                infoContainer.Add(unlockButton);
            }
            else if (achievement.achieved_times > 1)
            {
                // If unlocked multiple times, show the count
                var countLabel = new Label($"Unlocked {achievement.achieved_times} times");
                countLabel.style.fontSize = 10;
                countLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
                infoContainer.Add(countLabel);
            }

            achievementItem.Add(infoContainer);

            // Apply styles to this achievement item
            ApplyAchievementItemStyles(achievementItem);

            _achievementContainer.Add(achievementItem);
        }
    }

    /// <summary>
    /// Clean up when the component is destroyed
    /// </summary>
    public void Cleanup()
    {
        _achievementContainer?.Clear();
        _achievements?.Clear();
    }
}
