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

			// Test achievement functionality
			await TestAchievementFunctionality();

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
		HostConfigUtil.HostType hostType =
			new HostConfigUtil().GetHostTypeFromPageURLIfPossible(Application.absoluteURL);
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
		Debug.Log(
			$"Leaderboard results for config: {JsonUtility.ToJson(config)} are {JsonUtility.ToJson(leaderboardResult)}");
		;

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
				Debug.LogError(
					$"Avatar service initialization failed: {avatarInit.ErrorMessage} - not continuing with avatar tests {JsonUtility.ToJson(avatarInit)}");
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
		Debug.Log(
			$"Session storage get result - Value: {sessionCodeResult.Data} Full json in case: {JsonUtility.ToJson(sessionCodeResult)},");

		// Test device detection
		ViverseResult<string> userAgentStringResult = ViverseUtils.UserAgentHelper.GetUserAgent();
		Debug.Log($"User Agent: {userAgentStringResult.Data}");
		Debug.Log($"Is Mobile: {ViverseUtils.UserAgentHelper.DeviceChecks.IsMobileDevice()}");
		Debug.Log($"Is iOS: {ViverseUtils.UserAgentHelper.DeviceChecks.IsIOSDevice()}");
		Debug.Log($"Is HTC VR: {ViverseUtils.UserAgentHelper.DeviceChecks.IsHtcVRDevice()}");
		Debug.Log($"Supports VR: {ViverseUtils.UserAgentHelper.DeviceChecks.SupportsVR()}");
		Debug.Log($"Is in XR: {ViverseUtils.UserAgentHelper.DeviceChecks.IsInXRMode()}");
	}

	private async Task TestAchievementFunctionality()
	{
		Debug.Log("Starting achievement tests...");

		// First, get the user's existing achievements
		if (!await GetUserAchievements())
		{
			Debug.LogError("Failed to get user achievements");
			return;
		}

		// Test sequential unlocking with verification
		await TestSequentialAchievementUnlocking();

		// Test batch unlocking
		string[] batchAchievements = new string[] {
			"test_achievement3",
			"test_achievement4",
			"hidden_achievement2"
		};

		await UnlockAchievementsBatch(batchAchievements);

		// Test resetting an achievement (if supported)
		await ResetAchievement("test_achievement1");

		// Finally, get the user's achievements again to verify all changes
		await GetUserAchievements();

		Debug.Log("Achievement tests completed");
	}

	private async Task<bool> GetUserAchievements()
	{
		Debug.Log("Getting user achievements...");

		try
		{
			string testAppId = TestViveportAppidForLeaderboards;
			ViverseResult<UserAchievementResult> result = await _core.LeaderboardService.GetUserAchievement(testAppId);

			if (!result.IsSuccess)
			{
				Debug.LogError($"Failed to get user achievements: {result.ErrorMessage}");
				return false;
			}

			Debug.Log($"Got {result.Data.total} achievements");

			if (result.Data.achievements != null && result.Data.achievements.Length > 0)
			{
				foreach (var achievement in result.Data.achievements)
				{
					Debug.Log($"Achievement: {achievement.display_name} ({achievement.api_name})");
					Debug.Log($"  Description: {achievement.description}");
					Debug.Log($"  Unlocked: {achievement.is_achieved} (times: {achievement.achieved_times})");
				}
			}
			else
			{
				Debug.Log("No achievements found for this user");
			}

			return true;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"Error getting achievements: {e.Message}");
			return false;
		}
	}

	private async Task<bool> UnlockAchievement(string achievementId)
	{
		Debug.Log($"Attempting to unlock achievement: {achievementId}");

		try
		{
			string testAppId = TestViveportAppidForLeaderboards;

			// Create the achievement object to unlock
			var achievements = new List<Achievement>
			{
				new Achievement
				{
					api_name = achievementId,
					unlock = true
				}
			};

			// Call the API
			ViverseResult<AchievementUploadResult> result =
				await _core.LeaderboardService.UploadUserAchievement(testAppId, achievements);

			if (!result.IsSuccess)
			{
				Debug.LogError($"Failed to upload achievement: {result.ErrorMessage}");
				return false;
			}

			// Process the result
			int successCount = result.Data?.success?.total ?? 0;
			int failureCount = result.Data?.failure?.total ?? 0;

			Debug.Log($"Achievement upload response - Success: {successCount}, Failures: {failureCount}");

			// Log detailed information about successful achievements
			if (successCount > 0 && result.Data.success.achievements != null)
			{
				foreach (var achievement in result.Data.success.achievements)
				{
					Debug.Log($"Successfully unlocked: {achievement.api_name} at timestamp: {achievement.time_stamp}");
				}
			}

			// Log detailed information about failed achievements
			if (failureCount > 0 && result.Data.failure.achievements != null)
			{
				foreach (var achievement in result.Data.failure.achievements)
				{
					Debug.LogWarning(
						$"Failed to unlock: {achievement.api_name}, Code: {achievement.code}, Message: {achievement.message}");
				}
			}

			return successCount > 0;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"Error unlocking achievement: {e.Message}");
			return false;
		}
	}
	/// <summary>
	/// Test unlocking achievements one by one with verification in between
	/// </summary>
	private async Task TestSequentialAchievementUnlocking()
	{
	    Debug.Log("Starting sequential achievement testing...");

	    string[] achievementIds = new string[]
	    {
	        "hidden_achievement1",
	        "test_achievement1",
	        "test_achievement2"
	    };

	    Dictionary<string, bool> initialAchievementStates = await GetAchievementStates();

	    foreach (var achievementId in achievementIds)
	    {
	        // Try to unlock the achievement
	        bool wasUnlocked = await UnlockAchievement(achievementId);

	        // Verify the achievement state changed
	        Dictionary<string, bool> currentStates = await GetAchievementStates();

	        bool initialState = initialAchievementStates.ContainsKey(achievementId) && initialAchievementStates[achievementId];
	        bool currentState = currentStates.ContainsKey(achievementId) && currentStates[achievementId];

	        if (wasUnlocked && !initialState && currentState)
	        {
	            Debug.Log($"✓ Successfully unlocked achievement: {achievementId}");
	        }
	        else if (initialState)
	        {
	            Debug.Log($"ℹ Achievement was already unlocked: {achievementId}");
	        }
	        else
	        {
	            Debug.LogWarning($"⚠ Failed to verify unlocking of achievement: {achievementId}");
	        }

	        // Update the initial states for the next achievement
	        initialAchievementStates = currentStates;
	    }
	}

	/// <summary>
	/// Gets a dictionary mapping achievement IDs to their unlocked state
	/// </summary>
	private async Task<Dictionary<string, bool>> GetAchievementStates()
	{
	    var achievementStates = new Dictionary<string, bool>();

	    string testAppId = TestViveportAppidForLeaderboards;
	    ViverseResult<UserAchievementResult> result = await _core.LeaderboardService.GetUserAchievement(testAppId);

	    if (result.IsSuccess && result.Data.achievements != null)
	    {
	        foreach (var achievement in result.Data.achievements)
	        {
	            achievementStates[achievement.api_name] = achievement.is_achieved;
	        }
	    }

	    return achievementStates;
	}

	/// <summary>
	/// Reset a specific achievement (Note: This might not be possible with all achievement systems,
	/// but it's useful for testing if the API supports it)
	/// </summary>
	private async Task<bool> ResetAchievement(string achievementId)
	{
	    Debug.Log($"Attempting to reset achievement: {achievementId}");

	    try
	    {
	        string testAppId = TestViveportAppidForLeaderboards;

	        // Create the achievement object to reset (unlock = false if supported)
	        var achievements = new List<Achievement>
	        {
	            new Achievement
	            {
	                api_name = achievementId,
	                unlock = false
	            }
	        };

	        // Call the API
	        ViverseResult<AchievementUploadResult> result =
	            await _core.LeaderboardService.UploadUserAchievement(testAppId, achievements);

	        if (!result.IsSuccess)
	        {
	            Debug.LogWarning($"Failed to reset achievement (this may be expected if reset is not supported): {result.ErrorMessage}");
	            return false;
	        }

	        int successCount = result.Data?.success?.total ?? 0;
	        return successCount > 0;
	    }
	    catch (Exception e)
	    {
	        Debug.LogException(e);
	        Debug.LogWarning($"Error resetting achievement (this may be expected): {e.Message}");
	        return false;
	    }
	}

	/// <summary>
	/// Test batch unlocking of achievements
	/// </summary>
	private async Task<bool> UnlockAchievementsBatch(string[] achievementIds)
	{
	    Debug.Log($"Attempting to unlock {achievementIds.Length} achievements in batch");

	    try
	    {
	        string testAppId = TestViveportAppidForLeaderboards;

	        // Create achievement objects for all IDs
	        var achievements = new List<Achievement>();
	        foreach (var id in achievementIds)
	        {
	            achievements.Add(new Achievement { api_name = id, unlock = true });
	        }

	        // Call the API
	        ViverseResult<AchievementUploadResult> result =
	            await _core.LeaderboardService.UploadUserAchievement(testAppId, achievements);

	        if (!result.IsSuccess)
	        {
	            Debug.LogError($"Failed to upload achievements batch: {result.ErrorMessage}");
	            return false;
	        }

	        // Process results
	        int successCount = result.Data?.success?.total ?? 0;
	        int failureCount = result.Data?.failure?.total ?? 0;

	        Debug.Log($"Batch achievement result: {successCount} succeeded, {failureCount} failed");
	        return successCount > 0;
	    }
	    catch (Exception e)
	    {
	        Debug.LogException(e);
	        Debug.LogError($"Error in batch achievement unlock: {e.Message}");
	        return false;
	    }
	}
}
