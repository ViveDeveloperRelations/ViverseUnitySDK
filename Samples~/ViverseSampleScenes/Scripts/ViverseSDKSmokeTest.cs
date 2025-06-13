using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using ViverseWebGLAPI;
using Avatar = ViverseWebGLAPI.Avatar;

/// <summary>
/// Comprehensive smoke test demonstrating Viverse SDK functionality with proper error handling.
/// This sample demonstrates the use of ViverseResult logging helpers and recovery patterns.
///
/// Key patterns shown:
/// - ✅ Using result.LogError("Operation Name") for comprehensive error reporting
/// - ✅ Using result.IsRecoverableError() to identify errors that may benefit from recovery
/// - ✅ Safe property access with SafePayload, SafeMessage, etc.
/// - ✅ Proper async/await patterns for all SDK operations
/// </summary>
public class ViverseSDKSmokeTest : MonoBehaviour
{
	private ViverseCore _core;
	private HostConfig _hostConfig;

	private string ClientID = "araydrgggn";

	//private const string ClientID = "ccc503fb-8547-49d5-9eb0-79af14113554";
	private string TestViveportAppidForLeaderboards = "64aa6613-4e6c-4db4-b270-67744e953ce0";
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
				if (_core != null && _authKey != null)
				{
					Debug.Log("Viverse initialized successfully. maybe other tasks failed though, check logs");
				}
				else
				{
					Debug.LogWarning("Viverse failed to init successfully.");
				}
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
				// ✅ Use safe logging extension for comprehensive error reporting
				initResult.LogError("SDK Core Initialize");

				// ✅ Demonstrate recovery pattern for critical errors
				if (initResult.IsRecoverableError())
				{
					Debug.LogWarning("SDK initialization failed with recoverable error - this sample doesn't implement recovery");
				}

				throw new Exception($"SDK initialization failed: {initResult.ErrorMessage}");
			}

			Debug.Log("SDK initialized successfully");

			Debug.Log("Before SSO");
			bool ssoSuccess = await InitializeSSO();
			if (!ssoSuccess)
			{
				Debug.LogError("SSO initialization failed");
				return;
			}
			Debug.Log("After SSO");

			if (_authKey == null)
			{
				Debug.LogWarning(
					"No auth key found, not continuing with other tests (the page is likely about to reload)");
				return;
			}

			// Test leaderboard functionality
			Debug.Log("=== Starting Leaderboard Tests ===");
			// await TestLeaderboardFunctionality(); // COMMENTED OUT FOR TESTING

			// Test achievement functionality
			Debug.Log("=== Starting Achievement Tests ===");
			await TestAchievementFunctionality();

			// Initialize and test Avatar service
			Debug.Log("=== Starting Avatar Service Tests ===");
			// await InitializeAvatarService(); // COMMENTED OUT FOR TESTING

			// Initialize Matchmaking service before room-based events
			Debug.Log("=== Initializing Matchmaking Service ===");
			await InitializeMatchmakingService();

			// Test room-based multiplayer events
			Debug.Log("=== Starting Room-Based Event Tests ===");
			await TestRoomBasedEvents();

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
		ViverseResult<LoginResult> tokenResult = await _core.SSOService.CheckAuth();
		if (!tokenResult.IsSuccess)
		{
			// ✅ Use safe logging extension for comprehensive error reporting
			tokenResult.LogError("Get Access Token");
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

	private async Task<LoginResult> DoLogin()
	{
		Debug.Log("Initiating enhanced login with LoginWithWorlds");
		// Enhanced LoginWithWorlds now blocks until auth completes and returns tokens directly
		ViverseResult<LoginResult> loginResult = await _core.SSOService.LoginWithWorlds();
		if (!loginResult.IsSuccess)
		{
			// ✅ Use safe logging extension for comprehensive error reporting
			loginResult.LogError("Login With Worlds failed");

			// ✅ Check if recoverable for user guidance
			if (loginResult.IsRecoverableError())
			{
				Debug.LogWarning("Login failed with recoverable error - user may try again");
			}

			return null;
		}

		URLUtils.URLParts urlParts = URLUtils.ParseURL(Application.absoluteURL);
		Debug.Log($"Current URL parts after reported login success: {urlParts}");

		string authToken = loginResult?.Data?.access_token;
		if (string.IsNullOrEmpty(authToken))
		{
			Debug.LogError("Login failed: No auth token received from LoginWithWorlds, despite reported success");
			return null;
		}
		Debug.Log($"✅ Enhanced login successful - received auth token directly from LoginWithWorlds");
		Debug.Log($"Auth token: {authToken} ");

		// Set the auth key directly from LoginWithWorlds result
		_authKey = new AuthKey(loginResult.Data.access_token);
		return loginResult.Data;
	}

	private async Task<bool> InitializeSSO()
	{
		try
		{
			Debug.Log("Before SSOService.Initialize");
			bool ssoInitSuccess = await _core.SSOService.Initialize(ClientID);
			Debug.Log($"SSO Initialize result: {ssoInitSuccess}");

			URLUtils.URLParts urlParts = URLUtils.ParseURL(Application.absoluteURL);
			Debug.Log($"Current URL parts: {urlParts}");

			if (await AlreadyHadAccessToken())
			{
				return true;
			}
			Debug.Log("Didn't already have login token, so triggering login");
			LoginResult loginResult = await DoLogin();
			return loginResult != null;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"SSO initialization failed: {e.Message}");
			return false;
		}
	}

	private async Task<bool> TestLeaderboardFunctionality()
	{
		try
		{
			ViverseResult<bool> initResult = await _core.LeaderboardService.Initialize();
			if (!initResult.IsSuccess)
			{
				// ✅ Use safe logging extension for comprehensive error reporting
				initResult.LogError("Initialize Leaderboard Service");
				return false;
			}

			Debug.Log("✅ Leaderboard service initialized successfully");

			const string appendTypeLeaderboard = "alextest";
			const string updateTypeLeaderboard = "alextest2";

			Debug.Log("Testing leaderboard score upload functionality...");
			if (!await TestPostScore(updateTypeLeaderboard) ||
			    !await TestPostScore(appendTypeLeaderboard))
			{
				Debug.LogError("Leaderboard score upload tests failed");
				return false;
			}

			Debug.Log("Testing leaderboard score retrieval functionality...");
			if (!await TestGetScores(appendTypeLeaderboard) ||
			    !await TestGetScores(updateTypeLeaderboard))
			{
				Debug.LogError("Leaderboard score retrieval tests failed");
				return false;
			}

			Debug.Log("✅ Leaderboard functionality tests completed successfully");
			return true;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"Leaderboard functionality test failed with exception: {e.Message}");
			return false;
		}
	}

	private async Task<bool> TestPostScore(string leaderboardName)
	{
		string testAppId = TestViveportAppidForLeaderboards;
		string testScore = "1000";

		ViverseResult<LeaderboardResult> scoreResult =
			await _core.LeaderboardService.UploadScore(testAppId, leaderboardName, testScore);
		if (!scoreResult.IsSuccess)
		{
			// ✅ Use safe logging extension for comprehensive error reporting
			scoreResult.LogError("Upload Score");
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
			// ✅ Use safe logging extension for comprehensive error reporting
			result.LogError("Get Leaderboard Scores");
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
				// ✅ Use safe logging extension for comprehensive error reporting
				avatarInit.LogError("Initialize Avatar Service");
				Debug.LogError("Not continuing with avatar tests");
				return;
			}

			Debug.Log("✅ Avatar service initialized successfully");

			// Test GetProfile - Enhanced logging
			await TestGetProfile();

			// Test GetActiveAvatar - New method
			await TestGetActiveAvatar();

			// Get avatar list
			ViverseResult<Avatar[]> avatarListViverseResult = await _core.AvatarService.GetAvatarList();
			if (avatarListViverseResult.IsSuccess && avatarListViverseResult.Data != null)
			{
				List<Avatar> avatarList = new List<Avatar>(avatarListViverseResult.Data);
				Debug.Log($"Found {avatarList.Count} avatars");
				foreach (var avatar in avatarList)
				{
					Debug.Log(
						$"Avatar ID: {avatar.id}, Private: {avatar.isPrivate} full avatar data: {JsonUtility.ToJson(avatar)}");
				}
			}
			else
			{
				Debug.LogWarning("Failed to get avatar list or no avatars found");
				avatarListViverseResult?.LogError("Get Avatar List");
			}

			// Test GetPublicAvatarByID with a public avatar (if available)
			await TestGetPublicAvatarByID();

			Debug.Log("✅ Avatar service tests completed successfully");
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"Avatar service initialization failed with exception: {e.Message}");
		}
	}

	/// <summary>
	/// Initialize the matchmaking service using the new standardized async pattern
	/// </summary>
	private async Task InitializeMatchmakingService()
	{
		try
		{
			Debug.Log("Initializing Matchmaking Service with standardized async pattern...");
			
			// Use the new standardized InitializeAsync method
			ViverseResult<bool> matchmakingInit = await _core.MatchmakingService.InitializeAsync();

			if (!matchmakingInit.IsSuccess)
			{
				// ✅ Use safe logging extension for comprehensive error reporting
				matchmakingInit.LogError("Initialize Matchmaking Service");
				Debug.LogError("Matchmaking service initialization failed - room-based events may not work properly");
				
				// Provide recovery guidance
				if (matchmakingInit.IsRecoverableError())
				{
					Debug.LogWarning("💡 This error may be recoverable - check network connectivity");
				}
				return;
			}

			Debug.Log("✅ Matchmaking service initialized successfully");
			Debug.Log("✅ Raw event callbacks registered using standardized pattern:");
			Debug.Log("  - Uses Action<string> callback instead of IntPtr");
			Debug.Log("  - Uses ViverseAsyncHelper.wrapAsyncWithPayload()");
			Debug.Log("  - Uses ViverseAsyncHelper.safeCallback() for memory management");
			Debug.Log("  - Returns ViverseResult<bool> like other services");
			Debug.Log("  - Same flow as SSO/Leaderboard/Avatar/Multiplayer services");
			
			// Wait for connection state to be established before proceeding
			await WaitForMatchmakingConnection();
			
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"Matchmaking service initialization failed with exception: {e.Message}");
		}
	}

	/// <summary>
	/// Wait for matchmaking connection to be established before proceeding with actor operations
	/// </summary>
	private async Task WaitForMatchmakingConnection()
	{
		Debug.Log("[CONNECTION_WAIT] Waiting for matchmaking connection to be established...");
		LogSDKState("Before Connection Wait");
		
		const int maxWaitSeconds = 15;
		const int checkIntervalMs = 500;
		int totalWaitTime = 0;
		
		while (totalWaitTime < maxWaitSeconds * 1000)
		{
			try
			{
				// Check SDK state to see if we're connected using the new public method
				var stateResult = _core.MatchmakingService.GetSDKStateInfo();
				if (stateResult.IsSuccess && stateResult.Data.connected)
				{
					Debug.Log($"[CONNECTION_SUCCESS] ✅ Matchmaking connected after {totalWaitTime}ms");
					LogSDKState("After Connection Established");
					return;
				}
				else if (!stateResult.IsSuccess)
				{
					Debug.LogWarning($"[CONNECTION_CHECK] Failed to get SDK state: {stateResult.ErrorMessage}");
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[CONNECTION_CHECK] Error checking SDK state: {e.Message}");
			}
			
			// Wait before next check
			await Task.Delay(checkIntervalMs);
			totalWaitTime += checkIntervalMs;
			
			if (totalWaitTime % 2000 == 0) // Log every 2 seconds
			{
				Debug.Log($"[CONNECTION_WAIT] Still waiting for connection... ({totalWaitTime/1000}s/{maxWaitSeconds}s)");
			}
		}
		
		Debug.LogWarning($"[CONNECTION_TIMEOUT] ⚠️ Matchmaking connection not established within {maxWaitSeconds} seconds");
		LogSDKState("After Connection Timeout");
	}

	/// <summary>
	/// Validate that the SDK state is ready for room operations to prevent early disconnects
	/// </summary>
	private async Task ValidateStateBeforeRoomOperations()
	{
		Debug.Log("[VALIDATION] Performing final state validation before room operations...");
		
		try
		{
			// Check SDK state to ensure we're properly connected using the new public method
			var stateResult = _core.MatchmakingService.GetSDKStateInfo();
			if (!stateResult.IsSuccess)
			{
				Debug.LogError($"[VALIDATION] ❌ Failed to get SDK state: {stateResult.ErrorMessage}");
				throw new InvalidOperationException($"Cannot validate SDK state: {stateResult.ErrorMessage}");
			}
			
			var stateData = stateResult.Data;
			
			// Validate connection state
			if (!stateData.connected)
			{
				Debug.LogError("[VALIDATION] ❌ Not connected to matchmaking server - room operations will fail");
				throw new InvalidOperationException("Cannot perform room operations without matchmaking connection");
			}
			
			// Check if room operations are ready
			if (!stateData.isReadyForRoomOperations)
			{
				Debug.LogWarning("[VALIDATION] ⚠️ SDK reports not ready for room operations - this may cause issues");
			}
			
			// Validate no pending operations that might interfere
			if (stateData.pendingOperations > 0)
			{
				Debug.LogWarning($"[VALIDATION] ⚠️ {stateData.pendingOperations} pending operations detected - waiting briefly for completion...");
				await Task.Delay(1000); // Brief wait for pending operations
			}
			
			// Validate actor state if actor is set
			if (stateData.actorSet && stateData.actorInfo != null)
			{
				if (string.IsNullOrEmpty(stateData.actorInfo.session_id) || string.IsNullOrEmpty(stateData.actorInfo.name))
				{
					Debug.LogWarning("[VALIDATION] ⚠️ Actor is set but missing required fields - this may cause room operation failures");
				}
				else
				{
					Debug.Log($"[VALIDATION] ✅ Actor validated: {stateData.actorInfo.name} (session: {stateData.actorInfo.session_id})");
				}
			}
			
			Debug.Log("[VALIDATION] ✅ State validation passed - ready for room operations");
		}
		catch (Exception e)
		{
			Debug.LogError($"[VALIDATION] Error during state validation: {e.Message}");
			throw; // Re-throw to prevent room operations with invalid state
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

	/// <summary>
	/// Helper method to log SDK state at different points in the test
	/// </summary>
	/// <param name="context">Context description for the state check</param>
	private void LogSDKState(string context)
	{
		try
		{
			Debug.Log($"[SDK_STATE_CHECK] === {context} ===");
			
			// Get SDK state from the matchmaking service using the new public method
			if (_core?.MatchmakingService != null)
			{
				var stateResult = _core.MatchmakingService.GetSDKStateInfo();
				if (stateResult.IsSuccess)
				{
					var stateData = stateResult.Data;
					Debug.Log($"[SDK_STATE_CHECK] ✅ Connected: {stateData.connected}");
					Debug.Log($"[SDK_STATE_CHECK] ✅ Joined Lobby: {stateData.joinedLobby}");
					Debug.Log($"[SDK_STATE_CHECK] ✅ Actor Set: {stateData.actorSet}");
					
					// Log detailed actor information for validation
					if (stateData.actorInfo != null)
					{
						Debug.Log($"[SDK_STATE_CHECK] 🤖 Actor Info: session_id='{stateData.actorInfo.session_id}', name='{stateData.actorInfo.name}', level={stateData.actorInfo.level}, skill={stateData.actorInfo.skill}");
						
						// Validate actor has required fields
						if (string.IsNullOrEmpty(stateData.actorInfo.session_id) || string.IsNullOrEmpty(stateData.actorInfo.name))
						{
							Debug.LogWarning($"[SDK_STATE_CHECK] ⚠️ Actor info incomplete - missing session_id or name (may cause room operation failures)");
						}
					}
					else if (stateData.actorSet)
					{
						Debug.LogWarning($"[SDK_STATE_CHECK] ⚠️ Actor marked as set but no actor info available (potential state inconsistency)");
					}
					
					Debug.Log($"[SDK_STATE_CHECK] ✅ Current Room: {stateData.currentRoomId ?? "None"}");
					Debug.Log($"[SDK_STATE_CHECK] ✅ Ready for Room Ops: {stateData.isReadyForRoomOperations}");
					Debug.Log($"[SDK_STATE_CHECK] ✅ Pending Operations: {stateData.pendingOperations}");
					Debug.Log($"[SDK_STATE_CHECK] ✅ Current State: {stateData.currentState}");
				}
				else
				{
					Debug.LogWarning($"[SDK_STATE_CHECK] ⚠️ Failed to get SDK state: {stateResult.ErrorMessage}");
				}
			}
			else
			{
				Debug.LogWarning($"[SDK_STATE_CHECK] ⚠️ Matchmaking service not available - cannot check SDK state");
			}
			
			Debug.Log($"[SDK_STATE_CHECK] === End {context} ===");
		}
		catch (Exception e)
		{
			Debug.LogError($"[SDK_STATE_CHECK] Exception logging SDK state: {e.Message}");
		}
	}


	private async Task TestAchievementFunctionality()
	{
		try
		{
			Debug.Log("Starting achievement tests...");

			// First, get the user's existing achievements
			Debug.Log("Getting initial achievement state...");
			if (!await GetUserAchievements())
			{
				Debug.LogError("Failed to get user achievements - skipping achievement tests");
				return;
			}

			// Test sequential unlocking with verification
			Debug.Log("Testing sequential achievement unlocking...");
			await TestSequentialAchievementUnlocking();

			// Test batch unlocking
			Debug.Log("Testing batch achievement unlocking...");
			string[] batchAchievements = new string[] {
				"test_achievement3",
				"test_achievement4",
				"hidden_achievement2"
			};

			await UnlockAchievementsBatch(batchAchievements);

			// Test resetting an achievement (if supported)
			Debug.Log("Testing achievement reset functionality...");
			await ResetAchievement("test_achievement1");

			// Finally, get the user's achievements again to verify all changes
			Debug.Log("Getting final achievement state to verify changes...");
			await GetUserAchievements();

			Debug.Log("✅ Achievement functionality tests completed successfully");
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Debug.LogError($"Achievement functionality test failed with exception: {e.Message}");
		}
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

	/// <summary>
	/// Test room-based strongly-typed event subscription system
	/// This tests the room-based API with real multiplayer room lifecycle
	/// </summary>
	private async Task TestRoomBasedEvents()
	{
		Debug.Log("=== Starting Room-Based Multiplayer Tests ===");
		Debug.Log("[SMOKE_TEST] Enhanced room connection testing with comprehensive error reporting");

		try
		{
			string testAppId = ClientID;
			Debug.Log($"[SMOKE_TEST] Using test app ID: {testAppId}");

			// Create a new room manager for this app following proper architecture
			Debug.Log("[SMOKE_TEST] Step 1: Creating room manager...");
			ViverseRoom room = _core.CreateRoomManager(testAppId);
			if (room == null)
			{
				Debug.LogError("[SMOKE_TEST] ❌ FAILED: CreateRoomManager returned null - ViverseCore may not be initialized");
				return;
			}
			Debug.Log("[SMOKE_TEST] ✅ Room manager created successfully");

			// Initialize services following proper flow: PlayService → MatchmakingService
			Debug.Log("[SMOKE_TEST] Step 2: Initializing room services...");
			LogSDKState("Before InitializeServices");
			
			var servicesResult = await room.InitializeServices();
			if (!servicesResult.IsSuccess)
			{
				Debug.LogError($"[SMOKE_TEST] ❌ FAILED: InitializeServices failed");
				LogSDKState("After InitializeServices (Failed)");
				servicesResult.LogError("Room Services Initialization");
				
				// Provide specific guidance based on error type
				if (servicesResult.IsRecoverableError())
				{
					Debug.LogWarning("[SMOKE_TEST] 💡 This error may be recoverable - check network connectivity and SDK initialization");
				}
				
				if (servicesResult.RawResult.ReturnCode == (int)ViverseSDKReturnCode.ErrorNetworkTimeout)
				{
					Debug.LogWarning("[SMOKE_TEST] 💡 Timeout error - this may indicate network issues or slow SDK initialization");
				}
				
				return;
			}
			Debug.Log("[SMOKE_TEST] ✅ Room services initialized successfully");
			LogSDKState("After InitializeServices (Success)");

			// Create strongly-typed actor information for this session
			long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			string randomSuffix = UnityEngine.Random.Range(100000000, 999999999).ToString("x8");
			var actorInfo = new ActorInfo
			{
				session_id = $"session_{timestamp}_{randomSuffix}",
				name = $"SmokeTestPlayer-{UnityEngine.Random.Range(1000, 9999)}",
				level = 1,
				skill = 50
			};

			// Create strongly-typed room configuration
			var roomInfo = new RoomInfo
			{
				name = $"SmokeTest-{DateTime.Now:MMdd-HHmm}",
				mode = "testing",
				maxPlayers = 4,
				minPlayers = 1,
				purpose = "smoke_test",
				created_by = "unity_sdk"
			};

			Debug.Log($"[SMOKE_TEST] Step 3: Attempting to join or create room with actor: {actorInfo.name}");
			Debug.Log($"[SMOKE_TEST] Actor session_id: {actorInfo.session_id}");
			Debug.Log($"[SMOKE_TEST] Actor properties: level={actorInfo.level}, skill={actorInfo.skill}");
			Debug.Log($"[SMOKE_TEST] Room name: {roomInfo.name}, mode: {roomInfo.mode}, max players: {roomInfo.maxPlayers}");
			Debug.Log($"[SMOKE_TEST] Room properties: purpose={roomInfo.purpose}, created_by={roomInfo.created_by}");
			LogSDKState("Before JoinOrCreateRoom");

			// Final validation before room operations to ensure state is good for multiplayer connection
			await ValidateStateBeforeRoomOperations();

			// Try to join or create room (this will internally set actor and handle empty room lists)
			var roomResult = await room.JoinOrCreateRoom(actorInfo, roomInfo);
			if (!roomResult.IsSuccess)
			{
				Debug.LogError($"[SMOKE_TEST] ❌ FAILED: JoinOrCreateRoom failed");
				LogSDKState("After JoinOrCreateRoom (Failed)");
				roomResult.LogError("Join Or Create Room");
				
				// Provide specific guidance based on error type
				if (roomResult.IsRecoverableError())
				{
					Debug.LogWarning("[SMOKE_TEST] 💡 This error may be recoverable - check network connectivity and authentication");
				}
				
				if (roomResult.RawResult.ReturnCode == (int)ViverseSDKReturnCode.ErrorNetworkTimeout)
				{
					Debug.LogWarning("[SMOKE_TEST] 💡 Timeout error - this may indicate network issues or server overload");
				}
				
				if (roomResult.RawResult.ReturnCode == (int)ViverseSDKReturnCode.ErrorInvalidParameter)
				{
					Debug.LogWarning("[SMOKE_TEST] 💡 Invalid parameter error - check actor and room information");
				}
				
				if (roomResult.RawResult.ReturnCode == (int)ViverseSDKReturnCode.ErrorInvalidState)
				{
					Debug.LogWarning("[SMOKE_TEST] 💡 Invalid state error - SDK may not be ready for room operations");
					Debug.LogWarning("[SMOKE_TEST] 💡 Check that SDK is connected, joined lobby, and actor is set (lobby join required for CreateRoom/JoinRoom)");
				}
				
				return;
			}
			LogSDKState("After JoinOrCreateRoom (Success)");

			if (room.IsRoomOwner)
			{
				Debug.Log($"[SMOKE_TEST] ✅ Created new room: {room.RoomId}");
				Debug.Log($"[SMOKE_TEST] 💡 This indicates no viable existing rooms were found (all may have been stale/inactive)");
			}
			else
			{
				Debug.Log($"[SMOKE_TEST] ✅ Joined existing room: {room.RoomId}");
				Debug.Log($"[SMOKE_TEST] 🎉 Found and joined an active room with other players!");
			}
			Debug.Log($"[SMOKE_TEST] Room info: {roomResult.Value.name} ({roomResult.Value.currentPlayers}/{roomResult.Value.maxPlayers} players)");
			Debug.Log($"[SMOKE_TEST] Room owner: {room.IsRoomOwner}");

			// Subscribe to room events with counters for verification
			int roomEventsReceived = 0;
			int multiplayerEventsReceived = 0;

			// Matchmaking event subscriptions
			room.OnRoomJoined += (result) => {
				roomEventsReceived++;
				if (result.IsSuccess)
				{
					Debug.Log($"✅ Room Joined Event: {result.Value.name} (Players: {result.Value.currentPlayers}/{result.Value.maxPlayers})");
				}
				else
				{
					Debug.LogError($"❌ Room Join Event Error: {result.Message}");
				}
			};

			room.OnActorJoined += (result) => {
				roomEventsReceived++;
				if (result.IsSuccess)
				{
					Debug.Log($"✅ Actor Joined Event: {result.Value.name} (Session: {result.Value.session_id})");
				}
				else
				{
					Debug.LogError($"❌ Actor Join Event Error: {result.Message}");
				}
			};

			room.OnActorLeft += (result) => {
				roomEventsReceived++;
				if (result.IsSuccess)
				{
					Debug.Log($"✅ Actor Left Event: {result.Value.name}");
				}
				else
				{
					Debug.LogError($"❌ Actor Leave Event Error: {result.Message}");
				}
			};

			room.OnRoomClosed += (result) => {
				roomEventsReceived++;
				if (result.IsSuccess)
				{
					Debug.Log($"✅ Room Closed Event: {result.Value.name}");
				}
				else
				{
					Debug.LogError($"❌ Room Close Event Error: {result.Message}");
				}
			};

			// Multiplayer event subscriptions
			room.OnGeneralMessage += (result) => {
				multiplayerEventsReceived++;
				if (result.IsSuccess)
				{
					Debug.Log($"✅ Message Event: [{result.Value.type}] from {result.Value.sender}: \"{result.Value.text}\"");
				}
				else
				{
					Debug.LogError($"❌ Message Event Error: {result.Message}");
				}
			};

			room.OnPositionUpdate += (result) => {
				multiplayerEventsReceived++;
				if (result.IsSuccess)
				{
					var update = result.Value;
					var entityType = update.entity_type == 1 ? "Player" : "Entity";
					var entityId = update.entity_type == 1 ? update.user_id : update.entity_id;
					Debug.Log($"✅ Position Event: [{entityType}] {entityId} -> ({update.data?.x:F1}, {update.data?.y:F1}, {update.data?.z:F1})");
				}
				else
				{
					Debug.LogError($"❌ Position Event Error: {result.Message}");
				}
			};

			room.OnCompetitionResult += (result) => {
				multiplayerEventsReceived++;
				if (result.IsSuccess)
				{
					Debug.Log($"✅ Competition Event: {result.Value.competition.action_name} - Winner: {result.Value.competition.successor}");
				}
				else
				{
					Debug.LogError($"❌ Competition Event Error: {result.Message}");
				}
			};

			room.OnLeaderboardUpdate += (result) => {
				multiplayerEventsReceived++;
				if (result.IsSuccess)
				{
					Debug.Log($"✅ Leaderboard Event: {result.Value.leaderboard?.Length ?? 0} entries updated");
				}
				else
				{
					Debug.LogError($"❌ Leaderboard Event Error: {result.Message}");
				}
			};

			// Subscribe to events
			room.Subscribe();
			Debug.Log($"[SMOKE_TEST] ✅ Subscribed to room events. Active subscribers: {room.GetActiveSubscriberCount()}");

			// Check if there are other players in the room
			Debug.Log($"[SMOKE_TEST] Step 4: Checking room population...");
			Debug.Log($"[SMOKE_TEST] Current players in room: {roomResult.Value.currentPlayers}/{roomResult.Value.maxPlayers}");
			
			if (roomResult.Value.currentPlayers > 1)
			{
				Debug.Log("[SMOKE_TEST] 🎉 Multiple players detected! Real multiplayer testing is possible.");
				Debug.Log("[SMOKE_TEST] Waiting for multiplayer events from other players...");
				await Task.Delay(5000, destroyCancellationToken);
			}
			else
			{
				Debug.Log("[SMOKE_TEST] ℹ️ Only one player in room. Multiplayer events require multiple players.");
				Debug.Log("[SMOKE_TEST] 💡 To test multiplayer features:");
				Debug.Log("[SMOKE_TEST]   1. Open this test in multiple browser tabs");
				Debug.Log("[SMOKE_TEST]   2. Run the test simultaneously");
				Debug.Log("[SMOKE_TEST]   3. Players will join the same room and can send messages");
				Debug.Log("[SMOKE_TEST] Waiting briefly for potential late joiners...");
				await Task.Delay(3000, destroyCancellationToken);
			}

			// Test room disposal and cleanup
			Debug.Log("[SMOKE_TEST] Step 5: Testing room cleanup...");
			LogSDKState("Before Room Cleanup");
			room.Dispose();

			// Verify the room was removed from active tracking
			string[] activeRooms = _core.GetActiveRoomIds();
			int remainingRooms = activeRooms.Length;

			Debug.Log($"[SMOKE_TEST] ✅ Room cleanup completed. Remaining active room managers: {remainingRooms}");
			LogSDKState("After Room Cleanup");

			Debug.Log($"[SMOKE_TEST] === Room-Based Event Test Complete ===");
			Debug.Log($"[SMOKE_TEST] Events received - Room events: {roomEventsReceived}, Multiplayer events: {multiplayerEventsReceived}");
			
			if (multiplayerEventsReceived == 0 && roomEventsReceived <= 2)
			{
				Debug.Log("[SMOKE_TEST] 📋 RESULT: Basic room functionality works, but no multiplayer events detected.");
				Debug.Log("[SMOKE_TEST] 💡 This is expected with only one player. For full testing:");
				Debug.Log("[SMOKE_TEST]   • Open multiple browser tabs with this test");
				Debug.Log("[SMOKE_TEST]   • Run simultaneously to see real multiplayer events");
			}
			else
			{
				Debug.Log("[SMOKE_TEST] 🎉 RESULT: Multiplayer functionality working! Events detected from other players.");
			}
		}
		catch (Exception e)
		{
			Debug.LogError($"Error during room-based event testing: {e.Message}");
			Debug.LogException(e);
		}
	}

	/// <summary>
	/// Test GetProfile method with detailed logging
	/// </summary>
	private async Task TestGetProfile()
	{
		Debug.Log("=== Testing GetProfile Method ===");

		try
		{
			ViverseResult<UserProfile> profile = await _core.AvatarService.GetProfile();

			if (profile.IsSuccess && profile.Data != null)
			{
				Debug.Log($"✅ GetProfile SUCCESS");
				Debug.Log($"📋 Profile Data:");
				Debug.Log($"   - User Name: {profile.Data.name ?? "null"}");
				Debug.Log($"   - Active Avatar: {(profile.Data.activeAvatar != null ? "Present" : "null")}");

				if (profile.Data.activeAvatar != null)
				{
					Debug.Log($"   - Active Avatar ID: {profile.Data.activeAvatar.id}");
					Debug.Log($"   - Active Avatar Private: {profile.Data.activeAvatar.isPrivate}");
					Debug.Log($"   - Active Avatar VRM URL: {profile.Data.activeAvatar.vrmUrl ?? "null"}");
					Debug.Log($"   - Active Avatar Snapshot: {profile.Data.activeAvatar.snapshot ?? "null"}");
				}

				Debug.Log($"📄 Full Profile JSON: {JsonUtility.ToJson(profile.Data)}");
				// ✅ Use safe logging extension for detailed debugging
				profile.LogDetailed("GetProfile");

				// Store for later use
				_userInfo.Profile = profile.Data;
			}
			else
			{
				// ✅ Use safe logging extension for error handling
				profile.LogError("GetProfile");
			}
		}
		catch (Exception e)
		{
			Debug.LogError($"💥 GetProfile EXCEPTION: {e.Message}");
			Debug.LogException(e);
		}

		Debug.Log("=== GetProfile Test Complete ===\n");
	}

	/// <summary>
	/// Test GetActiveAvatar method with detailed logging
	/// </summary>
	private async Task TestGetActiveAvatar()
	{
		Debug.Log("=== Testing GetActiveAvatar Method ===");

		try
		{
			ViverseResult<Avatar> activeAvatarResult = await _core.AvatarService.GetActiveAvatar();

			if (activeAvatarResult.IsSuccess)
			{
				if (activeAvatarResult.Data != null)
				{
					Debug.Log($"✅ GetActiveAvatar SUCCESS - Avatar found");
					Debug.Log($"🎭 Active Avatar Data:");
					Debug.Log($"   - Avatar ID: {activeAvatarResult.Data.id}");
					Debug.Log($"   - Is Private: {activeAvatarResult.Data.isPrivate}");
					Debug.Log($"   - VRM URL: {activeAvatarResult.Data.vrmUrl ?? "null"}");
					Debug.Log($"   - Head Icon URL: {activeAvatarResult.Data.headIconUrl ?? "null"}");
					Debug.Log($"   - Snapshot URL: {activeAvatarResult.Data.snapshot ?? "null"}");
					Debug.Log($"   - Create Time: {activeAvatarResult.Data.createTime}");
					Debug.Log($"   - Update Time: {activeAvatarResult.Data.updateTime}");

					Debug.Log($"📄 Full Active Avatar JSON: {JsonUtility.ToJson(activeAvatarResult.Data)}");
				}
				else
				{
					Debug.Log($"✅ GetActiveAvatar SUCCESS - No active avatar set");
					Debug.Log($"   User has no active avatar configured");
				}

				Debug.Log($"🔧 Raw Result Code: {activeAvatarResult.RawResult.ReturnCode}");
				Debug.Log($"💬 Raw Result Message: {activeAvatarResult.RawResult.Message ?? "null"}");
			}
			else
			{
				Debug.LogError($"❌ GetActiveAvatar FAILED");
				Debug.LogError($"   Error Message: {activeAvatarResult.ErrorMessage ?? "Unknown error"}");
				Debug.LogError($"   Return Code: {activeAvatarResult.RawResult.ReturnCode}");
				Debug.LogError($"   Raw Result: {JsonUtility.ToJson(activeAvatarResult.RawResult)}");
			}
		}
		catch (Exception e)
		{
			Debug.LogError($"💥 GetActiveAvatar EXCEPTION: {e.Message}");
			Debug.LogException(e);
		}

		Debug.Log("=== GetActiveAvatar Test Complete ===\n");
	}

	/// <summary>
	/// Test GetPublicAvatarByID method with detailed logging
	/// </summary>
	private async Task TestGetPublicAvatarByID()
	{
		Debug.Log("=== Testing GetPublicAvatarByID Method ===");

		try
		{
			// First, get the public avatar list to find a valid ID to test with
			ViverseResult<Avatar[]> publicAvatarsResult = await _core.AvatarService.GetPublicAvatarList();

			if (!publicAvatarsResult.IsSuccess || publicAvatarsResult.Data == null || publicAvatarsResult.Data.Length == 0)
			{
				Debug.LogWarning("⚠️ GetPublicAvatarByID SKIPPED - No public avatars available to test with");
				Debug.Log("=== GetPublicAvatarByID Test Complete ===\n");
				return;
			}

			// Use the first public avatar for testing
			var testAvatar = publicAvatarsResult.Data[0];
			Debug.Log($"🎯 Testing with public avatar ID: {testAvatar.id}");

			ViverseResult<Avatar> avatarByIdResult = await _core.AvatarService.GetPublicAvatarByID(testAvatar.id.ToString());

			if (avatarByIdResult.IsSuccess)
			{
				if (avatarByIdResult.Data != null)
				{
					Debug.Log($"✅ GetPublicAvatarByID SUCCESS - Avatar found");
					Debug.Log($"🎭 Retrieved Avatar Data:");
					Debug.Log($"   - Avatar ID: {avatarByIdResult.Data.id}");
					Debug.Log($"   - Is Private: {avatarByIdResult.Data.isPrivate}");
					Debug.Log($"   - VRM URL: {avatarByIdResult.Data.vrmUrl ?? "null"}");
					Debug.Log($"   - Head Icon URL: {avatarByIdResult.Data.headIconUrl ?? "null"}");
					Debug.Log($"   - Snapshot URL: {avatarByIdResult.Data.snapshot ?? "null"}");
					Debug.Log($"   - Create Time: {avatarByIdResult.Data.createTime}");
					Debug.Log($"   - Update Time: {avatarByIdResult.Data.updateTime}");

					// Verify the data matches what we expected
					if (avatarByIdResult.Data.id.ToString() == testAvatar.id.ToString())
					{
						Debug.Log($"✅ ID Verification PASSED - Retrieved avatar matches requested ID");
					}
					else
					{
						Debug.LogWarning($"⚠️ ID Verification FAILED - Expected {testAvatar.id}, got {avatarByIdResult.Data.id}");
					}

					Debug.Log($"📄 Full Retrieved Avatar JSON: {JsonUtility.ToJson(avatarByIdResult.Data)}");
				}
				else
				{
					Debug.LogWarning($"⚠️ GetPublicAvatarByID SUCCESS - But returned null avatar");
					Debug.LogWarning($"   This might indicate the avatar was deleted or is no longer public");
				}

				Debug.Log($"🔧 Raw Result Code: {avatarByIdResult.RawResult.ReturnCode}");
				Debug.Log($"💬 Raw Result Message: {avatarByIdResult.RawResult.Message ?? "null"}");
			}
			else
			{
				Debug.LogError($"❌ GetPublicAvatarByID FAILED");
				Debug.LogError($"   Avatar ID Tested: {testAvatar.id}");
				Debug.LogError($"   Error Message: {avatarByIdResult.ErrorMessage ?? "Unknown error"}");
				Debug.LogError($"   Return Code: {avatarByIdResult.RawResult.ReturnCode}");
				Debug.LogError($"   Raw Result: {JsonUtility.ToJson(avatarByIdResult.RawResult)}");
			}

			// Test with invalid ID to verify error handling
			Debug.Log($"🧪 Testing error handling with invalid avatar ID...");
			ViverseResult<Avatar> invalidResult = await _core.AvatarService.GetPublicAvatarByID("999999999");

			if (!invalidResult.IsSuccess)
			{
				Debug.Log($"✅ Invalid ID test PASSED - Properly returned error for non-existent avatar");
				Debug.Log($"   Error Message: {invalidResult.ErrorMessage}");
			}
			else
			{
				Debug.LogWarning($"⚠️ Invalid ID test unexpected result - Should have failed for non-existent avatar");
			}
		}
		catch (Exception e)
		{
			Debug.LogError($"💥 GetPublicAvatarByID EXCEPTION: {e.Message}");
			Debug.LogException(e);
		}

		Debug.Log("=== GetPublicAvatarByID Test Complete ===\n");
	}
}
