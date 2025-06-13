using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ViverseWebGLAPI
{
	public partial class ViverseCore
	{
		/// <summary>
		/// Service class for handling leaderboard functionality
		/// </summary>
		public class LeaderboardServiceClass
		{
			[DllImport("__Internal")]
			private static extern void Leaderboard_Initialize(int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void Leaderboard_UploadScore(string appId, string leaderboardName, string score,
				int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void Leaderboard_GetLeaderboard(string appId, string leaderboardName, int taskId,
				Action<string> callback);

			[DllImport("__Internal")]
			private static extern void ViverseSDK_SetLeaderboardHosts(string baseURL, string communityBaseURL);

			[DllImport("__Internal")]
			private static extern void Achievement_UploadUserAchievement(string appId, string achievementsJson,
				int taskId, Action<string> callback);

			[DllImport("__Internal")]
			private static extern void Achievement_GetUserAchievement(string appId, int taskId, Action<string> callback);

			private readonly string _baseURL;
			private readonly string _communityBaseURL;

			public LeaderboardServiceClass() : this("https://www.viveport.com/", "https://www.viverse.com/")
			{
			}

			private LeaderboardServiceClass(string baseURL, string communityBaseURL)
			{
				_baseURL = baseURL;
				_communityBaseURL = communityBaseURL;
			}

			public async Task<ViverseResult<bool>> Initialize()
			{
				if (!string.IsNullOrEmpty(_baseURL) && !string.IsNullOrEmpty(_communityBaseURL))
				{
					ViverseSDK_SetLeaderboardHosts(_baseURL, _communityBaseURL);
				}

				var result = await CallNativeViverseFunction(Leaderboard_Initialize);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					// ✅ Use safe logging extension for comprehensive error reporting
					var failureResult = ViverseResult<bool>.Failure(result);
					failureResult.LogError("Initialize Leaderboard");
					return failureResult;
				}

				return ViverseResult<bool>.Success(true, result);
			}

			public async Task<ViverseResult<LeaderboardResult>> UploadScore(string appId, string leaderboardName,
				string score)
			{
				void UploadWrapper(int taskId, Action<string> callback)
				{
					Leaderboard_UploadScore(appId, leaderboardName, score, taskId, callback);
				}

				var result = await CallNativeViverseFunction(UploadWrapper);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					// ✅ Use safe logging extension for comprehensive error reporting
					var failureResult = ViverseResult<LeaderboardResult>.Failure(result);
					failureResult.LogError("Upload Leaderboard Score");
					return failureResult;
				}

				try
				{
					var leaderboardResult = JsonUtility.FromJson<LeaderboardResult>(result.Payload);
					return ViverseResult<LeaderboardResult>.Success(leaderboardResult, result);
				}
				catch (Exception e)
				{
					Debug.LogError($"Failed to parse leaderboard result: {e.Message}");
					return ViverseResult<LeaderboardResult>.Failure(result);
				}
			}

			public async Task<ViverseResult<LeaderboardResult>> GetLeaderboardScores(string appId,
				LeaderboardConfig leaderboardConfig)
			{
				var leaderboardConfigString =
					JsonUtility.ToJson(leaderboardConfig); //serialize early to sniff out exceptions/errors

				void GetLeaderboardNativeFunctionWrapped(int taskId, Action<string> callback)
				{
					Leaderboard_GetLeaderboard(appId, leaderboardConfigString, taskId, callback);
				}

				ViverseSDKReturn result = await CallNativeViverseFunction(GetLeaderboardNativeFunctionWrapped);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					// ✅ Use safe logging extension for comprehensive error reporting
					var failureResult = ViverseResult<LeaderboardResult>.Failure(result);
					failureResult.LogError("Get Leaderboard Scores");
					return failureResult;
				}

				try
				{
					LeaderboardResult leaderboardResult = JsonUtility.FromJson<LeaderboardResult>(result.Payload);
					return ViverseResult<LeaderboardResult>.Success(leaderboardResult, result);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					throw new Exception($"Failed to parse leaderboard result. Error: {e.Message}", e);
				}
			}

			/// <summary>
			/// Uploads user achievements to the specified application.
			/// </summary>
			/// <param name="appId">Application ID from the Developer Console</param>
			/// <param name="achievements">List of achievements to upload</param>
			/// <returns>Result containing achievement upload response</returns>
			public async Task<ViverseResult<AchievementUploadResult>> UploadUserAchievement(string appId, List<Achievement> achievements)
			{
			    if (string.IsNullOrEmpty(appId))
			    {
			        Debug.LogError("AppID cannot be null or empty");
			        return ViverseResult<AchievementUploadResult>.Failure(new ViverseSDKReturn
			        {
			            ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
			            Message = "AppID cannot be null or empty"
			        });
			    }

			    if (achievements == null || !achievements.Any())
			    {
			        Debug.LogError("Achievements list cannot be null or empty");
			        return ViverseResult<AchievementUploadResult>.Failure(new ViverseSDKReturn
			        {
			            ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
			            Message = "Achievements list cannot be null or empty"
			        });
			    }

			    string achievementsJson = JsonUtility.ToJson(new AchievementsWrapper { achievements = achievements.ToArray() });
			    Debug.Log($"Uploading achievements: {achievementsJson}");

			    void UploadWrapper(int taskId, Action<string> callback)
			    {
			        Achievement_UploadUserAchievement(appId, achievementsJson, taskId, callback);
			    }

			    ViverseSDKReturn result = await CallNativeViverseFunction(UploadWrapper);
			    if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
			    {
			        // ✅ Use safe logging extension for comprehensive error reporting
			        var failureResult = ViverseResult<AchievementUploadResult>.Failure(result);
			        failureResult.LogError("Upload User Achievement");
			        return failureResult;
			    }

			    try
			    {
			        AchievementUploadResult uploadResult = JsonUtility.FromJson<AchievementUploadResult>(result.Payload);
			        return ViverseResult<AchievementUploadResult>.Success(uploadResult, result);
			    }
			    catch (Exception e)
			    {
			        Debug.LogError($"Failed to parse achievement upload result: {e.Message}");
			        return ViverseResult<AchievementUploadResult>.Failure(result);
			    }
			}

			/// <summary>
			/// Gets user achievements for the specified application.
			/// </summary>
			/// <param name="appId">Application ID from the Developer Console</param>
			/// <returns>Result containing user achievements</returns>
			public async Task<ViverseResult<UserAchievementResult>> GetUserAchievement(string appId)
			{
			    if (string.IsNullOrEmpty(appId))
			    {
			        Debug.LogError("AppID cannot be null or empty");
			        return ViverseResult<UserAchievementResult>.Failure(new ViverseSDKReturn
			        {
			            ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
			            Message = "AppID cannot be null or empty"
			        });
			    }

			    void GetAchievementWrapper(int taskId, Action<string> callback)
			    {
			        Achievement_GetUserAchievement(appId, taskId, callback);
			    }

			    ViverseSDKReturn result = await CallNativeViverseFunction(GetAchievementWrapper);
			    if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
			    {
			        // ✅ Use safe logging extension for comprehensive error reporting
			        var failureResult = ViverseResult<UserAchievementResult>.Failure(result);
			        failureResult.LogError("Get User Achievement");
			        return failureResult;
			    }

			    try
			    {
			        UserAchievementResult achievementResult = JsonUtility.FromJson<UserAchievementResult>(result.Payload);
			        return ViverseResult<UserAchievementResult>.Success(achievementResult, result);
			    }
			    catch (Exception e)
			    {
			        Debug.LogError($"Failed to parse user achievement result: {e.Message}");
			        return ViverseResult<UserAchievementResult>.Failure(result);
			    }
			}
		}
	}
}
