using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace ViverseWebGLAPI
{
	/// <summary>
	/// Internal wrapper class for JSON parsing - Unity JsonUtility cannot parse top-level arrays
	/// </summary>
	[Serializable]
	internal class InternalAvatarListWrapper
	{
		[Preserve] public Avatar[] avatars;
	}
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
			[DllImport("__Internal")]
			private static extern void Avatar_GetActiveAvatar(int taskId, Action<string> callback);
			[DllImport("__Internal")]
			private static extern void Avatar_GetPublicAvatarByID(string avatarId, int taskId, Action<string> callback);

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
					// ✅ Use safe logging extension for comprehensive error reporting
					var failureResult = ViverseResult<UserProfile>.Failure(result);
					failureResult.LogError("Get Profile");
					return failureResult;
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

			public async Task<ViverseResult<Avatar[]>> GetAvatarList()
			{
				ViverseSDKReturn result = await CallNativeViverseFunction(Avatar_GetAvatarList);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					// ✅ Use safe logging extension for comprehensive error reporting
					var failureResult = ViverseResult<Avatar[]>.Failure(result);
					failureResult.LogError("Get Avatar List");
					return failureResult;
				}
				try
				{
					//Debug.Log($"Avatar list result payload: {result.Payload}");
					//json workaround since the result is a json array, but the json utility can't parse it directly
					if (result.Payload.TrimStart().StartsWith("["))
					{
						result.Payload = "{\"avatars\":" + result.Payload + "}";
					}
					
					// Use internal wrapper class for JSON parsing only
					var avatarListWrapper = JsonUtility.FromJson<InternalAvatarListWrapper>(result.Payload);
					//WORKAROUND: remove null entries and entries with empty vrmUrl - seems to happen in some situations
					Avatar[] avatars = avatarListWrapper.avatars
						.Where(avatar => avatar != null && !string.IsNullOrEmpty(avatar.vrmUrl))
						.ToArray();
					return ViverseResult<Avatar[]>.Success(avatars, result);
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
					var exceptionResult = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
						Message = e.Message
					};
					// ✅ Use safe logging extension for comprehensive error reporting
					var failureResult = ViverseResult<byte[]>.Failure(exceptionResult);
					failureResult.LogError("Download VRM");
					return failureResult;
				}
			}

			public async Task<ViverseResult<Avatar[]>> GetPublicAvatarList()
			{
				ViverseSDKReturn result = await CallNativeViverseFunction(Avatar_GetPublicAvatarList);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					// ✅ Use safe logging extension for comprehensive error reporting
					var failureResult = ViverseResult<Avatar[]>.Failure(result);
					failureResult.LogError("Get Public Avatar List");
					return failureResult;
				}

				try
				{
					//Debug.Log($"Public avatar list result payload: {result.Payload}");
					//json workaround since the result is a json array, but the json utility can't parse it directly
					if (result.Payload.TrimStart().StartsWith("["))
					{
						result.Payload = "{\"avatars\":" + result.Payload + "}";
					}

					// Use internal wrapper class for JSON parsing only
					var avatarListWrapper = JsonUtility.FromJson<InternalAvatarListWrapper>(result.Payload);

					//WORKAROUND: remove null entries and entries with empty vrmUrl - seems to happen in some situations
					Avatar[] avatars = avatarListWrapper.avatars
						.Where(avatar => avatar != null && !string.IsNullOrEmpty(avatar.vrmUrl))
						.ToArray();

					return ViverseResult<Avatar[]>.Success(avatars, result);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					throw new Exception($"Failed to parse get public avatar list result. Error: {e.Message} un-parsed payload {JsonUtility.ToJson(result)}", e);
				}
			}

			public async Task<ViverseResult<Avatar>> GetActiveAvatar()
			{
				ViverseSDKReturn result = await CallNativeViverseFunction(Avatar_GetActiveAvatar);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					// ✅ Use safe logging extension for comprehensive error reporting
					var failureResult = ViverseResult<Avatar>.Failure(result);
					failureResult.LogError("Get Active Avatar");
					return failureResult;
				}

				try
				{
					// Handle null case (no active avatar)
					if (string.IsNullOrEmpty(result.Payload) || result.Payload.Trim() == "null")
					{
						return ViverseResult<Avatar>.Success(null, result);
					}

					Avatar activeAvatar = JsonUtility.FromJson<Avatar>(result.Payload);
					return ViverseResult<Avatar>.Success(activeAvatar, result);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					throw new Exception($"Failed to parse get active avatar result. Error: {e.Message} un-parsed payload {JsonUtility.ToJson(result)}", e);
				}
			}

			public async Task<ViverseResult<Avatar>> GetPublicAvatarByID(string avatarId)
			{
				if (string.IsNullOrEmpty(avatarId))
				{
					var invalidParamResult = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
						Message = "Avatar ID cannot be null or empty"
					};
					return ViverseResult<Avatar>.Failure(invalidParamResult);
				}

				void GetPublicAvatarByIDWrapper(int taskId, Action<string> callback)
				{
					Avatar_GetPublicAvatarByID(avatarId, taskId, callback);
				}

				ViverseSDKReturn result = await CallNativeViverseFunction(GetPublicAvatarByIDWrapper);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning($"Failed to get public avatar by ID. Error code: {result.ReturnCode} info:" +
					                 ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode) + " payload:" +
					                 JsonUtility.ToJson(result));
					return ViverseResult<Avatar>.Failure(result);
				}

				try
				{
					// Handle null case (avatar not found)
					if (string.IsNullOrEmpty(result.Payload) || result.Payload.Trim() == "null")
					{
						return ViverseResult<Avatar>.Success(null, result);
					}

					Avatar avatar = JsonUtility.FromJson<Avatar>(result.Payload);
					return ViverseResult<Avatar>.Success(avatar, result);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					throw new Exception($"Failed to parse get public avatar by ID result. Error: {e.Message} un-parsed payload {JsonUtility.ToJson(result)}", e);
				}
			}
		}
	}
}
