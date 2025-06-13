using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ViverseWebGLAPI
{
    public partial class ViverseCore
    {
        /// <summary>
        /// Service class for managing Viverse real-time multiplayer communication
        /// </summary>
        public class MultiplayerServiceClass
        {
            // DllImport declarations for multiplayer initialization
            [DllImport("__Internal")]
            private static extern void Multiplayer_Initialize(string roomId, string appId, int taskId, Action<string> callback);

            // DllImport declarations for communication (standardized async pattern)
            [DllImport("__Internal")]
            private static extern void Multiplayer_SendMessage(string messageContent, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Multiplayer_UpdateMyPosition(string positionJson, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Multiplayer_UpdateEntityPosition(string entityId, string positionJson, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Multiplayer_SendCompetition(string actionName, string actionMessage, string actionId, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Multiplayer_UpdateLeaderboard(float score, int taskId, Action<string> callback);

            // DllImport declarations for event listener management (standardized async pattern)
            [DllImport("__Internal")]
            private static extern void Multiplayer_RegisterMessageListener(string listenerId, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Multiplayer_RegisterPositionListener(string listenerId, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Multiplayer_RegisterCompetitionListener(string listenerId, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Multiplayer_RegisterLeaderboardListener(string listenerId, int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Multiplayer_UnregisterListener(string listenerId, int taskId, Action<string> callback);

            // Event declarations
            public event Action<GeneralMessage> OnMessage;
            public event Action<PositionUpdate> OnPositionUpdate;
            public event Action<CompetitionResult> OnCompetitionResult;
            public event Action<LeaderboardUpdate> OnLeaderboardUpdate;
            
            #pragma warning disable CS0067 // Event is never used - reserved for future functionality
            public event Action<RemoveNotification> OnRemoveNotification;
            #pragma warning restore CS0067

            private bool _isInitialized = false;
            private string _roomId = null;
            private string _appId = null;
            private MultiplayerSessionInfo _sessionInfo = null;

            /// <summary>
            /// Initialize the multiplayer client for real-time communication
            /// </summary>
            /// <param name="roomId">The room ID to connect to</param>
            /// <param name="appId">The application ID</param>
            /// <returns>Result containing session information</returns>
            public async Task<ViverseResult<MultiplayerSessionInfo>> Initialize(string roomId, string appId)
            {
                if (string.IsNullOrEmpty(roomId))
                {
                    var invalidRoomResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Room ID cannot be null or empty"
                    };
                    return ViverseResult<MultiplayerSessionInfo>.Failure(invalidRoomResult);
                }

                if (string.IsNullOrEmpty(appId))
                {
                    var invalidAppResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "App ID cannot be null or empty"
                    };
                    return ViverseResult<MultiplayerSessionInfo>.Failure(invalidAppResult);
                }

                try
                {
                    void InitWrapper(int taskId, Action<string> callback)
                    {
                        Multiplayer_Initialize(roomId, appId, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(InitWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        if (string.IsNullOrEmpty(result.Payload))
                        {
                            Debug.LogError("Multiplayer initialization succeeded but received null or empty payload");
                            var emptyResult = new ViverseSDKReturn
                            {
                                ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                Message = "Multiplayer initialization succeeded but received null or empty payload"
                            };
                            return ViverseResult<MultiplayerSessionInfo>.Failure(emptyResult);
                        }

                        try
                        {
                            _sessionInfo = JsonUtility.FromJson<MultiplayerSessionInfo>(result.Payload);
                            if (_sessionInfo == null)
                            {
                                Debug.LogError("Failed to parse multiplayer session info from payload");
                                var parseResult = new ViverseSDKReturn
                                {
                                    ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                    Message = "Failed to parse multiplayer session info from payload"
                                };
                                return ViverseResult<MultiplayerSessionInfo>.Failure(parseResult);
                            }

                            _roomId = roomId;
                            _appId = appId;
                            _isInitialized = true;
                            Debug.Log($"Multiplayer client initialized successfully for room: {roomId}");
                            return ViverseResult<MultiplayerSessionInfo>.Success(_sessionInfo, result);
                        }
                        catch (Exception parseException)
                        {
                            Debug.LogError($"Exception parsing session info: {parseException.Message}");
                            var parseResult = new ViverseSDKReturn
                            {
                                ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                                Message = $"Exception parsing session info: {parseException.Message}"
                            };
                            return ViverseResult<MultiplayerSessionInfo>.Failure(parseResult);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to initialize multiplayer client: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
                        return ViverseResult<MultiplayerSessionInfo>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during multiplayer initialization: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during multiplayer initialization: {e.Message}"
                    };
                    return ViverseResult<MultiplayerSessionInfo>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Send a general message to all players in the room
            /// </summary>
            /// <param name="messageContent">The message content as a string (caller responsible for serialization)</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> SendMessage(string messageContent)
            {
                if (!_isInitialized)
                {
                    var notInitializedResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Multiplayer client not initialized. Call Initialize() first."
                    };
                    return ViverseResult<bool>.Failure(notInitializedResult);
                }

                if (string.IsNullOrEmpty(messageContent))
                {
                    var nullMessageResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Message content cannot be null or empty"
                    };
                    return ViverseResult<bool>.Failure(nullMessageResult);
                }

                try
                {
                    void SendWrapper(int taskId, Action<string> callback)
                    {
                        Multiplayer_SendMessage(messageContent, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(SendWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        Debug.Log("General message sent successfully");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to send message: {result.Message}");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during SendMessage: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during SendMessage: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Update this player's position for network synchronization
            /// </summary>
            /// <param name="positionJson">The position data as JSON string (caller responsible for serialization)</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> UpdateMyPosition(string positionJson)
            {
                if (!_isInitialized)
                {
                    var notInitializedResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Multiplayer client not initialized. Call Initialize() first."
                    };
                    return ViverseResult<bool>.Failure(notInitializedResult);
                }

                if (string.IsNullOrEmpty(positionJson))
                {
                    var nullPositionResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Position JSON cannot be null or empty"
                    };
                    return ViverseResult<bool>.Failure(nullPositionResult);
                }

                try
                {
                    void UpdateWrapper(int taskId, Action<string> callback)
                    {
                        Multiplayer_UpdateMyPosition(positionJson, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(UpdateWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to update position: {result.Message}");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during UpdateMyPosition: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during UpdateMyPosition: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Update an entity's position for network synchronization
            /// </summary>
            /// <param name="entityId">The entity identifier</param>
            /// <param name="positionJson">The position data as JSON string (caller responsible for serialization)</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> UpdateEntityPosition(string entityId, string positionJson)
            {
                if (!_isInitialized)
                {
                    var notInitializedResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Multiplayer client not initialized. Call Initialize() first."
                    };
                    return ViverseResult<bool>.Failure(notInitializedResult);
                }

                if (string.IsNullOrEmpty(entityId))
                {
                    var invalidEntityResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Entity ID cannot be null or empty"
                    };
                    return ViverseResult<bool>.Failure(invalidEntityResult);
                }

                if (string.IsNullOrEmpty(positionJson))
                {
                    var nullPositionResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Position JSON cannot be null or empty"
                    };
                    return ViverseResult<bool>.Failure(nullPositionResult);
                }

                try
                {
                    void UpdateWrapper(int taskId, Action<string> callback)
                    {
                        Multiplayer_UpdateEntityPosition(entityId, positionJson, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(UpdateWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to update entity position: {result.Message}");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during UpdateEntityPosition: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during UpdateEntityPosition: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Send a competition action for action synchronization
            /// </summary>
            /// <param name="actionName">The name of the action</param>
            /// <param name="actionMessage">The action message/data</param>
            /// <param name="actionId">The unique action identifier</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> SendCompetition(string actionName, string actionMessage, string actionId)
            {
                if (!_isInitialized)
                {
                    var notInitializedResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Multiplayer client not initialized. Call Initialize() first."
                    };
                    return ViverseResult<bool>.Failure(notInitializedResult);
                }

                if (string.IsNullOrEmpty(actionName))
                {
                    var invalidActionResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "Action name cannot be null or empty"
                    };
                    return ViverseResult<bool>.Failure(invalidActionResult);
                }

                try
                {
                    void CompetitionWrapper(int taskId, Action<string> callback)
                    {
                        Multiplayer_SendCompetition(actionName, actionMessage ?? "", actionId ?? "", taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(CompetitionWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        Debug.Log($"Competition sent successfully: {actionName}");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to send competition: {result.Message}");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during SendCompetition: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during SendCompetition: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Update the real-time leaderboard with a score
            /// </summary>
            /// <param name="score">The score to update</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> UpdateLeaderboard(float score)
            {
                if (!_isInitialized)
                {
                    var notInitializedResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Multiplayer client not initialized. Call Initialize() first."
                    };
                    return ViverseResult<bool>.Failure(notInitializedResult);
                }

                try
                {
                    void LeaderboardWrapper(int taskId, Action<string> callback)
                    {
                        Multiplayer_UpdateLeaderboard(score, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(LeaderboardWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        Debug.Log($"Leaderboard updated successfully with score: {score}");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to update leaderboard: {result.Message}");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during UpdateLeaderboard: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during UpdateLeaderboard: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Register a listener for general messages
            /// </summary>
            /// <param name="listenerId">Unique identifier for this listener</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> RegisterMessageListener(string listenerId)
            {
                return await RegisterMultiplayerListenerAsync(listenerId, Multiplayer_RegisterMessageListener, "message");
            }

            /// <summary>
            /// Register a listener for position updates
            /// </summary>
            /// <param name="listenerId">Unique identifier for this listener</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> RegisterPositionListener(string listenerId)
            {
                return await RegisterMultiplayerListenerAsync(listenerId, Multiplayer_RegisterPositionListener, "position");
            }

            /// <summary>
            /// Register a listener for competition results
            /// </summary>
            /// <param name="listenerId">Unique identifier for this listener</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> RegisterCompetitionListener(string listenerId)
            {
                return await RegisterMultiplayerListenerAsync(listenerId, Multiplayer_RegisterCompetitionListener, "competition");
            }

            /// <summary>
            /// Register a listener for leaderboard updates
            /// </summary>
            /// <param name="listenerId">Unique identifier for this listener</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> RegisterLeaderboardListener(string listenerId)
            {
                return await RegisterMultiplayerListenerAsync(listenerId, Multiplayer_RegisterLeaderboardListener, "leaderboard");
            }

            /// <summary>
            /// Unregister a multiplayer event listener
            /// </summary>
            /// <param name="listenerId">The ID of the listener to remove</param>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> UnregisterListener(string listenerId)
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
                    void UnregisterWrapper(int taskId, Action<string> callback)
                    {
                        Multiplayer_UnregisterListener(listenerId, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(UnregisterWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        Debug.Log($"Unregistered multiplayer listener: {listenerId}");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to unregister multiplayer listener: {result.Message}");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during UnregisterListener: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during UnregisterListener: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Helper method for registering multiplayer listeners using async pattern
            /// </summary>
            private async Task<ViverseResult<bool>> RegisterMultiplayerListenerAsync(string listenerId, Action<string, int, Action<string>> registerFunction, string eventTypeName)
            {
                if (!_isInitialized)
                {
                    var notInitializedResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Multiplayer client not initialized. Call Initialize() first."
                    };
                    return ViverseResult<bool>.Failure(notInitializedResult);
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
                    void RegisterWrapper(int taskId, Action<string> callback)
                    {
                        registerFunction(listenerId, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(RegisterWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        Debug.Log($"Registered {eventTypeName} listener: {listenerId}");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to register {eventTypeName} listener: {result.Message}");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during Register{eventTypeName}Listener: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during Register{eventTypeName}Listener: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Get the current session information
            /// </summary>
            public MultiplayerSessionInfo SessionInfo => _sessionInfo;

            /// <summary>
            /// Get the current room ID
            /// </summary>
            public string RoomId => _roomId;

            /// <summary>
            /// Get the current app ID
            /// </summary>
            public string AppId => _appId;

            /// <summary>
            /// Check if the multiplayer client is initialized
            /// </summary>
            public bool IsInitialized => _isInitialized;

            /// <summary>
            /// Reset the multiplayer service state (called during logout/cleanup)
            /// </summary>
            internal void Reset()
            {
                _isInitialized = false;
                _roomId = null;
                _appId = null;
                _sessionInfo = null;
                Debug.Log("Multiplayer service reset");
            }

            /// <summary>
            /// Register a network event listener for multiplayer events using ViverseResult<T> pattern
            /// </summary>
            /// <param name="eventType">The multiplayer event type to listen for</param>
            /// <param name="callback">Callback using standard ViverseResult<NetworkEventData> pattern</param>
            /// <returns>TaskId for managing the listener</returns>
            public string RegisterNetworkEventListener(MultiplayerEventType eventType, Action<ViverseResult<NetworkEventData>> callback)
            {
                return ViverseEventDispatcher.RegisterMultiplayerListener(eventType, callback);
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
            /// Internal method to dispatch events from JavaScript to C# events
            /// </summary>
            internal void DispatchEvent(MultiplayerEventType eventType, string eventData)
            {
                try
                {
                    // ROBUST LOGGING: Log all multiplayer event details
                    Debug.Log($"[MULTIPLAYER_DISPATCH] Event type: {eventType.ToLogString()} ({(int)eventType})");
                    Debug.Log($"[MULTIPLAYER_RAW_DATA] Raw event data: {eventData ?? "NULL"}");
                    
                    switch (eventType)
                    {
                        case MultiplayerEventType.Message:
                            Debug.Log($"[MULTIPLAYER_MESSAGE] Parsing GeneralMessage...");
                            var message = JsonUtility.FromJson<GeneralMessage>(eventData);
                            if (message != null)
                            {
                                Debug.Log($"[MULTIPLAYER_MESSAGE_PARSED] Type: {message.type}, Sender: {message.sender}, Text: {message.text}, Timestamp: {message.timestamp}");
                                Debug.Log($"[MULTIPLAYER_MESSAGE_INVOKE] Invoking OnMessage with {OnMessage?.GetInvocationList()?.Length ?? 0} subscribers");
                                OnMessage?.Invoke(message);
                            }
                            else
                            {
                                Debug.LogError($"[MULTIPLAYER_MESSAGE_ERROR] Failed to parse GeneralMessage from: {eventData}");
                                Debug.LogError("[MULTIPLAYER_MESSAGE_ERROR] Expected structure: {{ type: string, text: string, timestamp: long, sender: string }}");
                            }
                            break;
                            
                        case MultiplayerEventType.Position:
                            Debug.Log($"[MULTIPLAYER_POSITION] Parsing PositionUpdate...");
                            var positionUpdate = JsonUtility.FromJson<PositionUpdate>(eventData);
                            if (positionUpdate != null)
                            {
                                Debug.Log($"[MULTIPLAYER_POSITION_PARSED] EntityType: {positionUpdate.EntityType} ({positionUpdate.entity_type}), UserID: {positionUpdate.user_id}, EntityID: {positionUpdate.entity_id}");
                                Debug.Log($"[MULTIPLAYER_POSITION_DATA] Position: x={positionUpdate.data?.x}, y={positionUpdate.data?.y}, z={positionUpdate.data?.z}, w={positionUpdate.data?.w}");
                                Debug.Log($"[MULTIPLAYER_POSITION_INVOKE] Invoking OnPositionUpdate with {OnPositionUpdate?.GetInvocationList()?.Length ?? 0} subscribers");
                                OnPositionUpdate?.Invoke(positionUpdate);
                            }
                            else
                            {
                                Debug.LogError($"[MULTIPLAYER_POSITION_ERROR] Failed to parse PositionUpdate from: {eventData}");
                                Debug.LogError("[MULTIPLAYER_POSITION_ERROR] Expected structure: {{ entity_type: int, user_id: string, entity_id: string, data: {{ x, y, z, w }} }}");
                            }
                            break;
                            
                        case MultiplayerEventType.Competition:
                            Debug.Log($"[MULTIPLAYER_COMPETITION] Parsing CompetitionResult...");
                            var competitionResult = JsonUtility.FromJson<CompetitionResult>(eventData);
                            if (competitionResult != null)
                            {
                                Debug.Log($"[MULTIPLAYER_COMPETITION_PARSED] Action: {competitionResult.competition?.action_name}, Winner: {competitionResult.competition?.successor}");
                                Debug.Log($"[MULTIPLAYER_COMPETITION_INVOKE] Invoking OnCompetitionResult with {OnCompetitionResult?.GetInvocationList()?.Length ?? 0} subscribers");
                                OnCompetitionResult?.Invoke(competitionResult);
                            }
                            else
                            {
                                Debug.LogError($"[MULTIPLAYER_COMPETITION_ERROR] Failed to parse CompetitionResult from: {eventData}");
                                Debug.LogError("[MULTIPLAYER_COMPETITION_ERROR] Expected structure: {{ competition: {{ action_name: string, successor: string, ... }} }}");
                            }
                            break;
                            
                        case MultiplayerEventType.Leaderboard:
                            Debug.Log($"[MULTIPLAYER_LEADERBOARD] Parsing LeaderboardUpdate...");
                            var leaderboardUpdate = JsonUtility.FromJson<LeaderboardUpdate>(eventData);
                            if (leaderboardUpdate != null)
                            {
                                Debug.Log($"[MULTIPLAYER_LEADERBOARD_PARSED] Entry count: {leaderboardUpdate.leaderboard?.Length ?? 0}, Timestamp: {leaderboardUpdate.timestamp}");
                                if (leaderboardUpdate.leaderboard != null)
                                {
                                    for (int i = 0; i < Math.Min(3, leaderboardUpdate.leaderboard.Length); i++)
                                    {
                                        var entry = leaderboardUpdate.leaderboard[i];
                                        Debug.Log($"[MULTIPLAYER_LEADERBOARD_ENTRY] Rank {entry.rank}: {entry.name} ({entry.user_id}) - Score: {entry.score}");
                                    }
                                }
                                Debug.Log($"[MULTIPLAYER_LEADERBOARD_INVOKE] Invoking OnLeaderboardUpdate with {OnLeaderboardUpdate?.GetInvocationList()?.Length ?? 0} subscribers");
                                OnLeaderboardUpdate?.Invoke(leaderboardUpdate);
                            }
                            else
                            {
                                Debug.LogError($"[MULTIPLAYER_LEADERBOARD_ERROR] Failed to parse LeaderboardUpdate from: {eventData}");
                                Debug.LogError("[MULTIPLAYER_LEADERBOARD_ERROR] Expected structure: {{ leaderboard: [{{ user_id, score, rank, name, timestamp }}], timestamp }}");
                            }
                            break;
                            
                        default:
                            Debug.LogWarning($"[MULTIPLAYER_UNKNOWN] Unknown multiplayer event type: {eventType} ({(int)eventType})");
                            Debug.LogWarning($"[MULTIPLAYER_UNKNOWN] Valid types are: {string.Join(", ", Enum.GetValues(typeof(MultiplayerEventType)).Cast<MultiplayerEventType>().Select(e => $"{e}({(int)e})"))}");
                            Debug.LogWarning($"[MULTIPLAYER_UNKNOWN] Raw data was: {eventData}");
                            break;
                    }
                    
                    Debug.Log($"[MULTIPLAYER_COMPLETE] Successfully dispatched {eventType.ToLogString()} event");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MULTIPLAYER_EXCEPTION] Exception dispatching multiplayer event {eventType}: {e.Message}");
                    Debug.LogError($"[MULTIPLAYER_EXCEPTION] Stack trace: {e.StackTrace}");
                    Debug.LogError($"[MULTIPLAYER_EXCEPTION] Event data was: {eventData ?? "NULL"}");
                    Debug.LogError($"[MULTIPLAYER_EXCEPTION] This indicates a critical parsing error - check data structure alignment with JavaScript SDK");
                }
            }
        }
    }
}