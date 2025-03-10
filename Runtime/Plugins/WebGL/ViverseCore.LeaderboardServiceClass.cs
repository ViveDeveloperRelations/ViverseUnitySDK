using System;
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
					Debug.LogWarning(
						$"Failed to initialize leaderboard: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
					return ViverseResult<bool>.Failure(result);
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
					Debug.LogWarning(
						$"Failed to upload score: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
					return ViverseResult<LeaderboardResult>.Failure(result);
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
					Debug.LogWarning($"Failed to upload score ViverseSDK. Error code: {result.ReturnCode} info:" +
					                 ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode) + " payload:" +
					                 JsonUtility.ToJson(result));
					return ViverseResult<LeaderboardResult>.Failure(result);
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
		}
	}
}
