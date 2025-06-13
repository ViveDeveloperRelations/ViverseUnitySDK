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
		
		// Multiplayer Services (v1.2.9)
		public PlayServiceClass PlayService { get; private set; }
		public MatchmakingServiceClass MatchmakingService { get; private set; }
		public MultiplayerServiceClass MultiplayerService { get; private set; }

		private SemaphoreSlim
			m_initializeSemaphore =
				new SemaphoreSlim(1, 1); //probably overkill, but when we get threads this will be needed

		public bool IsInitialized { get; private set; }

		/// <summary>
		/// Helper function to call a native function using the centralized async helper library.
		/// </summary>
		/// <param name="nativeFunction">Native function to call that will have the taskid (int) and callback (Action<string>) parameters</param>
		private static Task<ViverseSDKReturn> CallNativeViverseFunction(Action<int, Action<string>> nativeFunction)
		{
			return ViverseAsyncHelperLib.WrapAsyncWithPayload(nativeFunction);
		}

		/// <summary>
		/// Reset all multiplayer services (called during logout or cleanup)
		/// </summary>
		internal void ResetMultiplayerServices()
		{
			try
			{
				PlayService?.Reset();
				MatchmakingService?.Reset();
				MultiplayerService?.Reset();
				ViverseEventDispatcher.Reset();
				CleanupActiveRooms();
				ViverseLogger.LogInfo(ViverseLogger.Categories.CORE, "All multiplayer services reset");
			}
			catch (Exception e)
			{
				ViverseLogger.LogException(ViverseLogger.Categories.CORE, e, "Exception during multiplayer services reset");
			}
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
				
				// Initialize multiplayer services (v1.2.9)
				PlayService = new PlayServiceClass();
				MatchmakingService = new MatchmakingServiceClass();
				MultiplayerService = new MultiplayerServiceClass();
				
				// Initialize the global event dispatcher for JavaScript->C# event bridging
				ViverseEventDispatcher.Initialize(this);
				
				IsInitialized = true;

				return ViverseResult<bool>.Success(true, result);
			}
			finally
			{
				m_initializeSemaphore.Release();
			}
		}

		// Room Management for Room-Based Event System
		private static readonly Dictionary<string, ViverseRoom> s_activeRooms = new();

		/// <summary>
		/// Create a new room-based multiplayer manager for the specified app.
		/// This creates a fresh room manager instance that follows the proper architecture:
		/// PlayService → MatchmakingService → Room Operations → MultiplayerService
		/// </summary>
		/// <param name="appId">The application ID for multiplayer context</param>
		/// <returns>ViverseRoom instance for room operations and typed event subscriptions</returns>
		public ViverseRoom CreateRoomManager(string appId)
		{
			if (string.IsNullOrEmpty(appId))
				throw new ArgumentException("App ID cannot be null or empty", nameof(appId));

			if (!IsInitialized)
			{
				ViverseLogger.LogError(ViverseLogger.Categories.CORE, "ViverseCore not initialized. Call Initialize() first.");
				return null;
			}

			// Create new room manager - each app can have multiple room managers
			var roomManagerKey = $"{appId}_{Guid.NewGuid():N}";
			var newRoom = new ViverseRoom(this, appId, roomManagerKey);
			s_activeRooms[roomManagerKey] = newRoom;

			ViverseLogger.LogInfo(ViverseLogger.Categories.CORE, "Created new room manager for app: {0} (key: {1})", appId, roomManagerKey);
			return newRoom;
		}


		/// <summary>
		/// Remove a room from active management (called when room is disposed)
		/// </summary>
		/// <param name="roomManagerKey">The room manager key to remove</param>
		internal static void RemoveRoom(string roomManagerKey)
		{
			if (string.IsNullOrEmpty(roomManagerKey))
				return;

			if (s_activeRooms.Remove(roomManagerKey))
			{
				ViverseLogger.LogInfo(ViverseLogger.Categories.CORE, "Removed room manager: {0}", roomManagerKey);
			}
		}

		/// <summary>
		/// Get all active room IDs (for debugging)
		/// </summary>
		/// <returns>Array of active room IDs</returns>
		public string[] GetActiveRoomIds()
		{
			var roomIds = new string[s_activeRooms.Count];
			s_activeRooms.Keys.CopyTo(roomIds, 0);
			return roomIds;
		}

		/// <summary>
		/// Clean up all active rooms (called during reset/logout)
		/// </summary>
		private void CleanupActiveRooms()
		{
			try
			{
				// Create copy to avoid modification during iteration
				var roomIdsCopy = new string[s_activeRooms.Count];
				s_activeRooms.Keys.CopyTo(roomIdsCopy, 0);

				foreach (var roomId in roomIdsCopy)
				{
					if (s_activeRooms.TryGetValue(roomId, out var room))
					{
						try
						{
							room.Dispose();
						}
						catch (Exception e)
						{
							ViverseLogger.LogWarning(ViverseLogger.Categories.CORE, "Exception disposing room {0}: {1}", roomId, e.Message);
						}
					}
				}

				s_activeRooms.Clear();
				ViverseLogger.LogInfo(ViverseLogger.Categories.CORE, "All active rooms cleaned up");
			}
			catch (Exception e)
			{
				ViverseLogger.LogException(ViverseLogger.Categories.CORE, e, "Exception during room cleanup");
			}
		}
	}
}
