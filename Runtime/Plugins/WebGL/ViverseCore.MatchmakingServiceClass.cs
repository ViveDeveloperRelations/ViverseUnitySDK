using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using AOT;

namespace ViverseWebGLAPI
{
    public partial class ViverseCore
    {
        /// <summary>
        /// Service class for managing Viverse matchmaking operations and room management
        /// </summary>
        public class MatchmakingServiceClass
        {
            // DllImport declarations for matchmaking operations
            [DllImport("__Internal")]
            private static extern void Matchmaking_SetActor(string actorJson, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Matchmaking_CreateRoom(string roomJson, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Matchmaking_JoinRoom(string roomId, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Matchmaking_LeaveRoom(int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Matchmaking_CloseRoom(int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Matchmaking_GetAvailableRooms(int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Matchmaking_GetMyRoomActors(int taskId, Action<string> callback);

            // DllImport declarations for event management
            [DllImport("__Internal")]
            private static extern int Matchmaking_RegisterEventListener(int eventType, string listenerId);

            [DllImport("__Internal")]
            private static extern int Matchmaking_UnregisterEventListener(string listenerId);

            [DllImport("__Internal")]
            private static extern void ViverseEventDispatcher_RegisterMatchmakingRawCallback(int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern int ViverseEventDispatcher_RegisterSDKEventCallback(Action<string> callback);

            [DllImport("__Internal")]
            private static extern string ViverseCore_GetSDKState();

            // Event declarations
            public event Action<string> OnRoomJoined;
            public event Action<string> OnRoomLeft;
            public event Action<string> OnActorJoined;
            public event Action<string> OnActorLeft;
            public event Action<string> OnRoomClosed;

            // Raw SDK event declarations (critical for proper SDK flow) - strongly-typed with ViverseResult pattern
            public event Action<ViverseResult<ConnectionEventData>> OnConnect;
            public event Action<ViverseResult<LobbyEventData>> OnJoinedLobby;
            public event Action<ViverseResult<RoomJoinEventData>> OnJoinRoom;
            public event Action<ViverseResult<RoomListEventData>> OnRoomListUpdate;
            public event Action<ViverseResult<RoomActorEventData>> OnRoomActorChange;
            public event Action<ViverseResult<SDKErrorEventData>> OnError;
            public event Action<ViverseResult<StateChangeEventData>> OnStateChange;

            private bool _actorSet = false;
            private string _currentRoomId = null;
            private bool _rawEventCallbackRegistered = false;

            /// <summary>
            /// Constructor - initializes raw SDK event handling
            /// </summary>
            public MatchmakingServiceClass()
            {
                // Note: RegisterRawEventCallbackAsync() should be called when the service is initialized
                // Changed to async pattern - call InitializeAsync() to register callbacks
            }

            /// <summary>
            /// Initialize the matchmaking service with standardized async pattern
            /// </summary>
            /// <returns>Result indicating success or failure</returns>
            public Task<ViverseResult<bool>> InitializeAsync()
            {
                try
                {
                    RegisterRawEventCallback(); // No longer async - direct static registration

                    var result = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.Success,
                        Message = "Matchmaking service initialized successfully"
                    };

                    return Task.FromResult(ViverseResult<bool>.Success(true, result));
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during matchmaking service initialization: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during initialization: {e.Message}"
                    };
                    return Task.FromResult(ViverseResult<bool>.Failure(errorResult));
                }
            }

            /// <summary>
            /// Set actor information for this player session using strongly-typed data structure
            /// </summary>
            /// <param name="actorInfo">Strongly-typed actor information with direct level/skill properties</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> SetActor(ActorInfo actorInfo)
            {
                if (actorInfo == null)
                {
                    var nullResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Actor info cannot be null"
                    };
                    return ViverseResult<bool>.Failure(nullResult);
                }

                if (string.IsNullOrEmpty(actorInfo.session_id) || string.IsNullOrEmpty(actorInfo.name))
                {
                    var invalidResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Actor session_id and name are required"
                    };
                    return ViverseResult<bool>.Failure(invalidResult);
                }

                try
                {
                    string actorJson = JsonUtility.ToJson(actorInfo);

                    void ActorWrapper(int taskId, Action<string> callback)
                    {
                        Matchmaking_SetActor(actorJson, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(ActorWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        _actorSet = true;
                        Debug.Log($"Actor set successfully: {actorInfo.name} (Session: {actorInfo.session_id}, Level: {actorInfo.level}, Skill: {actorInfo.skill})");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        result.LogError("Set Actor");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during SetActor: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during SetActor: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }


            /// <summary>
            /// Create a new multiplayer room using strongly-typed data structure
            /// </summary>
            /// <param name="roomInfo">Strongly-typed room configuration with direct property fields</param>
            /// <returns>Result containing the created room data</returns>
            public async Task<ViverseResult<RoomData>> CreateRoom(RoomInfo roomInfo)
            {
                if (!_actorSet)
                {
                    var notSetResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Actor not set. Call SetActor() first."
                    };
                    return ViverseResult<RoomData>.Failure(notSetResult);
                }

                if (roomInfo == null)
                {
                    var nullResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Room info cannot be null"
                    };
                    return ViverseResult<RoomData>.Failure(nullResult);
                }

                if (string.IsNullOrEmpty(roomInfo.name) || string.IsNullOrEmpty(roomInfo.mode))
                {
                    var invalidResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Room name and mode are required"
                    };
                    return ViverseResult<RoomData>.Failure(invalidResult);
                }

                try
                {
                    string roomJson = JsonUtility.ToJson(roomInfo);

                    void RoomWrapper(int taskId, Action<string> callback)
                    {
                        Matchmaking_CreateRoom(roomJson, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(RoomWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        if (string.IsNullOrEmpty(result.Payload))
                        {
                            Debug.LogError("CreateRoom succeeded but received null or empty payload");
                            var emptyResult = new ViverseSDKReturn
                            {
                                ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                Message = "CreateRoom succeeded but received null or empty payload"
                            };
                            return ViverseResult<RoomData>.Failure(emptyResult);
                        }

                        try
                        {
                            var roomData = JsonUtility.FromJson<RoomData>(result.Payload);
                            if (roomData == null)
                            {
                                Debug.LogError("Failed to parse room data from payload");
                                var parseResult = new ViverseSDKReturn
                                {
                                    ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                    Message = "Failed to parse room data from payload"
                                };
                                return ViverseResult<RoomData>.Failure(parseResult);
                            }

                            _currentRoomId = roomData.roomId;
                            Debug.Log($"Room created successfully: {roomData.roomId} (Name: {roomInfo.name}, Mode: {roomInfo.mode}, Max: {roomInfo.maxPlayers})");
                            return ViverseResult<RoomData>.Success(roomData, result);
                        }
                        catch (Exception parseException)
                        {
                            Debug.LogError($"Exception parsing room data: {parseException.Message}");
                            var parseResult = new ViverseSDKReturn
                            {
                                ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                Message = $"Exception parsing room data: {parseException.Message}"
                            };
                            return ViverseResult<RoomData>.Failure(parseResult);
                        }
                    }
                    else
                    {
                        result.LogError("Create Room");
                        return ViverseResult<RoomData>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during CreateRoom: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during CreateRoom: {e.Message}"
                    };
                    return ViverseResult<RoomData>.Failure(errorResult);
                }
            }


            /// <summary>
            /// Join an existing multiplayer room
            /// </summary>
            /// <param name="roomId">The ID of the room to join</param>
            /// <returns>Result containing the joined room data</returns>
            public async Task<ViverseResult<RoomData>> JoinRoom(string roomId)
            {
                if (!_actorSet)
                {
                    var notSetResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Actor not set. Call SetActor() first."
                    };
                    return ViverseResult<RoomData>.Failure(notSetResult);
                }

                if (string.IsNullOrEmpty(roomId))
                {
                    var invalidResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Room ID cannot be null or empty"
                    };
                    return ViverseResult<RoomData>.Failure(invalidResult);
                }

                try
                {
                    void JoinWrapper(int taskId, Action<string> callback)
                    {
                        Matchmaking_JoinRoom(roomId, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(JoinWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        if (string.IsNullOrEmpty(result.Payload))
                        {
                            Debug.LogError("JoinRoom succeeded but received null or empty payload");
                            var emptyResult = new ViverseSDKReturn
                            {
                                ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                Message = "JoinRoom succeeded but received null or empty payload"
                            };
                            return ViverseResult<RoomData>.Failure(emptyResult);
                        }

                        try
                        {
                            var roomData = JsonUtility.FromJson<RoomData>(result.Payload);
                            if (roomData == null)
                            {
                                Debug.LogError("Failed to parse room data from payload");
                                var parseResult = new ViverseSDKReturn
                                {
                                    ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                    Message = "Failed to parse room data from payload"
                                };
                                return ViverseResult<RoomData>.Failure(parseResult);
                            }

                            _currentRoomId = roomData.roomId;
                            Debug.Log($"Room joined successfully: {roomId}");
                            return ViverseResult<RoomData>.Success(roomData, result);
                        }
                        catch (Exception parseException)
                        {
                            Debug.LogError($"Exception parsing room data: {parseException.Message}");
                            var parseResult = new ViverseSDKReturn
                            {
                                ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                Message = $"Exception parsing room data: {parseException.Message}"
                            };
                            return ViverseResult<RoomData>.Failure(parseResult);
                        }
                    }
                    else
                    {
                        result.LogError("Join Room");
                        return ViverseResult<RoomData>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during JoinRoom: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during JoinRoom: {e.Message}"
                    };
                    return ViverseResult<RoomData>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Leave the current room
            /// </summary>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> LeaveRoom()
            {
                if (string.IsNullOrEmpty(_currentRoomId))
                {
                    var notInRoomResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidState,
                        Message = "Not currently in a room"
                    };
                    return ViverseResult<bool>.Failure(notInRoomResult);
                }

                try
                {
                    var result = await CallNativeViverseFunction(Matchmaking_LeaveRoom);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        _currentRoomId = null;
                        Debug.Log("Left room successfully");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        result.LogError("Leave Room");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during LeaveRoom: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during LeaveRoom: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Close the current room (only room owner can do this)
            /// </summary>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> CloseRoom()
            {
                if (string.IsNullOrEmpty(_currentRoomId))
                {
                    var notInRoomResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidState,
                        Message = "Not currently in a room"
                    };
                    return ViverseResult<bool>.Failure(notInRoomResult);
                }

                try
                {
                    var result = await CallNativeViverseFunction(Matchmaking_CloseRoom);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        _currentRoomId = null;
                        Debug.Log("Room closed successfully");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        result.LogError("Close Room");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during CloseRoom: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during CloseRoom: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Get list of available rooms to join
            /// </summary>
            /// <returns>Result containing array of available room data</returns>
            public async Task<ViverseResult<RoomData[]>> GetAvailableRooms()
            {
                try
                {
                    var result = await CallNativeViverseFunction(Matchmaking_GetAvailableRooms);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        try
                        {
                            RoomData[] rooms = string.IsNullOrEmpty(result.Payload) ?
	                            new RoomData[0] : JsonUtility.FromJson<RoomData[]>(result.Payload);
                            Debug.Log($"Retrieved {rooms.Length} available rooms");
                            return ViverseResult<RoomData[]>.Success(rooms, result);
                        }
                        catch (Exception parseException)
                        {
                            Debug.LogError($"Exception parsing room list: {parseException.Message}");
                            // Return empty array on parse failure
                            return ViverseResult<RoomData[]>.Success(new RoomData[0], result);
                        }
                    }
                    else
                    {
                        result.LogError("Get Available Rooms");
                        return ViverseResult<RoomData[]>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during GetAvailableRooms: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during GetAvailableRooms: {e.Message}"
                    };
                    return ViverseResult<RoomData[]>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Get list of actors in the current room
            /// </summary>
            /// <returns>Result containing array of actor information</returns>
            public async Task<ViverseResult<ActorInfo[]>> GetMyRoomActors()
            {
                if (string.IsNullOrEmpty(_currentRoomId))
                {
                    var notInRoomResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidState,
                        Message = "Not currently in a room"
                    };
                    return ViverseResult<ActorInfo[]>.Failure(notInRoomResult);
                }

                try
                {
                    var result = await CallNativeViverseFunction(Matchmaking_GetMyRoomActors);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        try
                        {
                            var actors = string.IsNullOrEmpty(result.Payload) ?
	                            new ActorInfo[0] : JsonUtility.FromJson<ActorInfo[]>(result.Payload);
                            Debug.Log($"Retrieved {actors.Length} actors in current room");
                            return ViverseResult<ActorInfo[]>.Success(actors, result);
                        }
                        catch (Exception parseException)
                        {
                            Debug.LogError($"Exception parsing actor list: {parseException.Message}");
                            // Return empty array on parse failure
                            return ViverseResult<ActorInfo[]>.Success(new ActorInfo[0], result);
                        }
                    }
                    else
                    {
                        result.LogError("Get Room Actors");
                        return ViverseResult<ActorInfo[]>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during GetMyRoomActors: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during GetMyRoomActors: {e.Message}"
                    };
                    return ViverseResult<ActorInfo[]>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Register an event listener for matchmaking events
            /// </summary>
            /// <param name="eventType">Type of event to listen for</param>
            /// <param name="listenerId">Unique identifier for this listener</param>
            /// <returns>Result indicating success or failure</returns>
            public ViverseResult<bool> RegisterEventListener(MatchmakingEventType eventType, string listenerId)
            {
                if (!eventType.IsValidEventType())
                {
                    var invalidEventResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = $"Invalid event type: {eventType}. Cannot be UNSET_VALUE."
                    };
                    return ViverseResult<bool>.Failure(invalidEventResult);
                }

                if (string.IsNullOrEmpty(listenerId))
                {
                    var invalidIdResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Listener ID cannot be null or empty"
                    };
                    return ViverseResult<bool>.Failure(invalidIdResult);
                }

                try
                {
                    int returnCode = Matchmaking_RegisterEventListener((int)eventType, listenerId);
                    var result = new ViverseSDKReturn
                    {
                        ReturnCode = returnCode,
                        Message = returnCode == (int)ViverseSDKReturnCode.Success
                            ? "Event listener registered successfully"
                            : ReturnCodeHelper.GetErrorMessage((ViverseSDKReturnCode)returnCode)
                    };

                    if (returnCode == (int)ViverseSDKReturnCode.Success)
                    {
                        Debug.Log($"Registered event listener for {eventType.ToLogString()}: {listenerId}");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        result.LogError("Register Event Listener");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during RegisterEventListener: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during RegisterEventListener: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Unregister an event listener
            /// </summary>
            /// <param name="listenerId">The ID of the listener to remove</param>
            /// <returns>Result indicating success or failure</returns>
            public ViverseResult<bool> UnregisterEventListener(string listenerId)
            {
                if (string.IsNullOrEmpty(listenerId))
                {
                    var invalidIdResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Listener ID cannot be null or empty"
                    };
                    return ViverseResult<bool>.Failure(invalidIdResult);
                }

                try
                {
                    int returnCode = Matchmaking_UnregisterEventListener(listenerId);
                    var result = new ViverseSDKReturn
                    {
                        ReturnCode = returnCode,
                        Message = returnCode == (int)ViverseSDKReturnCode.Success
                            ? "Event listener unregistered successfully"
                            : ReturnCodeHelper.GetErrorMessage((ViverseSDKReturnCode)returnCode)
                    };

                    if (returnCode == (int)ViverseSDKReturnCode.Success)
                    {
                        Debug.Log($"Unregistered event listener: {listenerId}");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        result.LogError("Unregister Event Listener");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during UnregisterEventListener: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during UnregisterEventListener: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Get the current room ID if in a room
            /// </summary>
            public string CurrentRoomId => _currentRoomId;

            /// <summary>
            /// Check if actor information has been set
            /// </summary>
            public bool IsActorSet => _actorSet;

            /// <summary>
            /// Check if currently in a room
            /// </summary>
            public bool IsInRoom => !string.IsNullOrEmpty(_currentRoomId);

            /// <summary>
            /// Get the current SDK state as a strongly-typed ViverseResult for reliable state monitoring
            /// </summary>
            /// <returns>ViverseResult containing parsed SDK state information</returns>
            public ViverseResult<SDKStateInfo> GetSDKStateInfo()
            {
                try
                {
                    string stateJson = ViverseCore_GetSDKState();

                    if (string.IsNullOrEmpty(stateJson))
                    {
                        return ViverseResult<SDKStateInfo>.Failure(ViverseSDKReturnCode.ErrorModuleNotLoaded, "SDK state not available - service may not be initialized");
                    }

                    // Check for error in JSON response
                    if (stateJson.Contains("\"error\""))
                    {
                        return ViverseResult<SDKStateInfo>.Failure(ViverseSDKReturnCode.ErrorUnknown, "SDK state returned error response");
                    }

                    try
                    {
                        SDKStateInfo stateInfo = JsonUtility.FromJson<SDKStateInfo>(stateJson);

                        if (stateInfo == null)
                        {
                            return ViverseResult<SDKStateInfo>.Failure(ViverseSDKReturnCode.ErrorUnknown, "Failed to parse SDK state JSON");
                        }

                        Debug.Log($"[SDK_STATE_INFO] Successfully parsed SDK state - Connected: {stateInfo.connected}, ActorSet: {stateInfo.actorSet}, RoomReady: {stateInfo.isReadyForRoomOperations}");
                        return ViverseResult<SDKStateInfo>.Success(stateInfo);
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogError($"Failed to parse SDK state JSON: {parseEx.Message}");
                        return ViverseResult<SDKStateInfo>.Failure(ViverseSDKReturnCode.ErrorUnknown, $"Failed to parse SDK state: {parseEx.Message}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception getting SDK state info: {e.Message}");
                    return ViverseResult<SDKStateInfo>.Failure(ViverseSDKReturnCode.ErrorException, $"Exception getting SDK state: {e.Message}");
                }
            }

            /// <summary>
            /// Register a network event listener for matchmaking events using ViverseResult<T> pattern
            /// </summary>
            /// <param name="eventType">The matchmaking event type to listen for</param>
            /// <param name="callback">Callback using standard ViverseResult<NetworkEventData> pattern</param>
            /// <returns>TaskId for managing the listener</returns>
            public string RegisterNetworkEventListener(MatchmakingEventType eventType, Action<ViverseResult<NetworkEventData>> callback)
            {
                return ViverseEventDispatcher.RegisterMatchmakingListener(eventType, callback);
            }

            /// <summary>
            /// Unregister a network event listener by TaskId
            /// </summary>
            /// <param name="taskId">The TaskId returned from RegisterNetworkEventListener</param>
            /// <returns>True if successfully unregistered</returns>
            public bool UnregisterNetworkEventListener(string taskId)
            {
                return ViverseEventDispatcher.UnregisterListener(taskId);
            }

            /// <summary>
            /// Reset the matchmaking service state (called during logout/cleanup)
            /// </summary>
            internal void Reset()
            {
                _actorSet = false;
                _currentRoomId = null;
                Debug.Log("Matchmaking service reset");
            }

            /// <summary>
            /// Register the raw event callback to receive SDK events from JavaScript using DIRECT static registration
            /// This is NOT an async operation - it's a direct static callback registration
            /// </summary>
            private void RegisterRawEventCallback()
            {
                if (_rawEventCallbackRegistered)
                {
                    Debug.LogWarning("Raw event callback already registered");
                    return;
                }

                try
                {
                    // Use DIRECT static registration instead of async pattern
                    // This registers OnMatchmakingRawEvent as the static callback for SDK events
                    int returnCode = ViverseEventDispatcher_RegisterSDKEventCallback(OnMatchmakingRawEvent);

                    if (returnCode == (int)ViverseSDKReturnCode.Success)
                    {
                        _rawEventCallbackRegistered = true;
                        Debug.Log("Matchmaking raw event callback registered successfully using DIRECT static registration");
                    }
                    else
                    {
                        Debug.LogError($"Failed to register raw event callback. Return code: {returnCode}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception registering matchmaking raw event callback: {e.Message}");
                }
            }

            /// <summary>
            /// Callback method invoked by JavaScript for raw SDK events using standardized pattern
            /// This handles strongly-typed SDKEventData from JavaScript (separate from async TaskId responses)
            /// </summary>
            /// <param name="eventMessage">Event message JSON string containing SDKEventData (using standard string callback)</param>
            [MonoPInvokeCallback(typeof(Action<string>))]
            private static void OnMatchmakingRawEvent(string eventMessage)
            {
                try
                {
                    if (string.IsNullOrEmpty(eventMessage))
                    {
                        Debug.LogWarning("Received empty raw matchmaking event message");
                        return;
                    }

                    Debug.Log($"[RAW_SDK_EVENT] Received event message: {eventMessage}");

                    // Parse as strongly-typed SDKEventData (new format)
                    SDKEventData sdkEventData = null;
                    SDKEventType eventType = SDKEventType.UNSET_VALUE;
                    string eventData = "";

                    try
                    {
                        sdkEventData = JsonUtility.FromJson<SDKEventData>(eventMessage);
                        if (sdkEventData != null && sdkEventData.EventType > 0)
                        {
                            eventType = (SDKEventType)sdkEventData.EventType;
                            eventData = sdkEventData.EventData ?? "";
                            Debug.Log($"[RAW_SDK_EVENT] Parsed strongly-typed: {eventType.ToLogString()} ({(int)eventType}) with data: {eventData}");
                        }
                        else
                        {
                            Debug.LogWarning($"[RAW_SDK_EVENT] Invalid SDKEventData structure: {eventMessage}");
                            return;
                        }
                    }
                    catch (Exception parseException)
                    {
                        Debug.LogWarning($"[RAW_SDK_EVENT] Failed to parse as SDKEventData: {parseException.Message}");

                        // Fallback to old string-based format for compatibility: "eventName|eventData"
                        string[] parts = eventMessage.Split('|', 2);
                        if (parts.Length == 2)
                        {
                            string eventName = parts[0];
                            eventData = parts[1];

                            try
                            {
                                eventType = EventTypeExtensions.ParseSDKEventType(eventName);
                                Debug.Log($"[RAW_SDK_EVENT] Parsed legacy format: {eventName} -> {eventType.ToLogString()} with data: {eventData}");
                            }
                            catch
                            {
                                Debug.LogWarning($"[RAW_SDK_EVENT] Failed to parse event type from legacy format: {eventName}");
                                return;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[RAW_SDK_EVENT] Invalid event message format (not SDKEventData or legacy): {eventMessage}");
                            return;
                        }
                    }

                    // Process the parsed event (this is static event dispatching, NOT async responses)
                    switch (eventType)
                    {
                        case SDKEventType.OnConnect:
                            Debug.Log("[RAW_SDK_EVENT] ✅ SDK Connected - ready for operations");
                            break;
                        case SDKEventType.OnJoinedLobby:
                            Debug.Log("[RAW_SDK_EVENT] ✅ Joined lobby - can now see available rooms");
                            break;
                        case SDKEventType.OnJoinRoom:
                            Debug.Log($"[RAW_SDK_EVENT] ✅ Joined room: {eventData}");
                            break;
                        case SDKEventType.OnRoomListUpdate:
                            Debug.Log($"[RAW_SDK_EVENT] 📋 Room list updated: {eventData}");
                            break;
                        case SDKEventType.OnRoomActorChange:
                            Debug.Log($"[RAW_SDK_EVENT] 👥 Room actors changed: {eventData}");
                            break;
                        case SDKEventType.OnRoomClosed:
                            Debug.Log($"[RAW_SDK_EVENT] 🚪 Room closed: {eventData}");
                            break;
                        case SDKEventType.OnError:
                            Debug.LogError($"[RAW_SDK_EVENT] ❌ SDK Error: {eventData}");
                            break;
                        case SDKEventType.StateChange:
                            Debug.Log($"[RAW_SDK_EVENT] 🔄 State changed: {eventData}");
                            break;
                        default:
                            Debug.LogWarning($"[RAW_SDK_EVENT] ❓ Unknown event type: {eventType} ({(int)eventType})");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception in OnMatchmakingRawEvent: {e.Message}");
                    Debug.LogError($"Stack trace: {e.StackTrace}");
                    Debug.LogError($"Event message was: {eventMessage ?? "NULL"}");
                }
            }

            /// <summary>
            /// Internal method to dispatch events from JavaScript to C# events
            /// </summary>
            internal void DispatchEvent(MatchmakingEventType eventType, string eventData)
            {
                try
                {
                    // ROBUST LOGGING: Log all matchmaking event details
                    Debug.Log($"[MATCHMAKING_DISPATCH] Event type: {eventType.ToLogString()} ({(int)eventType})");
                    Debug.Log($"[MATCHMAKING_RAW_DATA] Raw event data: {eventData ?? "NULL"}");

                    // Attempt to parse data for additional context
                    if (!string.IsNullOrEmpty(eventData))
                    {
                        try
                        {
                            switch (eventType)
                            {
                                case MatchmakingEventType.RoomJoined:
                                case MatchmakingEventType.RoomLeft:
                                    var roomData = JsonUtility.FromJson<RoomData>(eventData);
                                    if (roomData != null)
                                    {
                                        Debug.Log($"[MATCHMAKING_PARSED] Room event - ID: {roomData.roomId ?? roomData.id}, Name: {roomData.name}, Players: {roomData.currentPlayers}/{roomData.maxPlayers}");
                                    }
                                    break;
                                case MatchmakingEventType.ActorJoined:
                                case MatchmakingEventType.ActorLeft:
                                    var actorData = JsonUtility.FromJson<ActorInfo>(eventData);
                                    if (actorData != null)
                                    {
                                        Debug.Log($"[MATCHMAKING_PARSED] Actor event - ID: {actorData.session_id}, Name: {actorData.name}");
                                    }
                                    break;
                            }
                        }
                        catch (Exception parseEx)
                        {
                            Debug.LogWarning($"[MATCHMAKING_PARSE_FAILED] Could not parse event data for type {eventType}: {parseEx.Message}");
                        }
                    }

                    switch (eventType)
                    {
                        case MatchmakingEventType.RoomJoined:
                            Debug.Log($"[MATCHMAKING_INVOKE] Invoking OnRoomJoined with {OnRoomJoined?.GetInvocationList()?.Length ?? 0} subscribers");
                            OnRoomJoined?.Invoke(eventData);
                            break;
                        case MatchmakingEventType.RoomLeft:
                            Debug.Log($"[MATCHMAKING_INVOKE] Invoking OnRoomLeft with {OnRoomLeft?.GetInvocationList()?.Length ?? 0} subscribers");
                            OnRoomLeft?.Invoke(eventData);
                            break;
                        case MatchmakingEventType.ActorJoined:
                            Debug.Log($"[MATCHMAKING_INVOKE] Invoking OnActorJoined with {OnActorJoined?.GetInvocationList()?.Length ?? 0} subscribers");
                            OnActorJoined?.Invoke(eventData);
                            break;
                        case MatchmakingEventType.ActorLeft:
                            Debug.Log($"[MATCHMAKING_INVOKE] Invoking OnActorLeft with {OnActorLeft?.GetInvocationList()?.Length ?? 0} subscribers");
                            OnActorLeft?.Invoke(eventData);
                            break;
                        case MatchmakingEventType.RoomClosed:
                            Debug.Log($"[MATCHMAKING_INVOKE] Invoking OnRoomClosed with {OnRoomClosed?.GetInvocationList()?.Length ?? 0} subscribers");
                            OnRoomClosed?.Invoke(eventData);
                            break;
                        default:
                            Debug.LogWarning($"[MATCHMAKING_UNKNOWN] Unknown matchmaking event type: {eventType} ({(int)eventType})");
                            Debug.LogWarning($"[MATCHMAKING_UNKNOWN] Valid types are: {string.Join(", ", Enum.GetValues(typeof(MatchmakingEventType)).Cast<MatchmakingEventType>().Select(e => $"{e}({(int)e})"))}");
                            break;
                    }

                    Debug.Log($"[MATCHMAKING_COMPLETE] Successfully dispatched {eventType.ToLogString()} event");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MATCHMAKING_EXCEPTION] Exception dispatching matchmaking event {eventType}: {e.Message}");
                    Debug.LogError($"[MATCHMAKING_EXCEPTION] Stack trace: {e.StackTrace}");
                    Debug.LogError($"[MATCHMAKING_EXCEPTION] Event data was: {eventData ?? "NULL"}");
                }
            }
        }
    }
}
