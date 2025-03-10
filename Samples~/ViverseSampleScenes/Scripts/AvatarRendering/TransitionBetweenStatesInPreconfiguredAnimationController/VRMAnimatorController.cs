#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public class VRMAnimatorController : MonoBehaviour
{
	[SerializeField] private RuntimeAnimatorController animatorController;
	[SerializeField] private string vrmUrl;

	private GameObject loadedModel;
	private Animator animator;
	private Dictionary<string, int> stateHashes = new Dictionary<string, int>();

	public event Action<string[]> OnAnimationStatesLoaded;
	public event Action<string> OnAnimationStateChanged;
	public bool IsInitialized { get; private set; }

	async void Start()
	{
		if (string.IsNullOrEmpty(vrmUrl))
		{
			Debug.LogError("VRM URL not set!");
			return;
		}

		try
		{
			await LoadVRMAndSetupAnimator();
			CacheAnimationStates();
			OnAnimationStatesLoaded?.Invoke(GetAvailableStates());
			IsInitialized = true;
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to initialize VRM: {e.Message}");
		}
	}

	private async Task LoadVRMAndSetupAnimator()
	{
		try
		{
			loadedModel = await SampleVRMUtility.DownloadAndLoadVRM(vrmUrl);
			animator = SampleVRMUtility.SetupAnimator(loadedModel, animatorController);
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to load VRM or setup animator: {e.Message}");
			throw;
		}
	}

	private void CacheAnimationStates()
	{
		if (animator == null) return;

		foreach (AnimatorControllerParameter param in animator.parameters)
		{
			if (param.type == AnimatorControllerParameterType.Trigger)
			{
				stateHashes[param.name] = Animator.StringToHash(param.name);
			}
		}
	}

	public void TransitionToState(string stateName)
	{
		if (animator == null) return;

		foreach (int hashStateValue in stateHashes.Values)
		{
			animator.ResetTrigger(hashStateValue);
		}

		if (stateHashes.TryGetValue(stateName, out int hash))
		{
			animator.SetTrigger(hash);
			OnAnimationStateChanged?.Invoke(stateName);
		}
		else
		{
			Debug.LogWarning($"Animation state {stateName} not found in controller");
		}
	}

	public string[] GetAvailableStates()
	{
		string[] states = new string[stateHashes.Count];
		stateHashes.Keys.CopyTo(states, 0);
		return states;
	}

	public string GetCurrentState()
	{
		if (animator == null) return string.Empty;

		AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
		foreach (string state in stateHashes.Keys)
		{
			if (currentState.IsName(state))
				return state;
		}

		return string.Empty;
	}
}
#endif //#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
