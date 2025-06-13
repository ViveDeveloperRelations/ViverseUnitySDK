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
			private static extern void SSO_InitializeClientAsync(string clientId, string domain, string cookieDomain, int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void SSO_LoginWithWorlds(string state, int taskId, Action<string> callback);
			[DllImport("__Internal")]
			private static extern void SSO_CheckAuth(int taskId, Action<string> callback);
			[DllImport("__Internal")]
			private static extern void SSO_Logout(string redirectUrl, int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void SSO_GetAccessToken(int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void SSO_DetectAndHandleOAuthCallback(string clientId, string domain, string cookieDomain, int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void SSO_ForceReinitializeClient(string clientId, string domain, string cookieDomain, int taskId, Action<string> callback);

			private readonly SSODomain _ssoDomain;

			public SSOServiceClass(SSODomain ssoDomain)
			{
				if (ssoDomain == null || string.IsNullOrEmpty(ssoDomain.SSODomainString))
					throw new ArgumentException(nameof(ssoDomain.SSODomainString) + " must not be empty");
				_ssoDomain = ssoDomain;
			}

			public async Task<bool> Initialize(string clientId, string cookieDomain = null)
			{
				if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException(nameof(clientId));
				
				var result = await ViverseAsyncHelperLib.WrapAsyncWithPayload(
					SSO_InitializeClientAsync, 
					clientId, 
					_ssoDomain.SSODomainString, 
					cookieDomain
				);
				return result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success;
			}

			public async Task<ViverseResult<AccessTokenResult>> GetAccessToken()
			{
				return await ViverseAsyncHelperLib.ExecuteWithResult(
					() => ViverseAsyncHelperLib.WrapAsyncWithPayload(SSO_GetAccessToken),
					ViverseAsyncHelperLib.ParseJsonPayload<AccessTokenResult>,
					"Get Access Token"
				);
			}

			public async Task<ViverseResult<LoginResult>> CheckAuth()
			{
				return await ViverseAsyncHelperLib.ExecuteWithResult(
					() => ViverseAsyncHelperLib.WrapAsyncWithPayload(SSO_CheckAuth),
					ViverseAsyncHelperLib.ParseJsonPayload<LoginResult>,
					"Check Auth"
				);
			}

			public async Task<ViverseResult<LoginResult>> LoginWithWorlds(string state = null)
			{
				return await ViverseAsyncHelperLib.ExecuteWithResult(
					() => ViverseAsyncHelperLib.WrapAsyncWithPayload(SSO_LoginWithWorlds, state ?? ""),
					ViverseAsyncHelperLib.ParseJsonPayload<LoginResult>,
					"Login With Worlds"
				);
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
				ViverseLogger.LogInfo(ViverseLogger.Categories.SSO, "Logout redirect URL: {0}", redirectUrl);

				return await ViverseAsyncHelperLib.ExecuteWithBoolResult(
					() => ViverseAsyncHelperLib.WrapAsyncWithPayload(SSO_Logout, redirectUrl),
					"Logout"
				);
			}

			/// <summary>
			/// Detect and handle OAuth callback, reinitializing client if needed
			/// </summary>
			public async Task<ViverseResult<OAuthCallbackResult>> DetectAndHandleOAuthCallback(string clientId, string cookieDomain = null)
			{
				if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException(nameof(clientId));
				
				return await ViverseAsyncHelperLib.ExecuteWithResult(
					() => ViverseAsyncHelperLib.WrapAsyncWithPayload(
						SSO_DetectAndHandleOAuthCallback, 
						clientId, 
						_ssoDomain.SSODomainString, 
						cookieDomain
					),
					ViverseAsyncHelperLib.ParseJsonPayload<OAuthCallbackResult>,
					"Detect OAuth Callback"
				);
			}

			/// <summary>
			/// Force reinitialize the client (for OAuth callback handling)
			/// </summary>
			public async Task<ViverseResult<bool>> ForceReinitializeClient(string clientId, string cookieDomain = null)
			{
				if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException(nameof(clientId));
				
				return await ViverseAsyncHelperLib.ExecuteWithBoolResult(
					() => ViverseAsyncHelperLib.WrapAsyncWithPayload(
						SSO_ForceReinitializeClient, 
						clientId, 
						_ssoDomain.SSODomainString, 
						cookieDomain
					),
					"Force Reinitialize Client"
				);
			}

		}
	}
}
