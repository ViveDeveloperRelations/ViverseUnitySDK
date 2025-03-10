#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.Networking;
using VRM;
using UniGLTF;

public class SampleVRMUtility
{
	public static async Task<GameObject> DownloadAndLoadVRM(string vrmUrl)
	{
		using UnityWebRequest www = UnityWebRequest.Get(vrmUrl);
		UnityWebRequestAsyncOperation asyncOp = www.SendWebRequest();
		while (!asyncOp.isDone)
			await Task.Yield();

		if (www.result != UnityWebRequest.Result.Success)
		{
			throw new Exception($"Failed to download VRM: {www.error}");
		}

		byte[] vrmData = www.downloadHandler.data;

		return await LoadVRMFromBytes(vrmData);
	}

	public static async Task<GameObject> LoadVRMFromBytes(byte[] vrmData)
	{
		GltfData gltfData = null;
		try
		{
			GlbBinaryParser parser = new GlbBinaryParser(vrmData, "LoadedVRM");
			gltfData = parser.Parse();
		}
		catch (Exception e)
		{
			throw new Exception($"Failed to parse VRM data: {e.Message}");
		}

		if (gltfData == null)
		{
			throw new Exception("Failed to parse GLB data: GltfData is null");
		}

		GameObject loadedModel = null;
		RuntimeGltfInstance instance = null;

		using VRMImporterContext context = new VRMImporterContext(new VRMData(gltfData));
		try
		{
			VRMMetaObject meta = context.ReadMeta(true);
			instance = await context.LoadAsync(new ImmediateCaller());

			if (instance == null)
			{
				throw new Exception("Failed to load VRM: Instance is null");
			}

			loadedModel = instance.Root;
			loadedModel.transform.position = Vector3.zero;
			loadedModel.transform.rotation = Quaternion.identity;
			instance.ShowMeshes();
		}
		catch (Exception e)
		{
			if (instance != null)
			{
				UnityEngine.Object.Destroy(instance.gameObject);
			}
			Debug.LogException(e);
			throw new Exception($"Failed to setup VRM model: {e.Message}");
		}

		return loadedModel;
	}

	public static Animator SetupAnimator(GameObject model, RuntimeAnimatorController controller)
	{
		if (model == null) throw new ArgumentNullException(nameof(model));
		if (controller == null) throw new ArgumentNullException(nameof(controller));

		Animator animator = model.GetComponent<Animator>();
		if (animator == null)
		{
			animator = model.AddComponent<Animator>();
		}

		animator.runtimeAnimatorController = controller;

		if (animator.avatar != null) return animator;
		Avatar avatar = AvatarBuilder.BuildGenericAvatar(model, "");
		if (avatar == null) return animator;
		avatar.name = "Runtime Avatar";
		animator.avatar = avatar;

		return animator;
	}
}
#endif //UNI_VRM_INSTALLED
