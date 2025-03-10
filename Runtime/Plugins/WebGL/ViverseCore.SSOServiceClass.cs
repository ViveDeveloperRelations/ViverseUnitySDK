using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ViverseWebGLAPI
{
	public partial class ViverseCore
	{
		/// <summary>
		/// Main class for handling SSO and SSL (logout) functions
		/// </summary>
		public class SSOServiceClass
		{
			[DllImport("__Internal")]
			private static extern bool SSO_InitializeClient(string clientId, string domain, string cookieDomain);

			[DllImport("__Internal")]
			private static extern void SSO_LoginWithRedirect(string redirectUrl, int taskId, Action<string> callback);
			[DllImport("__Internal")]
			private static extern void SSO_Logout(string redirectUrl, int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void SSO_HandleCallback(int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void SSO_GetAccessToken(int taskId, Action<string> callback);

			private readonly SSODomain _ssoDomain;

			public SSOServiceClass(SSODomain ssoDomain)
			{
				if (ssoDomain == null || string.IsNullOrEmpty(ssoDomain.SSODomainString))
					throw new ArgumentException(nameof(ssoDomain.SSODomainString) + " must not be empty");
				_ssoDomain = ssoDomain;
			}

			public bool Initialize(string clientId, string cookieDomain = null)
			{
				if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException(nameof(clientId));
				return SSO_InitializeClient(clientId, _ssoDomain.SSODomainString, cookieDomain);
			}

			public async Task<ViverseResult<AccessTokenResult>> GetAccessToken()
			{
				var result = await CallNativeViverseFunction(SSO_GetAccessToken);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning(
						$"Failed to get access token: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
					return ViverseResult<AccessTokenResult>.Failure(result);
				}

				try
				{
					var accessTokenResult = JsonUtility.FromJson<AccessTokenResult>(result.Payload);
					return ViverseResult<AccessTokenResult>.Success(accessTokenResult, result);
				}
				catch (Exception e)
				{
					Debug.LogError($"Failed to parse access token result: {e.Message}");
					return ViverseResult<AccessTokenResult>.Failure(result);
				}
			}

			public async Task<ViverseResult<LoginResult>> LoginWithRedirect(URLUtils.URLParts currentUrl)
			{
				if (currentUrl == null)
				{
					var invalidParamResult = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
						Message = "Current URL is null"
					};
					return ViverseResult<LoginResult>.Failure(invalidParamResult);
				}

				var redirectUrl = currentUrl.ReconstructURL();
				Debug.Log($"LoginWithRedirect URL: {redirectUrl}");

				void LoginWrapper(int taskId, Action<string> callback)
				{
					SSO_LoginWithRedirect(redirectUrl, taskId, callback);
				}

				var result = await CallNativeViverseFunction(LoginWrapper);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning(
						$"Failed to login with redirect: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
					return ViverseResult<LoginResult>.Failure(result);
				}

				try
				{
					var loginResult = JsonUtility.FromJson<LoginResult>(result.Payload);
					return ViverseResult<LoginResult>.Success(loginResult, result);
				}
				catch (Exception e)
				{
					Debug.LogError($"Failed to parse login result: {e.Message}");
					return ViverseResult<LoginResult>.Failure(result);
				}
			}

			public async Task<ViverseResult<bool>> Logout(URLUtils.URLParts currentUrl)
			{
				if (currentUrl == null)
				{
					var invalidParamResult = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
						Message = "Current URL is null"
					};
					return ViverseResult<bool>.Failure(invalidParamResult);
				}

				var redirectUrl = currentUrl.ReconstructURL();
				Debug.Log($"Logout redirect URL: {redirectUrl}");

				void LogoutWrapper(int taskId, Action<string> callback)
				{
					SSO_Logout(redirectUrl, taskId, callback);
				}

				var result = await CallNativeViverseFunction(LogoutWrapper);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning($"Failed to logout: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
					return ViverseResult<bool>.Failure(result);
				}

				return ViverseResult<bool>.Success(true, result);
			}

			public async Task<ViverseResult<LoginResult>> HandleCallback()
			{
				var result = await CallNativeViverseFunction(SSO_HandleCallback);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					Debug.LogWarning(
						$"Failed to handle callback: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
					return ViverseResult<LoginResult>.Failure(result);
				}

				try
				{
					var loginResult = JsonUtility.FromJson<LoginResult>(result.Payload);
					return ViverseResult<LoginResult>.Success(loginResult, result);
				}
				catch (Exception e)
				{
					Debug.LogError($"Failed to parse login result: {e.Message}");
					return ViverseResult<LoginResult>.Failure(result);
				}
			}
		}
	}
}
