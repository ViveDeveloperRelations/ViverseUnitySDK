using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ViverseWebGLAPI;
using Avatar = ViverseWebGLAPI.Avatar;

public class ViverseSDKSmokeTest : MonoBehaviour
{
	private ViverseCore _core;
	private HostConfig _hostConfig;
	[SerializeField] private string ClientID = "3c3e8325-db8f-4a77-a66b-c189c500b0ad";
	//private const string ClientID = "ccc503fb-8547-49d5-9eb0-79af14113554";
	[SerializeField] private string TestViveportAppidForLeaderboards = "64aa6613-4e6c-4db4-b270-67744e953ce0";
	private AuthKey _authKey;
	private UserInfo _userInfo;

	void Start()
	{
		Debug.Log("Hello World Viverse!");
		InitializeViverse().ContinueWith(task =>
		{
			if (task.IsFaulted)
			{
				Debug.LogError($"Failed to initialize Viverse: {task.Exception}");
			}
			else
			{
				Debug.Log("Viverse initialized successfully");
			}
		}, TaskScheduler.FromCurrentSynchronizationContext());
	}

	private async Task InitializeViverse()
	{
		try
		{
			_hostConfig = GetEnvironmentConfig();
			Debug.Log($"Using host config: {_hostConfig.WorldHost.HostString}");

			_core = new ViverseCore();
			ViverseResult<bool> initResult = await _core.Initialize(_hostConfig, destroyCancellationToken);
			if (!initResult.IsSuccess)
			{
				throw new Exception($"SDK initialization failed: {initResult.ErrorMessage}");
			}

			Debug.Log("SDK initialized successfully");

			Debug.Log("Before SSO");
			await InitializeSSO();
			Debug.Log("After SSO");

			if (_authKey == null)
			{
				Debug.LogWarning(
					"No auth key found, not continuing with other tests (the page is likely about to reload)");
				return;
			}

			await TestLeaderboardFunctionality();

			// Initialize and test Avatar service
			await InitializeAvatarService();

			TestUtilityFunctions();
		}
		catch (Exception e)
		{
			Debug.LogError($"Error during Viverse initialization: {e.Message}\n{e.StackTrace}");
		}
	}

	private HostConfig GetEnvironmentConfig()
	{
		HostConfigUtil.HostType hostType = new HostConfigUtil().GetHostTypeFromPageURLIfPossible(Application.absoluteURL);
		return HostConfigLookup.HostTypeToDefaultHostConfig.TryGetValue(hostType, out var config)
			? config
			: HostConfigLookup.HostTypeToDefaultHostConfig[HostConfigUtil.HostType.PROD];
	}

	private async Task<bool> AlreadyHadAccessToken()
	{
		ViverseResult<AccessTokenResult> tokenResult = await _core.SSOService.GetAccessToken();
		if (!tokenResult.IsSuccess)
		{
			Debug.Log($"No access token found: {tokenResult.ErrorMessage}");
			return false;
		}

		if (tokenResult.Data?.access_token != null)
		{
			Debug.Log($"Got access token immediately, already logged in");
			_authKey = new AuthKey(tokenResult.Data.access_token);
			return true;
		}

		Debug.Log("No existing access token found");
		return false;
		/*
			  //try to get existing auth token cookie
			  //TODO: figure out what to do with the cookie version, as we'd need to set internal state anyway even if it was there
			  string authKeyFromCookie()
			  {
				  var (code,authKey) = ViverseUtils.Cookie.Get(_hostConfig.CookieAccessKey.Key);
				  return authKey;
	   }*/
	}

	private async Task<bool> DoLogin(URLUtils.URLParts urlParts)
	{
		Debug.Log("Initiating login with redirect");
		//this will actually take place after next program load, maybe put something in the playerprefs to indicate we're looking for a login redirect at this point? - that would help with some potential infinite load scenarios
		ViverseResult<LoginResult> loginResult = await _core.SSOService.LoginWithRedirect(urlParts);
		if (!loginResult.IsSuccess)
		{
			Debug.LogError($"Login failed: {loginResult.ErrorMessage}");
			return false;
		}

		if (loginResult.Data == null)
		{
			Debug.LogError("Login failed: No login data received");
			return false;
		}

		Debug.Log($"Login successful");

		ViverseResult<AccessTokenResult> tokenResult = await _core.SSOService.GetAccessToken();
		if (!tokenResult.IsSuccess || string.IsNullOrEmpty(tokenResult.Data?.access_token))
		{
			Debug.LogError($"Failed to get access token after login: {tokenResult.ErrorMessage}");
			return false;
		}

		_authKey = new AuthKey(tokenResult.Data.access_token);
		return true;
	}

	private async Task InitializeSSO()
	{
		try
		{
			Debug.Log("Before SSOService.Initialize");
			bool ssoInitSuccess = _core.SSOService.Initialize(ClientID);
			Debug.Log($"SSO Initialize result: {ssoInitSuccess}");

			URLUtils.URLParts urlParts = URLUtils.ParseURL(Application.absoluteURL);
			Debug.Log($"Current URL parts: {urlParts}");

			if (await AlreadyHadAccessToken())
			{
				return;
			}

			if (urlParts.Parameters.ContainsKey("code") && urlParts.Parameters.ContainsKey("state"))
			{
				Debug.Log("Handling SSO callback");
				ViverseResult<LoginResult> callbackResult = await _core.SSOService.HandleCallback();
				if (!callbackResult.IsSuccess)
				{
					Debug.LogError($"Login callback failed: {callbackResult.ErrorMessage}");
					return;
				}

				if (string.IsNullOrEmpty(callbackResult.Data?.access_token))
				{
					Debug.LogError("Login failed: No access token received");
					return;
				}

				_authKey = new AuthKey(callbackResult.Data.access_token);
				ViverseResult<AccessTokenResult> tokenResult = await _core.SSOService.GetAccessToken();
				if (!tokenResult.IsSuccess)
				{
					Debug.LogError($"Failed to verify access token: {tokenResult.ErrorMessage}");
					return;
				}

				if (tokenResult.Data?.access_token != callbackResult.Data.access_token)
				{
					Debug.LogWarning("Access token mismatch between callback and verification");
				}
			}
			else
			{
				if (await DoLogin(urlParts))
				{
					Debug.Log("SSO login initiated, page will likely refresh");
					return;
				}
				else
				{
					Debug.LogError("Login failed");
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"SSO initialization failed: {e.Message}");
		}
	}

	private async Task<bool> TestLeaderboardFunctionality()
	{
		ViverseResult<bool> initResult = await _core.LeaderboardService.Initialize();
		if (!initResult.IsSuccess)
		{
			Debug.LogError($"Leaderboard service initialization failed: {initResult.ErrorMessage}");
			return false;
		}

		Debug.Log("Leaderboard service initialized");

		const string appendTypeLeaderboard = "alextest";
		const string updateTypeLeaderboard = "alextest2";

		if (!await TestPostScore(updateTypeLeaderboard) ||
		    !await TestPostScore(appendTypeLeaderboard))
		{
			return false;
		}

		if (!await TestGetScores(appendTypeLeaderboard) ||
		    !await TestGetScores(updateTypeLeaderboard))
		{
			return false;
		}

		return true;
	}

	private async Task<bool> TestPostScore(string leaderboardName)
	{
		string testAppId = TestViveportAppidForLeaderboards;
		string testScore = "1000";

		ViverseResult<LeaderboardResult> scoreResult =
			await _core.LeaderboardService.UploadScore(testAppId, leaderboardName, testScore);
		if (!scoreResult.IsSuccess)
		{
			Debug.LogError($"Failed to upload score: {scoreResult.ErrorMessage}");
			return false;
		}
		LeaderboardResult leaderboardResult = scoreResult.Data;

		Debug.Log($"Uploaded score. Total entries: {leaderboardResult.total_count}");
		if (leaderboardResult.ranking == null) return true;
		foreach (LeaderboardEntry entry in leaderboardResult.ranking)
		{
			Debug.Log($"Rank {entry.rank}: {entry.name} - {entry.value}");
		}

		return true;
	}

	private async Task<bool> TestGetScores(string leaderboardName)
	{
		string testAppId = TestViveportAppidForLeaderboards;
		LeaderboardConfig[] configs = new[]
		{
			LeaderboardConfig.CreateDefault(leaderboardName),
			new LeaderboardConfig
			{
				Name = leaderboardName,
				RangeStart = 0,
				RangeEnd = 100,
				Region = LeaderboardRegion.Global,
				TimeRange = LeaderboardTimeRange.Alltime,
				AroundUser = false
			},
			new LeaderboardConfig
			{
				Name = leaderboardName,
				RangeStart = 10,
				RangeEnd = 50,
				Region = LeaderboardRegion.Local,
				TimeRange = LeaderboardTimeRange.Weekly,
				AroundUser = false
			},
			new LeaderboardConfig
			{
				//if arounduser is true, it needs to be the first property set, a bit goofy, so may set explicit classes around this in the future
				AroundUser = true,
				Name = leaderboardName,
				RangeStart = -5,
				RangeEnd = 10,
				Region = LeaderboardRegion.Global,
				TimeRange = LeaderboardTimeRange.Monthly,
			}
		};

		foreach (LeaderboardConfig config in configs)
		{
			if (!await TestGetScoresWithConfig(testAppId, config))
			{
				return false;
			}
		}

		return true;
	}

	private async Task<bool> TestGetScoresWithConfig(string appId, LeaderboardConfig config)
	{
		ViverseResult<LeaderboardResult> result = await _core.LeaderboardService.GetLeaderboardScores(appId, config);
		if (!result.IsSuccess)
		{
			Debug.LogError($"Failed to get scores: {result.ErrorMessage}");
			return false;
		}
		LeaderboardResult leaderboardResult = result.Data;
		Debug.Log($"Leaderboard results for config: {JsonUtility.ToJson(config)} are {JsonUtility.ToJson(leaderboardResult)}");;

		Debug.Log($"Got scores. Total entries: {leaderboardResult.total_count}");
		if (leaderboardResult.ranking == null) return true;
		foreach (LeaderboardEntry entry in leaderboardResult.ranking)
		{
			Debug.Log($"Rank {entry.rank}: {entry.name} - {entry.value}");
		}

		return true;
	}

	private async Task InitializeAvatarService()
	{
		try
		{
			ViverseResult<bool> avatarInit = await _core.AvatarService.Initialize();

			if (!avatarInit.IsSuccess)
			{
				Debug.LogError($"Avatar service initialization failed: {avatarInit.ErrorMessage} - not continuing with avatar tests {JsonUtility.ToJson(avatarInit)}");
				return;
			}

			// Get user profile
			ViverseResult<UserProfile> profile = await _core.AvatarService.GetProfile();
			if (profile.IsSuccess)
			{
				Debug.Log($"Got profile for user: {profile.Data.name}");
				_userInfo.Profile = profile.Data;
			}

			// Get avatar list
			ViverseResult<AvatarListWrapper> avatarListViverseResult = await _core.AvatarService.GetAvatarList();
			List<Avatar> avatarList = new List<Avatar>(avatarListViverseResult.Data.avatars);
			Debug.Log($"Found {avatarList.Count} avatars");
			foreach (var avatar in avatarList)
			{
				Debug.Log(
					$"Avatar ID: {avatar.id}, Private: {avatar.isPrivate} full avatar data: {JsonUtility.ToJson(avatar)}");
			}
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"Avatar service initialization failed with exception: {e.Message}");
		}
	}
	private void TestUtilityFunctions()
	{
		// Test Cookie operations
		ViverseResult<bool> cookieResult = ViverseUtils.Cookie.Set("testKey", "testValue", 7);
		Debug.Log($"Cookie set result: {cookieResult}");

		ViverseResult<string> cookieValue = ViverseUtils.Cookie.Get("testKey");
		Debug.Log($"Cookie get result - Code: {JsonUtility.ToJson(cookieValue)}, Value: {cookieValue.Data}");

		// Test SessionStorage operations
		ViverseResult<bool> sessionResult = ViverseUtils.SessionStorage.Set("sessionKey", "sessionValue");
		Debug.Log($"Session storage set result: {sessionResult.IsSuccess} data just in case {sessionResult.Data}");

		ViverseResult<string> sessionCodeResult = ViverseUtils.SessionStorage.Get("sessionKey");
		Debug.Log($"Session storage get result - Value: {sessionCodeResult.Data} Full json in case: {JsonUtility.ToJson(sessionCodeResult)},");

		// Test device detection
		ViverseResult<string> userAgentStringResult = ViverseUtils.UserAgentHelper.GetUserAgent();
		Debug.Log($"User Agent: {userAgentStringResult.Data}");
		Debug.Log($"Is Mobile: {ViverseUtils.UserAgentHelper.DeviceChecks.IsMobileDevice()}");
		Debug.Log($"Is iOS: {ViverseUtils.UserAgentHelper.DeviceChecks.IsIOSDevice()}");
		Debug.Log($"Is HTC VR: {ViverseUtils.UserAgentHelper.DeviceChecks.IsHtcVRDevice()}");
		Debug.Log($"Supports VR: {ViverseUtils.UserAgentHelper.DeviceChecks.SupportsVR()}");
		Debug.Log($"Is in XR: {ViverseUtils.UserAgentHelper.DeviceChecks.IsInXRMode()}");
	}
}
