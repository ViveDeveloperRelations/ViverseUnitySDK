using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using UnityEngine;

namespace ViverseWebGLAPI
{
	/// <summary>
	/// Core class for handling viverse functions, wrap and manage the sdk functions
	/// </summary>
	public partial class ViverseCore
	{
		[DllImport("__Internal")]
		private static extern void ViverseSDK_Initialize(int taskId, Action<string> callback);

		[DllImport("__Internal")]
		internal static extern void FreeString(IntPtr ptr);

		public SSOServiceClass SSOService { get; private set; }
		public LeaderboardServiceClass LeaderboardService { get; private set; }
		public AvatarServiceClass AvatarService { get; private set; }

		private static Dictionary<int, TaskCompletionSource<ViverseSDKReturn>> s_pendingTasks = new();
		private static int s_taskId = 0;

		private SemaphoreSlim
			m_initializeSemaphore =
				new SemaphoreSlim(1, 1); //probably overkill, but when we get threads this will be needed

		public bool IsInitialized { get; private set; }

		/// <summary>
		/// Handle internal callbacks from the viverse api
		/// </summary>
		/// <param name="result">JSON formatted ViverseSDKReturn class object</param>
		[MonoPInvokeCallback(typeof(Action<string>))]
		public static void OnTaskCompleteViverse(string result)
		{
			if (string.IsNullOrEmpty(result))
			{
				Debug.LogError("Received null or empty result from viverse sdk - this is a bug");
				return;
			}

			try
			{
				ViverseSDKReturn sdkReturn = JsonUtility.FromJson<ViverseSDKReturn>(result);
				int taskId = sdkReturn.TaskId;

				if (!s_pendingTasks.ContainsKey(taskId))
				{
					Debug.LogError($"TaskId {taskId} not found in pending tasks - this is a bug - json received :{result}");
					return;
				}

				s_pendingTasks[taskId].SetResult(sdkReturn);
			}
			catch (Exception e)
			{
				Debug.LogError("Result was not valid json - json received :"+result);
				Debug.LogException(e);
			}
		}

		/// <summary>
		/// Helper function to call a native function, and simulate a task completion through the callback from javascript
		/// </summary>
		/// <param name="nativeFunction">Native function to call that will have the taskid (int) and callback (Action<string>) parameters, where the callback currently should be returning a json formatted ViverseSDKReturn from javascript</param>
		private static Task<ViverseSDKReturn> CallNativeViverseFunction(Action<int, Action<string>> nativeFunction)
		{
			TaskCompletionSource<ViverseSDKReturn> tcs = new();
			s_taskId++;
			s_pendingTasks.Add(s_taskId, tcs);
			try
			{
				nativeFunction(s_taskId, OnTaskCompleteViverse);
				return tcs.Task;
			}
			catch (Exception e)
			{
				Debug.LogError("Exception when calling native function");
				Debug.LogException(e);
				tcs.SetResult(new ViverseSDKReturn
				{
					TaskId = s_taskId,
					ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
					Message = e.Message
				});
			}

			return tcs.Task;
		}

		public async Task<ViverseResult<bool>> Initialize(HostConfig hostConfig, CancellationToken ct)
		{
			if (hostConfig == null) throw new ArgumentNullException(nameof(hostConfig));
			if (string.IsNullOrEmpty(hostConfig.SSODomain?.SSODomainString))
				throw new ArgumentException(nameof(hostConfig.SSODomain) + " must not be empty");
			if (string.IsNullOrEmpty(hostConfig.AvatarHost?.HostString))
				throw new ArgumentException(nameof(hostConfig.AvatarHost) + " must not be empty");
			if (string.IsNullOrEmpty(hostConfig.WorldAPIHost?.HostString))
				throw new ArgumentException(nameof(hostConfig.WorldAPIHost) + " must not be empty");
			if (string.IsNullOrEmpty(hostConfig.WorldHost?.HostString))
				throw new ArgumentException(nameof(hostConfig.WorldHost) + " must not be empty");
			if (string.IsNullOrEmpty(hostConfig.CookieAccessKey?.Key))
				throw new ArgumentException(nameof(hostConfig.CookieAccessKey) + " must not be empty");

			if (IsInitialized)
			{
				ViverseSDKReturn alreadyInitializedResult = new ViverseSDKReturn
				{
					ReturnCode = (int)ViverseSDKReturnCode.Success,
					Message = "Already initialized"
				};
				return ViverseResult<bool>.Success(true, alreadyInitializedResult);
			}

			await m_initializeSemaphore.WaitAsync(ct);
			try
			{
				if (IsInitialized)
				{
					var alreadyInitializedResult = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.Success,
						Message = "Already initialized"
					};
					return ViverseResult<bool>.Success(true, alreadyInitializedResult);
				}

				ViverseSDKReturn result = await CallNativeViverseFunction(ViverseSDK_Initialize);
				if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
				{
					return ViverseResult<bool>.Failure(result);
				}

				ct.ThrowIfCancellationRequested();

				SSOService = new SSOServiceClass(hostConfig.SSODomain);
				LeaderboardService = new LeaderboardServiceClass();
				AvatarService = new AvatarServiceClass(hostConfig.AvatarHost);
				IsInitialized = true;

				return ViverseResult<bool>.Success(true, result);
			}
			finally
			{
				m_initializeSemaphore.Release();
			}
		}
	}
}
