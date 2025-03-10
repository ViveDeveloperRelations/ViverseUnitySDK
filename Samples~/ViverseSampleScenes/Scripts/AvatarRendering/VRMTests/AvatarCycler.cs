#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ViverseWebGLAPI;
using Avatar = ViverseWebGLAPI.Avatar;

public class AvatarCycler : MonoBehaviour
{
    private List<Avatar> _avatars = new List<Avatar>();
    private int _currentIndex = -1;
    private bool _isCycling = false;
    private float _displayDuration = 5f;
    private float _nextSwitchTime;

    // Reference to the avatar preview UI
    private ViverseAvatarTestUI _avatarUI;

    // Events
    public event Action<string> OnStatusChange;
    public event Action<Avatar> OnAvatarChanged;

    public bool IsCycling => _isCycling;

    public void Initialize(ViverseAvatarTestUI avatarUI, float displayDuration = 5f)
    {
        _avatarUI = avatarUI;
        _displayDuration = displayDuration;
    }

    public void SetAvatars(IEnumerable<Avatar> avatars)
    {
        _avatars.Clear();

        foreach (Avatar avatar in avatars)
        {
            if (avatar != null && !string.IsNullOrEmpty(avatar.vrmUrl))
            {
                _avatars.Add(avatar);
            }
        }

        OnStatusChange?.Invoke($"Loaded {_avatars.Count} avatars for cycling");
    }

    public void SetDisplayDuration(float seconds)
    {
        _displayDuration = Mathf.Max(1f, seconds);
    }

    public void StartCycling()
    {
        if (_isCycling || _avatars.Count == 0) return;

        _isCycling = true;
        _currentIndex = -1;
        _nextSwitchTime = Time.time;

        OnStatusChange?.Invoke("Starting avatar cycling");
    }

    public void StopCycling()
    {
        if (!_isCycling) return;

        _isCycling = false;
        OnStatusChange?.Invoke("Stopped avatar cycling");
    }

    void Update()
    {
        if (!_isCycling || _avatars.Count == 0) return;

        if (Time.time >= _nextSwitchTime)
        {
            LoadNextAvatar();
        }
    }

    private async void LoadNextAvatar()
    {
        // Schedule next switch
        _nextSwitchTime = Time.time + _displayDuration;

        // Move to next avatar
        _currentIndex = (_currentIndex + 1) % _avatars.Count;
        Avatar currentAvatar = _avatars[_currentIndex];

        OnStatusChange?.Invoke($"Loading avatar {_currentIndex + 1}/{_avatars.Count}: {currentAvatar.id}");
        OnAvatarChanged?.Invoke(currentAvatar);

        try
        {
            // Load the avatar in the preview UI
            var (success, message) = await _avatarUI.PreviewVRM(currentAvatar.vrmUrl);

            if (success)
            {
                // Play a random animation
                PlayRandomAnimation();
            }
            else
            {
                Debug.LogWarning($"Failed to load avatar {currentAvatar.id}: {message}");
                OnStatusChange?.Invoke($"Failed to load avatar: {message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading avatar {currentAvatar.id}: {e.Message}");
            OnStatusChange?.Invoke($"Error: {e.Message}");
        }
    }

    private void PlayRandomAnimation()
    {
        if (_avatarUI == null) return;

        // Get available animations from the UI
        string[] availableAnimations = _avatarUI.GetAvailableAnimations();

        if (availableAnimations == null || availableAnimations.Length == 0) return;

        // Pick a random animation
        int randomIndex = UnityEngine.Random.Range(0, availableAnimations.Length);
        string animationName = availableAnimations[randomIndex];

        // Play it
        _avatarUI.PlayAnimation(animationName);
    }
}
#endif
