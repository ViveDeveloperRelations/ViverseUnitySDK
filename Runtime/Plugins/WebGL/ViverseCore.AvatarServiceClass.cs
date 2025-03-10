using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ViverseWebGLAPI
{
	public partial class ViverseCore
	{
		/// <summary>
		/// Avatar related viverse functions, wrap and manage the sdk avatar functions
		/// </summary>
		public class AvatarServiceClass
		{
			[DllImport("__Internal")]
			private static extern void ViverseSDK_SetAvatarHost(string host);
			[DllImport("__Internal")]
			private static extern void Avatar_Initialize(int taskId, Action<string> callback);
			[DllImport("__Internal")]
			private static extern void Avatar_GetProfile(int taskId, Action<string> callback);
			[DllImport("__Internal")]
			private static extern void Avatar_GetAvatarList(int taskId, Action<string> callback);
			[DllImport("__Internal")]
			private static extern void Avatar_GetPublicAvatarList(int taskId, Action<string> callback);

			private void ConfigureAvatarHost(AvatarHost host) => ViverseSDK_SetAvatarHost(host.HostString);

			private readonly AvatarHost _avatarHost;

			public AvatarServiceClass(AvatarHost host)
			{
				if(host == null || string.IsNullOrEmpty(host.HostString)) throw new ArgumentException("Host string must not be empty");
				_avatarHost = host;
				ConfigureAvatarHost(host);
			}

			public async Task<ViverseResult<bool>> Initialize()
			{
				ViverseSDKReturn result = await CallNativeViverseFunction(Avatar_Initialize);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning($"Failed to upload score ViverseSDK. Error code: {result.ReturnCode} info:" +
					                 ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode) + " payload:" +
					                 JsonUtility.ToJson(result));
					return ViverseResult<bool>.Failure(result);
				}
				return ViverseResult<bool>.Success(true, result);
			}

			public async Task<ViverseResult<UserProfile>> GetProfile()
			{
				ViverseSDKReturn result = await CallNativeViverseFunction(Avatar_GetProfile);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning($"Failed to upload get profile. Error code: {result.ReturnCode} info:" +
					                 ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode) + " payload:" +
					                 JsonUtility.ToJson(result));
					return ViverseResult<UserProfile>.Failure(result);
				}
				try
				{
					return ViverseResult<UserProfile>.Success(
						JsonUtility.FromJson<UserProfile>(result.Payload), result);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					throw new Exception($"Failed to parse get profile result. Error: {e.Message} un-parsed payload {JsonUtility.ToJson(result)}", e);
				}
			}

			public async Task<ViverseResult<AvatarListWrapper>> GetAvatarList()
			{
				ViverseSDKReturn result = await CallNativeViverseFunction(Avatar_GetAvatarList);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning($"Failed to get avatar list. Error code: {result.ReturnCode} info:" +
					                 ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode) + " payload:" +
					                 JsonUtility.ToJson(result));
					return ViverseResult<AvatarListWrapper>.Failure(result);
				}
				try
				{
					//Debug.Log($"Avatar list result payload: {result.Payload}");
					//json workaround since the result is a json array, but the json utility can't parse it directly
					if (result.Payload.TrimStart().StartsWith("["))
					{
						result.Payload = "{\"avatars\":" + result.Payload + "}";
					}
					AvatarListWrapper avatarList = JsonUtility.FromJson<AvatarListWrapper>(result.Payload);
					//WORKAROUND: remove null entries and entries with empty vrmUrl - seems to happen in some situations
					avatarList.avatars = avatarList.avatars
						.Where(avatar => avatar != null && !string.IsNullOrEmpty(avatar.vrmUrl))
						.ToArray();
					return ViverseResult<AvatarListWrapper>.Success(avatarList, result);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					throw new Exception($"Failed to parse get avatar list result. Error: {e.Message} un-parsed payload {JsonUtility.ToJson(result)}", e);
				}
			}

			public async Task<ViverseResult<byte[]>> DownloadVRMFile(string url)
			{
				if (string.IsNullOrEmpty(url))
				{
					var invalidParamResult = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
						Message = "URL cannot be null or empty"
					};
					return ViverseResult<byte[]>.Failure(invalidParamResult);
				}

				try
				{
					using (var webRequest = UnityEngine.Networking.UnityWebRequest.Get(url))
					{
						await webRequest.SendWebRequest();

						if (webRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
						{
							var errorResult = new ViverseSDKReturn
							{
								ReturnCode = (int)ViverseSDKReturnCode.ErrorNotFound,
								Message = webRequest.error
							};
							return ViverseResult<byte[]>.Failure(errorResult);
						}

						var successResult = new ViverseSDKReturn
						{
							ReturnCode = (int)ViverseSDKReturnCode.Success,
							Message = "VRM downloaded successfully"
						};
						return ViverseResult<byte[]>.Success(webRequest.downloadHandler.data, successResult);
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"Failed to download VRM: {e.Message}");
					var exceptionResult = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
						Message = e.Message
					};
					return ViverseResult<byte[]>.Failure(exceptionResult);
				}
			}

			public async Task<ViverseResult<AvatarListWrapper>> GetPublicAvatarList()
			{
				ViverseSDKReturn result = await CallNativeViverseFunction(Avatar_GetPublicAvatarList);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning($"Failed to get public avatar list. Error code: {result.ReturnCode} info:" +
					                 ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode) + " payload:" +
					                 JsonUtility.ToJson(result));
					return ViverseResult<AvatarListWrapper>.Failure(result);
				}

				try
				{
					//Debug.Log($"Public avatar list result payload: {result.Payload}");
					//json workaround since the result is a json array, but the json utility can't parse it directly
					if (result.Payload.TrimStart().StartsWith("["))
					{
						result.Payload = "{\"avatars\":" + result.Payload + "}";
					}

					AvatarListWrapper avatarList = JsonUtility.FromJson<AvatarListWrapper>(result.Payload);

					//WORKAROUND: remove null entries and entries with empty vrmUrl - seems to happen in some situations
					avatarList.avatars = avatarList.avatars
						.Where(avatar => avatar != null && !string.IsNullOrEmpty(avatar.vrmUrl))
						.ToArray();

					return ViverseResult<AvatarListWrapper>.Success(avatarList, result);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					throw new Exception($"Failed to parse get public avatar list result. Error: {e.Message} un-parsed payload {JsonUtility.ToJson(result)}", e);
				}
			}
		}
	}
}
