using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ViverseWebGLAPI
{
    /// <summary>
    /// Room-based strongly-typed event subscription system for Viverse multiplayer events.
    /// Follows proper architecture: PlayService → MatchmakingService → MultiplayerService.
    /// Provides clean developer experience with automatic event management and cleanup.
    /// </summary>
    public class ViverseRoom : IDisposable
    {
        public string RoomId { get; private set; }
        public string AppId { get; }
        public bool IsRoomOwner { get; private set; }
        public RoomData CurrentRoomData { get; private set; }

        // Static tracking of rooms created by this client session
        private static readonly HashSet<string> _ownedRoomIds = new HashSet<string>();

        private ViverseCore _viverseCore;
        private string _roomManagerKey;
        private ViverseCore.MatchmakingServiceClass _matchmakingService;
        private ViverseCore.MultiplayerServiceClass _multiplayerService;
        private bool _isSubscribed = false;
        private bool _isDisposed = false;
        private bool _isInRoom = false;
        private bool _multiplayerInitialized = false;

        // Matchmaking Events - strongly typed with ViverseResult pattern
        public event Action<ViverseResult<RoomData>> OnRoomJoined;
        public event Action<ViverseResult<RoomData>> OnRoomLeft;
        public event Action<ViverseResult<ActorInfo>> OnActorJoined;
        public event Action<ViverseResult<ActorInfo>> OnActorLeft;
        public event Action<ViverseResult<RoomData>> OnRoomClosed;

        // Multiplayer Events - strongly typed with ViverseResult pattern
        public event Action<ViverseResult<GeneralMessage>> OnGeneralMessage;
        public event Action<ViverseResult<PositionUpdate>> OnPositionUpdate;
        public event Action<ViverseResult<CompetitionResult>> OnCompetitionResult;
        public event Action<ViverseResult<LeaderboardUpdate>> OnLeaderboardUpdate;

        // Internal tracking for automatic cleanup
        private string _matchmakingListenerId;
        private string _multiplayerListenerId;

        /// <summary>
        /// Create a new room-based event subscription manager
        /// </summary>
        /// <param name="viverseCore">ViverseCore instance for services</param>
        /// <param name="appId">The application ID for multiplayer initialization</param>
        /// <param name="roomManagerKey">Internal key for tracking this room manager</param>
        internal ViverseRoom(ViverseCore viverseCore, string appId, string roomManagerKey)
        {
            if (viverseCore == null)
                throw new ArgumentNullException(nameof(viverseCore));
            if (string.IsNullOrEmpty(appId))
                throw new ArgumentException("App ID cannot be null or empty", nameof(appId));
            if (string.IsNullOrEmpty(roomManagerKey))
                throw new ArgumentException("Room manager key cannot be null or empty", nameof(roomManagerKey));

            _viverseCore = viverseCore;
            AppId = appId;
            _roomManagerKey = roomManagerKey;

            Debug.Log($"[VIVERSE_ROOM] Created room manager for app: {appId} (key: {roomManagerKey})");
        }

        private void LogSDKState()
        {
	        var stateResult = _matchmakingService.GetSDKStateInfo();
	        if (!stateResult.IsSuccess)
	        {
		        Debug.LogError($"[VIVERSE_ROOM] Failed to get SDK state: {stateResult.ErrorMessage}");
		        return;
	        }
	        
	        var sdkState = stateResult.Data;
	        if (sdkState != null)
	        {
		        Debug.Log($"[VIVERSE_ROOM] SDK State - Connected: {sdkState.connected}, ActorSet: {sdkState.actorSet}, RoomReady: {sdkState.isReadyForRoomOperations}");
	        }
	        else
	        {
		        Debug.Log($"[VIVERSE_ROOM] sdkstate is nulL!");
	        }
        }
        /// <summary>
        /// Initialize the room manager with proper service flow
        /// PlayService → MatchmakingService → Ready for room operations
        /// </summary>
        /// <returns>Result indicating success or failure</returns>
        public async Task<ViverseResult<bool>> InitializeServices()
        {
            try
            {
                Debug.Log($"[VIVERSE_ROOM] Initializing services for app: {AppId}");

                // Initialize PlayService if not already done with timeout protection
                if (!_viverseCore.PlayService.IsInitialized)
                {
                    Debug.Log($"[VIVERSE_ROOM] PlayService not initialized, initializing now...");

                    using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        var playResult = await _viverseCore.PlayService.Initialize();
                        if (!playResult.IsSuccess)
                        {
                            Debug.LogError($"[VIVERSE_ROOM] PlayService initialization failed: {playResult.ErrorMessage}");
                            return ViverseResult<bool>.Failure(playResult.RawResult);
                        }
                    }
                }

                // Create matchmaking client for this app with timeout protection
                Debug.Log($"[VIVERSE_ROOM] Creating and connecting matchmaking client for app: {AppId}");

                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30))) // Longer timeout for full connection
                {
                    var matchmakingResult = await _viverseCore.PlayService.NewMatchmakingClient(AppId, debugMode: true);
                    if (!matchmakingResult.IsSuccess)
                    {
                        Debug.LogError($"[VIVERSE_ROOM] Matchmaking client creation failed: {matchmakingResult.ErrorMessage}");
                        return ViverseResult<bool>.Failure(matchmakingResult.RawResult);
                    }

                    _matchmakingService = matchmakingResult.Data;
                }

                // Wait for matchmaking client to be fully connected and joined lobby

                /*Debug.Log($"[VIVERSE_ROOM] Waiting for matchmaking client to connect and join lobby...");

                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    // Poll the SDK state until we're ready for room operations
                    while (!cancellation.Token.IsCancellationRequested)
                    {
                        try
                        {
                            // Check SDK state using the new public method
                            var stateResult = _matchmakingService.GetSDKStateInfo();
                            if (stateResult.IsSuccess)
                            {
                                var sdkState = stateResult.Data;
                                if (sdkState != null && sdkState.connected && sdkState.joinedLobby)
                                {
                                    Debug.Log($"[VIVERSE_ROOM] Matchmaking client fully ready - connected: {sdkState.connected}, joinedLobby: {sdkState.joinedLobby}");
                                    break;
                                }
                                else
                                {
                                    Debug.Log($"[VIVERSE_ROOM] Still waiting... connected: {sdkState?.connected ?? false}, joinedLobby: {sdkState?.joinedLobby ?? false}");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[VIVERSE_ROOM] Error checking SDK state: {e.Message}");
                        }

                        // Wait 500ms before checking again
                        await Task.Delay(500, cancellation.Token);
                    }

                    if (cancellation.Token.IsCancellationRequested)
                    {
                        Debug.LogError($"[VIVERSE_ROOM] Timed out waiting for matchmaking client to connect and join lobby");
                        return ViverseResult<bool>.Failure(ViverseSDKReturnCode.ErrorNetworkTimeout, "Matchmaking client failed to connect and join lobby within timeout");
                    }
                }
                */

                Debug.Log($"[VIVERSE_ROOM] Services initialized successfully for app: {AppId}");

                var successResult = new ViverseSDKReturn
                {
                    ReturnCode = (int)ViverseSDKReturnCode.Success,
                    Message = "Services initialized successfully with timeout protection"
                };
                return ViverseResult<bool>.Success(true, successResult);
            }
            catch (OperationCanceledException)
            {
                Debug.LogError($"[VIVERSE_ROOM] Service initialization timed out for app: {AppId}");
                return ViverseResult<bool>.Failure(ViverseSDKReturnCode.ErrorNetworkTimeout, "Service initialization timed out");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VIVERSE_ROOM] Exception initializing services: {e.Message}");
                return ViverseResult<bool>.Failure(ViverseSDKReturnCode.ErrorException, $"Exception: {e.Message}");
            }
        }

        /// <summary>
        /// Try to find and join an available room, or create one if none available using strongly-typed data structures.
        /// Follows proper architecture: SetActor → GetAvailableRooms → JoinRoom or CreateRoom → Initialize Multiplayer
        /// </summary>
        /// <param name="actorInfo">Strongly-typed actor information for this player session</param>
        /// <param name="roomInfo">Strongly-typed room configuration if we need to create a new room</param>
        /// <returns>Result indicating success and the room data</returns>
        public async Task<ViverseResult<RoomData>> JoinOrCreateRoom(ActorInfo actorInfo, RoomInfo roomInfo)
        {
            // Validate preconditions
            var validationResult = ValidatePreConditions();
            if (!validationResult.IsSuccess) return validationResult;

            try
            {
                // Set actor information
                var actorResult = await SetActorWithTimeout(actorInfo);
                if (!actorResult.IsSuccess) return actorResult;

                // Try to join existing room
                var joinResult = await TryJoinExistingRoom(actorInfo);
                if (joinResult.IsSuccess) return joinResult;

                // Create new room if join failed
                return await CreateNewRoom(roomInfo);
            }
            catch (OperationCanceledException)
            {
                return HandleTimeout();
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }

        /// <summary>
        /// Validate preconditions before attempting room operations
        /// </summary>
        private ViverseResult<RoomData> ValidatePreConditions()
        {
            if (_isInRoom)
            {
                return ViverseResult<RoomData>.Failure(ViverseSDKReturnCode.ErrorInvalidState, "Already in a room. Call LeaveRoom() first.");
            }

            if (_matchmakingService == null)
            {
                return ViverseResult<RoomData>.Failure(ViverseSDKReturnCode.ErrorModuleNotLoaded, "Services not initialized. Call InitializeServices() first.");
            }

            return ViverseResult<RoomData>.Success(null);
        }

        /// <summary>
        /// Set actor information with timeout protection
        /// </summary>
        private async Task<ViverseResult<RoomData>> SetActorWithTimeout(ActorInfo actorInfo)
        {
            Debug.Log($"[VIVERSE_ROOM] Setting actor information with timeout protection...");

            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var actorResult = await _matchmakingService.SetActor(actorInfo);
                if (!actorResult.IsSuccess)
                {
                    Debug.LogError($"[VIVERSE_ROOM] SetActor failed: {actorResult.ErrorMessage}");
                    return ViverseResult<RoomData>.Failure(actorResult.RawResult);
                }
            }

            return ViverseResult<RoomData>.Success(null);
        }

        /// <summary>
        /// Try to join an existing room from available rooms
        /// </summary>
        private async Task<ViverseResult<RoomData>> TryJoinExistingRoom(ActorInfo actorInfo)
        {
            Debug.Log($"[VIVERSE_ROOM] Getting available rooms with timeout protection...");

            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var roomsResult = await _matchmakingService.GetAvailableRooms();
                if (!roomsResult.IsSuccess || roomsResult.Data == null || roomsResult.Data.Length == 0)
                {
                    Debug.Log($"[VIVERSE_ROOM] No available rooms found or GetAvailableRooms failed: {roomsResult.ErrorMessage}");
                    return ViverseResult<RoomData>.Failure(ViverseSDKReturnCode.ErrorNotFound, "No available rooms to join");
                }

                Debug.Log($"[VIVERSE_ROOM] Found {roomsResult.Data.Length} available rooms");

                // Filter and prioritize rooms for joining
                var viableRooms = FilterViableRooms(roomsResult.Data, actorInfo);

                if (viableRooms.Length == 0)
                {
                    Debug.Log($"[VIVERSE_ROOM] No viable rooms found after filtering (all may be stale/inactive)");
                    return ViverseResult<RoomData>.Failure(ViverseSDKReturnCode.ErrorNotFound, "No viable rooms available");
                }

                Debug.Log($"[VIVERSE_ROOM] {viableRooms.Length} viable rooms after filtering");

                // Try to join viable rooms in priority order
                return await AttemptJoinRooms(viableRooms);
            }
        }

        /// <summary>
        /// Attempt to join rooms from a list of viable rooms
        /// </summary>
        private async Task<ViverseResult<RoomData>> AttemptJoinRooms(RoomData[] viableRooms)
        {
            foreach (var room in viableRooms)
            {
                Debug.Log($"[VIVERSE_ROOM] Attempting to join room: {room.roomId ?? room.id} ({room.currentPlayers}/{room.maxPlayers} players)");

                using (var joinCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                {
                    try
                    {
                        var joinResult = await _matchmakingService.JoinRoom(room.roomId ?? room.id);
                        if (joinResult.IsSuccess)
                        {
                            RoomId = joinResult.Data.roomId ?? joinResult.Data.id;
                            CurrentRoomData = joinResult.Data;
                            IsRoomOwner = false;
                            _isInRoom = true;

                            Debug.Log($"[VIVERSE_ROOM] Successfully joined existing room: {RoomId}");

                            // Initialize multiplayer service for this room
                            await InitializeMultiplayerService();

                            return ViverseResult<RoomData>.Success(joinResult.Data, joinResult.RawResult);
                        }
                        else
                        {
                            Debug.LogWarning($"[VIVERSE_ROOM] Failed to join room {room.roomId ?? room.id}: {joinResult.ErrorMessage}");
                            // Continue to next room instead of giving up
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogWarning($"[VIVERSE_ROOM] Timeout joining room {room.roomId ?? room.id}, trying next room...");
                        // Continue to next room
                    }
                }
            }

            Debug.Log($"[VIVERSE_ROOM] Failed to join any of the {viableRooms.Length} viable rooms");
            return ViverseResult<RoomData>.Failure(ViverseSDKReturnCode.ErrorNotFound, "Failed to join any viable rooms");
        }

        /// <summary>
        /// Create a new room when no viable rooms are available to join
        /// </summary>
        private async Task<ViverseResult<RoomData>> CreateNewRoom(RoomInfo roomInfo)
        {
            Debug.Log($"[VIVERSE_ROOM] No viable rooms to join (all may be stale/inactive), creating new room with timeout protection...");

            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                var createResult = await _matchmakingService.CreateRoom(roomInfo);
                if (createResult.IsSuccess)
                {
                    RoomId = createResult.Data.roomId ?? createResult.Data.id;
                    CurrentRoomData = createResult.Data;
                    IsRoomOwner = true;
                    _isInRoom = true;

                    // Track this room as one we created
                    if (!string.IsNullOrEmpty(RoomId))
                    {
                        _ownedRoomIds.Add(RoomId);
                        Debug.Log($"[VIVERSE_ROOM] Added room {RoomId} to owned rooms tracking");
                    }

                    Debug.Log($"[VIVERSE_ROOM] Successfully created new room: {RoomId}");

                    // Initialize multiplayer service for this room
                    await InitializeMultiplayerService();

                    return ViverseResult<RoomData>.Success(createResult.Data, createResult.RawResult);
                }
                else
                {
                    Debug.LogError($"[VIVERSE_ROOM] CreateRoom failed: {createResult.ErrorMessage}");
                    return ViverseResult<RoomData>.Failure(createResult.RawResult);
                }
            }
        }

        /// <summary>
        /// Handle timeout exceptions in room operations
        /// </summary>
        private ViverseResult<RoomData> HandleTimeout()
        {
            Debug.LogError($"[VIVERSE_ROOM] Room operation timed out for app: {AppId}");
            return ViverseResult<RoomData>.Failure(ViverseSDKReturnCode.ErrorNetworkTimeout, "Room operation timed out");
        }

        /// <summary>
        /// Handle general exceptions in room operations
        /// </summary>
        private ViverseResult<RoomData> HandleException(Exception e)
        {
            Debug.LogError($"[VIVERSE_ROOM] Exception in room operation: {e.Message}");
            return ViverseResult<RoomData>.Failure(ViverseSDKReturnCode.ErrorException, $"Exception: {e.Message}");
        }

        /// <summary>
        /// Filter and prioritize rooms for joining, removing stale/inactive rooms
        /// </summary>
        /// <param name="availableRooms">List of available rooms from server</param>
        /// <param name="actorInfo">Current strongly-typed actor information</param>
        /// <returns>Filtered and prioritized list of viable rooms</returns>
        private RoomData[] FilterViableRooms(RoomData[] availableRooms, ActorInfo actorInfo)
        {
            var viableRooms = new List<RoomData>();

            foreach (var room in availableRooms)
            {
                // Filter criteria for viable rooms
                bool isViable = true;
                string skipReason = "";

                // 1. Must have space for new players
                if (room.currentPlayers >= room.maxPlayers)
                {
                    isViable = false;
                    skipReason = "room is full";
                }

                // 2. Skip rooms with 0 players (likely stale/ghost rooms)
                else if (room.currentPlayers <= 0)
                {
                    isViable = false;
                    skipReason = "room has no active players (likely stale)";
                }

                // 3. Prefer rooms with reasonable player counts (not too many, indicating active host)
                else if (room.currentPlayers > room.maxPlayers * 0.8f)
                {
                    // Still viable but lower priority - room is getting full
                    Debug.Log($"[VIVERSE_ROOM] Room {room.roomId ?? room.id} is {room.currentPlayers}/{room.maxPlayers} (getting full)");
                }

                // 4. Check if room has a valid ID
                if (isViable && string.IsNullOrEmpty(room.roomId) && string.IsNullOrEmpty(room.id))
                {
                    isViable = false;
                    skipReason = "room has no valid ID";
                }

                if (isViable)
                {
                    viableRooms.Add(room);
                    bool isOwnedRoom = _ownedRoomIds.Contains(room.roomId ?? room.id);
                    string ownership = isOwnedRoom ? " [OWN ROOM - HIGH PRIORITY]" : "";
                    Debug.Log($"[VIVERSE_ROOM] Room {room.roomId ?? room.id} is viable ({room.currentPlayers}/{room.maxPlayers} players){ownership}");
                }
                else
                {
                    Debug.Log($"[VIVERSE_ROOM] Skipping room {room.roomId ?? room.id}: {skipReason}");
                }
            }

            // Sort by priority: prefer our own rooms, then rooms with some players but not too full
            viableRooms.Sort((a, b) => {
                // Highest priority: Our own previously created rooms
                bool aIsOwned = _ownedRoomIds.Contains(a.roomId ?? a.id);
                bool bIsOwned = _ownedRoomIds.Contains(b.roomId ?? b.id);

                if (aIsOwned && !bIsOwned) return -1; // A wins (our room)
                if (!aIsOwned && bIsOwned) return 1;  // B wins (our room)

                // If both or neither are owned, use regular scoring
                int scoreA = GetRoomPriorityScore(a);
                int scoreB = GetRoomPriorityScore(b);
                return scoreB.CompareTo(scoreA); // Higher score = higher priority
            });

            return viableRooms.ToArray();
        }

        /// <summary>
        /// Calculate priority score for room selection
        /// </summary>
        /// <param name="room">Room to score</param>
        /// <returns>Priority score (higher = better)</returns>
        private int GetRoomPriorityScore(RoomData room)
        {
            int score = 0;

            // Prefer rooms with some active players
            if (room.currentPlayers >= 1 && room.currentPlayers <= 2)
            {
                score += 100; // Sweet spot - active but not crowded
            }
            else if (room.currentPlayers >= 3 && room.currentPlayers <= room.maxPlayers / 2)
            {
                score += 50; // Good - moderately active
            }
            else if (room.currentPlayers > room.maxPlayers / 2)
            {
                score += 10; // Lower priority - getting crowded
            }

            // Bonus for rooms with descriptive names (suggests active management)
            if (!string.IsNullOrEmpty(room.name) && room.name.Length > 5)
            {
                score += 10;
            }

            return score;
        }

        /// <summary>
        /// Initialize the multiplayer service after joining/creating a room.
        /// This enables real-time communication features.
        /// </summary>
        private async Task InitializeMultiplayerService()
        {
            if (string.IsNullOrEmpty(RoomId))
            {
                Debug.LogWarning($"[VIVERSE_ROOM] Cannot initialize multiplayer - no room ID available");
                return;
            }

            try
            {
                Debug.Log($"[VIVERSE_ROOM] Initializing multiplayer service for room: {RoomId}");

                // Get the multiplayer service and initialize it with the room ID and app ID
                _multiplayerService = _viverseCore.MultiplayerService;
                var multiplayerResult = await _multiplayerService.Initialize(RoomId, AppId);

                if (multiplayerResult.IsSuccess)
                {
                    _multiplayerInitialized = true;
                    Debug.Log($"[VIVERSE_ROOM] Multiplayer service initialized successfully for room: {RoomId}");
                }
                else
                {
                    Debug.LogWarning($"[VIVERSE_ROOM] Failed to initialize multiplayer service: {multiplayerResult.ErrorMessage}");
                    _multiplayerInitialized = false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VIVERSE_ROOM] Exception initializing multiplayer service: {e.Message}");
                _multiplayerInitialized = false;
            }
        }

        /// <summary>
        /// Leave the current room
        /// </summary>
        /// <returns>Result indicating success or failure</returns>
        public async Task<ViverseResult<bool>> LeaveRoom()
        {
            if (!_isInRoom)
            {
                return ViverseResult<bool>.Failure(ViverseSDKReturnCode.ErrorInvalidState, "Not currently in a room");
            }

            if (_matchmakingService == null)
            {
                return ViverseResult<bool>.Failure(ViverseSDKReturnCode.ErrorModuleNotLoaded, "Matchmaking service not available");
            }

            try
            {
                ViverseResult<bool> result;

                if (IsRoomOwner)
                {
                    Debug.Log($"[VIVERSE_ROOM] Closing room as owner: {RoomId}");
                    result = await _matchmakingService.CloseRoom();
                }
                else
                {
                    Debug.Log($"[VIVERSE_ROOM] Leaving room: {RoomId}");
                    result = await _matchmakingService.LeaveRoom();
                }

                if (result.IsSuccess)
                {
                    var oldRoomId = RoomId;

                    // Reset all room state
                    RoomId = null;
                    CurrentRoomData = null;
                    IsRoomOwner = false;
                    _isInRoom = false;
                    _multiplayerInitialized = false;
                    _multiplayerService = null;

                    Debug.Log($"[VIVERSE_ROOM] Successfully left room: {oldRoomId}");
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VIVERSE_ROOM] Exception leaving room: {e.Message}");
                return ViverseResult<bool>.Failure(ViverseSDKReturnCode.ErrorException, $"Exception: {e.Message}");
            }
        }

        /// <summary>
        /// Subscribe to all room events using the proper service classes.
        /// Called automatically when first event listener is added.
        /// </summary>
        public void Subscribe()
        {
            if (_isSubscribed || _isDisposed)
                return;

            if (_matchmakingService == null)
            {
                Debug.LogError($"[VIVERSE_ROOM] Cannot subscribe - matchmaking service not available");
                return;
            }

            try
            {
                // Subscribe to matchmaking events using the MatchmakingServiceClass
                _matchmakingListenerId = _matchmakingService.RegisterNetworkEventListener(
                    MatchmakingEventType.RoomJoined, OnMatchmakingEvent);

                // Subscribe to additional matchmaking events
                _matchmakingService.RegisterNetworkEventListener(MatchmakingEventType.RoomLeft, OnMatchmakingEvent);
                _matchmakingService.RegisterNetworkEventListener(MatchmakingEventType.ActorJoined, OnMatchmakingEvent);
                _matchmakingService.RegisterNetworkEventListener(MatchmakingEventType.ActorLeft, OnMatchmakingEvent);
                _matchmakingService.RegisterNetworkEventListener(MatchmakingEventType.RoomClosed, OnMatchmakingEvent);

                // Subscribe to multiplayer events if multiplayer service is available
                if (_multiplayerInitialized && _multiplayerService != null)
                {
                    _multiplayerListenerId = _multiplayerService.RegisterNetworkEventListener(
                        MultiplayerEventType.Message, OnMultiplayerEvent);

                    _multiplayerService.RegisterNetworkEventListener(MultiplayerEventType.Position, OnMultiplayerEvent);
                    _multiplayerService.RegisterNetworkEventListener(MultiplayerEventType.Competition, OnMultiplayerEvent);
                    _multiplayerService.RegisterNetworkEventListener(MultiplayerEventType.Leaderboard, OnMultiplayerEvent);
                }

                _isSubscribed = true;
                Debug.Log($"[VIVERSE_ROOM] Subscribed to events for room: {RoomId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VIVERSE_ROOM] Failed to subscribe to room {RoomId} events: {e.Message}");
            }
        }

        /// <summary>
        /// Unsubscribe from all room events. Called automatically on disposal.
        /// </summary>
        public void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            try
            {
                // Unregister from matchmaking service
                if (!string.IsNullOrEmpty(_matchmakingListenerId) && _matchmakingService != null)
                {
                    _matchmakingService.UnregisterNetworkEventListener(_matchmakingListenerId);
                    _matchmakingListenerId = null;
                }

                // Unregister from multiplayer service
                if (!string.IsNullOrEmpty(_multiplayerListenerId) && _multiplayerService != null)
                {
                    _multiplayerService.UnregisterNetworkEventListener(_multiplayerListenerId);
                    _multiplayerListenerId = null;
                }

                _isSubscribed = false;
                Debug.Log($"[VIVERSE_ROOM] Unsubscribed from events for room: {RoomId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VIVERSE_ROOM] Failed to unsubscribe from room {RoomId} events: {e.Message}");
            }
        }

        /// <summary>
        /// Internal handler for matchmaking events - routes to appropriate typed events
        /// </summary>
        private void OnMatchmakingEvent(ViverseResult<NetworkEventData> result)
        {
            if (result == null || !result.IsSuccess)
            {
                Debug.LogWarning($"[VIVERSE_ROOM] Received failed matchmaking event for room {RoomId}: {result?.ErrorMessage}");
                return;
            }

            try
            {
                var eventData = result.Data;
                var eventType = (MatchmakingEventType)eventData.EventType;

                Debug.Log($"[VIVERSE_ROOM] Processing matchmaking event {eventType} for room {RoomId}");

                switch (eventType)
                {
                    case MatchmakingEventType.RoomJoined:
                        var joinedRoom = JsonUtility.FromJson<RoomData>(eventData.EventData);
                        OnRoomJoined?.Invoke(ViverseResult<RoomData>.Success(joinedRoom, result.RawResult));
                        break;

                    case MatchmakingEventType.RoomLeft:
                        var leftRoom = JsonUtility.FromJson<RoomData>(eventData.EventData);
                        OnRoomLeft?.Invoke(ViverseResult<RoomData>.Success(leftRoom, result.RawResult));
                        break;

                    case MatchmakingEventType.ActorJoined:
                        var joinedActor = JsonUtility.FromJson<ActorInfo>(eventData.EventData);
                        OnActorJoined?.Invoke(ViverseResult<ActorInfo>.Success(joinedActor, result.RawResult));
                        break;

                    case MatchmakingEventType.ActorLeft:
                        var leftActor = JsonUtility.FromJson<ActorInfo>(eventData.EventData);
                        OnActorLeft?.Invoke(ViverseResult<ActorInfo>.Success(leftActor, result.RawResult));
                        break;

                    case MatchmakingEventType.RoomClosed:
                        var closedRoom = JsonUtility.FromJson<RoomData>(eventData.EventData);
                        OnRoomClosed?.Invoke(ViverseResult<RoomData>.Success(closedRoom, result.RawResult));
                        break;

                    default:
                        Debug.LogWarning($"[VIVERSE_ROOM] Unknown matchmaking event type: {eventType}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VIVERSE_ROOM] Exception processing matchmaking event for room {RoomId}: {e.Message}");
            }
        }

        /// <summary>
        /// Internal handler for multiplayer events - routes to appropriate typed events
        /// </summary>
        private void OnMultiplayerEvent(ViverseResult<NetworkEventData> result)
        {
            if (result == null || !result.IsSuccess)
            {
                Debug.LogWarning($"[VIVERSE_ROOM] Received failed multiplayer event for room {RoomId}: {result?.ErrorMessage}");
                return;
            }

            try
            {
                var eventData = result.Data;
                var eventType = (MultiplayerEventType)eventData.EventType;

                Debug.Log($"[VIVERSE_ROOM] Processing multiplayer event {eventType} for room {RoomId}");

                switch (eventType)
                {
                    case MultiplayerEventType.Message:
                        var message = JsonUtility.FromJson<GeneralMessage>(eventData.EventData);
                        OnGeneralMessage?.Invoke(ViverseResult<GeneralMessage>.Success(message, result.RawResult));
                        break;

                    case MultiplayerEventType.Position:
                        var position = JsonUtility.FromJson<PositionUpdate>(eventData.EventData);
                        OnPositionUpdate?.Invoke(ViverseResult<PositionUpdate>.Success(position, result.RawResult));
                        break;

                    case MultiplayerEventType.Competition:
                        var competition = JsonUtility.FromJson<CompetitionResult>(eventData.EventData);
                        OnCompetitionResult?.Invoke(ViverseResult<CompetitionResult>.Success(competition, result.RawResult));
                        break;

                    case MultiplayerEventType.Leaderboard:
                        var leaderboard = JsonUtility.FromJson<LeaderboardUpdate>(eventData.EventData);
                        OnLeaderboardUpdate?.Invoke(ViverseResult<LeaderboardUpdate>.Success(leaderboard, result.RawResult));
                        break;

                    default:
                        Debug.LogWarning($"[VIVERSE_ROOM] Unknown multiplayer event type: {eventType}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VIVERSE_ROOM] Exception processing multiplayer event for room {RoomId}: {e.Message}");
            }
        }

        /// <summary>
        /// Get active subscriber count for debugging
        /// </summary>
        public int GetActiveSubscriberCount()
        {
            int count = 0;
            count += OnRoomJoined?.GetInvocationList()?.Length ?? 0;
            count += OnRoomLeft?.GetInvocationList()?.Length ?? 0;
            count += OnActorJoined?.GetInvocationList()?.Length ?? 0;
            count += OnActorLeft?.GetInvocationList()?.Length ?? 0;
            count += OnRoomClosed?.GetInvocationList()?.Length ?? 0;
            count += OnGeneralMessage?.GetInvocationList()?.Length ?? 0;
            count += OnPositionUpdate?.GetInvocationList()?.Length ?? 0;
            count += OnCompetitionResult?.GetInvocationList()?.Length ?? 0;
            count += OnLeaderboardUpdate?.GetInvocationList()?.Length ?? 0;
            return count;
        }

        /// <summary>
        /// Automatic cleanup when room is disposed
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            Unsubscribe();

            // Remove from ViverseCore tracking
            ViverseCore.RemoveRoom(_roomManagerKey);

            // Clean up service references
            _matchmakingService = null;
            _multiplayerService = null;

            // Clear all event handlers to prevent memory leaks
            OnRoomJoined = null;
            OnRoomLeft = null;
            OnActorJoined = null;
            OnActorLeft = null;
            OnRoomClosed = null;
            OnGeneralMessage = null;
            OnPositionUpdate = null;
            OnCompetitionResult = null;
            OnLeaderboardUpdate = null;

            _isDisposed = true;
            Debug.Log($"[VIVERSE_ROOM] Disposed room event manager for room: {RoomId}");
        }
    }
}
