#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
using System;
using UniGLTF;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class VRMDownloaderAndAnimator : MonoBehaviour
{
	private PlayableGraph playableGraph;
	private AnimationClipPlayable clipPlayable;
	private Animator animator;

	public AnimationClip animationClip;
	public string vrmUrl = "https://avatar.viverse.com/api/meetingareaselector/v2/newgenavatar/avatars/be13b1bc76b03d90dae55902820c31258be58ab6fae355906525f5ca70b1d4b1f3/files?filetype=model&lod=original";

	private RuntimeGltfInstance loadedModel;

	async void Start()
	{
		if (animationClip == null)
		{
			Debug.LogError("No animation clip assigned!");
			return;
		}
		CleanupModelIfItExists();
		try
		{
			// Use the shared utility to download and load the VRM
			loadedModel = await SampleVRMUtility.DownloadAndLoadVRM(vrmUrl);
			animator = loadedModel.GetComponent<Animator>();
			if (animator == null)
			{
				Debug.LogError("No Animator component found on VRM!");
				return;
			}
			// Create and setup the playable graph
			playableGraph = PlayableGraph.Create("Runtime Animation");

			// Create the clip playable and set it to loop
			clipPlayable = AnimationClipPlayable.Create(playableGraph, animationClip);

			// Connect the Playable to the output
			AnimationPlayableOutput playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
			playableOutput.SetSourcePlayable(clipPlayable);

			// Start playing
			playableGraph.Play();
		}catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"Failed to load and setup VRM: {e.Message}");
		}
	}

	void CleanupModelIfItExists()
	{
		if (playableGraph.IsValid())
		{
			playableGraph.Destroy();
		}
		if(loadedModel != null)
		{
			Destroy(loadedModel.Root);
		}
	}
	private void OnDestroy()
	{
		CleanupModelIfItExists();
	}
}
#endif //#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
