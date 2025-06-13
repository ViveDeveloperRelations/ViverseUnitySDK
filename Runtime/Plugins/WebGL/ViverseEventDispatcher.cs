using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace ViverseWebGLAPI
{
    /// <summary>
    /// Unified event dispatcher using standard ViverseResult<T> pattern for networking events
    /// Replaces complex event queue system with simple TaskId-based callbacks matching other SDK services
    /// </summary>
    public static class ViverseEventDispatcher
    {
        // DllImport declarations for registering event listeners (returns TaskId)
        [DllImport("__Internal")]
        private static extern string ViverseEventDispatcher_RegisterMatchmakingListener(string eventTypeName, Action<IntPtr> callback);

        [DllImport("__Internal")]
        private static extern string ViverseEventDispatcher_RegisterMultiplayerListener(string eventTypeName, Action<IntPtr> callback);

        [DllImport("__Internal")]
        private static extern int ViverseEventDispatcher_UnregisterListener(string taskId);

        // Active callback storage using TaskId pattern
        private static readonly Dictionary<string, Action<ViverseResult<NetworkEventData>>> _activeCallbacks = 
            new Dictionary<string, Action<ViverseResult<NetworkEventData>>>();

        private static ViverseCore _viverseCore;
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize the unified event dispatcher
        /// </summary>
        /// <param name="viverseCore">The ViverseCore instance to route events to</param>
        public static void Initialize(ViverseCore viverseCore)
        {
            if (viverseCore == null)
            {
                Debug.LogError("ViverseEventDispatcher.Initialize: viverseCore parameter cannot be null");
                return;
            }
            
            _viverseCore = viverseCore;
            _isInitialized = true;
            Debug.Log("ViverseEventDispatcher initialized with unified callback pattern");
        }

        /// <summary>
        /// Register a matchmaking event listener using standard async pattern
        /// </summary>
        /// <param name="eventType">The matchmaking event type to listen for</param>
        /// <param name="callback">Callback using standard ViverseResult<NetworkEventData> pattern</param>
        /// <returns>TaskId for managing the listener</returns>
        public static string RegisterMatchmakingListener(MatchmakingEventType eventType, Action<ViverseResult<NetworkEventData>> callback)
        {
            if (!_isInitialized)
            {
                Debug.LogError("ViverseEventDispatcher not initialized");
                callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorSdkNotInitialized, "Event dispatcher not initialized"));
                return null;
            }

            if (!eventType.IsValidEventType())
            {
                Debug.LogError($"Invalid matchmaking event type: {eventType}");
                callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorInvalidParameter, $"Invalid event type: {eventType}"));
                return null;
            }

            try
            {
                // Register with JavaScript and get TaskId
                string taskId = ViverseEventDispatcher_RegisterMatchmakingListener(eventType.ToLogString(), OnEventCallback);
                
                if (string.IsNullOrEmpty(taskId))
                {
                    Debug.LogError($"Failed to register matchmaking listener for {eventType}");
                    callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorException, "Failed to register event listener"));
                    return null;
                }

                // Store callback using TaskId
                _activeCallbacks[taskId] = callback;

                Debug.Log($"Registered matchmaking listener for {eventType} with TaskId: {taskId}");
                return taskId;
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception registering matchmaking listener: {e.Message}");
                callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorException, $"Registration exception: {e.Message}"));
                return null;
            }
        }

        /// <summary>
        /// Register a multiplayer event listener using standard async pattern
        /// </summary>
        /// <param name="eventType">The multiplayer event type to listen for</param>
        /// <param name="callback">Callback using standard ViverseResult<NetworkEventData> pattern</param>
        /// <returns>TaskId for managing the listener</returns>
        public static string RegisterMultiplayerListener(MultiplayerEventType eventType, Action<ViverseResult<NetworkEventData>> callback)
        {
            if (!_isInitialized)
            {
                Debug.LogError("ViverseEventDispatcher not initialized");
                callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorSdkNotInitialized, "Event dispatcher not initialized"));
                return null;
            }

            if (!eventType.IsValidEventType())
            {
                Debug.LogError($"Invalid multiplayer event type: {eventType}");
                callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorInvalidParameter, $"Invalid event type: {eventType}"));
                return null;
            }

            try
            {
                // Register with JavaScript and get TaskId
                string taskId = ViverseEventDispatcher_RegisterMultiplayerListener(eventType.ToLogString(), OnEventCallback);
                
                if (string.IsNullOrEmpty(taskId))
                {
                    Debug.LogError($"Failed to register multiplayer listener for {eventType}");
                    callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorException, "Failed to register event listener"));
                    return null;
                }

                // Store callback using TaskId
                _activeCallbacks[taskId] = callback;

                Debug.Log($"Registered multiplayer listener for {eventType} with TaskId: {taskId}");
                return taskId;
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception registering multiplayer listener: {e.Message}");
                callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorException, $"Registration exception: {e.Message}"));
                return null;
            }
        }

        /// <summary>
        /// Unregister an event listener by TaskId
        /// </summary>
        /// <param name="taskId">The TaskId returned from RegisterXXXListener</param>
        /// <returns>True if successfully unregistered</returns>
        public static bool UnregisterListener(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                Debug.LogWarning("Cannot unregister listener with null or empty TaskId");
                return false;
            }

            try
            {
                // Remove from JavaScript
                int result = ViverseEventDispatcher_UnregisterListener(taskId);
                
                // Remove from C# callback storage
                bool removed = _activeCallbacks.Remove(taskId);

                if (result == (int)ViverseSDKReturnCode.Success && removed)
                {
                    Debug.Log($"Successfully unregistered listener: {taskId}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Partial unregister for TaskId {taskId}: JS result={result}, C# removed={removed}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception unregistering listener {taskId}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// MonoPInvokeCallback for receiving events from JavaScript using standard ViverseResult<T> pattern
        /// </summary>
        [MonoPInvokeCallback(typeof(Action<IntPtr>))]
        private static void OnEventCallback(IntPtr ptr)
        {
            string resultJson = null;
            try
            {
                // Parse standard ViverseResult<NetworkEventData> from JavaScript
                resultJson = ViverseUtils.IntPtrToString(ptr);
                
                // ROBUST LOGGING: Always log the raw JSON for debugging
                Debug.Log($"[VIVERSE_EVENT_RAW] Received raw JSON: {resultJson ?? "NULL"}");
                
                if (string.IsNullOrEmpty(resultJson))
                {
                    Debug.LogWarning("[VIVERSE_EVENT_ERROR] Received empty event result JSON - this indicates a JavaScript->C# marshaling issue");
                    return;
                }

                var viverseResult = JsonUtility.FromJson<ViverseResult<string>>(resultJson);
                if (viverseResult == null)
                {
                    Debug.LogError($"[VIVERSE_EVENT_ERROR] Failed to parse ViverseResult from JSON. Raw JSON was: {resultJson}");
                    Debug.LogError("[VIVERSE_EVENT_ERROR] This suggests the JSON structure doesn't match ViverseResult<string> - check JavaScript event dispatch format");
                    return;
                }

                Debug.Log($"[VIVERSE_EVENT_PARSED] Successfully parsed ViverseResult - TaskId: {viverseResult.TaskId}, IsSuccess: {viverseResult.IsSuccess}, PayloadLength: {viverseResult.SafePayload.Length}");

                // Find and invoke the callback for this TaskId
                if (_activeCallbacks.TryGetValue(viverseResult.TaskId, out var callback))
                {
                    Debug.Log($"[VIVERSE_EVENT_CALLBACK] Found callback for TaskId: {viverseResult.TaskId}");
                    
                    if (viverseResult.IsSuccess)
                    {
                        // ROBUST LOGGING: Log the payload before parsing
                        Debug.Log($"[VIVERSE_EVENT_PAYLOAD] Raw NetworkEventData payload: {viverseResult.SafePayload}");
                        
                        // Parse NetworkEventData from payload
                        var eventData = JsonUtility.FromJson<NetworkEventData>(viverseResult.SafePayload);
                        if (eventData != null)
                        {
                            // ROBUST LOGGING: Log parsed event details
                            Debug.Log($"[VIVERSE_EVENT_SUCCESS] Parsed NetworkEventData - Category: {eventData.EventCategory}, Type: {eventData.EventType}, DataLength: {eventData.EventData?.Length ?? 0}");
                            Debug.Log($"[VIVERSE_EVENT_DATA] Event data contents: {eventData.EventData ?? "NULL"}");
                            
                            var result = ViverseResult<NetworkEventData>.Success(eventData, viverseResult.RawResult);
                            callback.Invoke(result);

                            Debug.Log($"[VIVERSE_EVENT_DISPATCHED] Successfully processed and dispatched event for TaskId: {viverseResult.TaskId}");
                        }
                        else
                        {
                            Debug.LogError($"[VIVERSE_EVENT_ERROR] Failed to parse NetworkEventData from payload for TaskId: {viverseResult.TaskId}");
                            Debug.LogError($"[VIVERSE_EVENT_ERROR] NetworkEventData payload was: {viverseResult.SafePayload}");
                            Debug.LogError("[VIVERSE_EVENT_ERROR] This suggests the payload structure doesn't match NetworkEventData class - check EventCategory, EventType, EventData, Timestamp fields");
                            callback.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorParseJson, "Failed to parse event data"));
                        }
                    }
                    else
                    {
                        // Forward the error result
                        Debug.LogError($"[VIVERSE_EVENT_ERROR] JavaScript reported failure for TaskId {viverseResult.TaskId}: {viverseResult.ErrorMessage}");
                        Debug.LogError($"[VIVERSE_EVENT_ERROR] Error code: {viverseResult.ViverseSDKReturnCode}, Raw JSON was: {resultJson}");
                        
                        var errorResult = ViverseResult<NetworkEventData>.Failure(viverseResult.RawResult);
                        callback.Invoke(errorResult);
                    }
                }
                else
                {
                    Debug.LogWarning($"[VIVERSE_EVENT_WARNING] No callback found for TaskId: {viverseResult.TaskId}");
                    Debug.LogWarning($"[VIVERSE_EVENT_WARNING] Available TaskIds: [{string.Join(", ", _activeCallbacks.Keys)}]");
                    Debug.LogWarning($"[VIVERSE_EVENT_WARNING] This suggests a TaskId mismatch between JavaScript and C# or callback was already removed");
                    Debug.LogWarning($"[VIVERSE_EVENT_WARNING] Raw JSON was: {resultJson}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VIVERSE_EVENT_EXCEPTION] Exception in OnEventCallback: {e.Message}");
                Debug.LogError($"[VIVERSE_EVENT_EXCEPTION] Stack trace: {e.StackTrace}");
                Debug.LogError($"[VIVERSE_EVENT_EXCEPTION] Raw JSON that caused exception: {resultJson ?? "NULL"}");
                Debug.LogError("[VIVERSE_EVENT_EXCEPTION] This indicates a critical parsing or callback execution error - check data structure alignment");
            }
        }

        /// <summary>
        /// Get statistics about active listeners
        /// </summary>
        /// <returns>Number of active event listeners</returns>
        public static int GetActiveListenerCount()
        {
            return _activeCallbacks.Count;
        }

        /// <summary>
        /// Get all active TaskIds (for debugging)
        /// </summary>
        /// <returns>Array of active TaskIds</returns>
        public static string[] GetActiveTaskIds()
        {
            var taskIds = new string[_activeCallbacks.Count];
            _activeCallbacks.Keys.CopyTo(taskIds, 0);
            return taskIds;
        }

        /// <summary>
        /// Register a room-aware matchmaking event listener that filters events by room ID
        /// </summary>
        /// <param name="roomId">The room ID to filter events for</param>
        /// <param name="callback">Callback for room-specific matchmaking events</param>
        /// <returns>TaskId for managing the listener</returns>
        public static string RegisterRoomMatchmakingListener(string roomId, Action<ViverseResult<NetworkEventData>> callback)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                Debug.LogError("Room ID cannot be null or empty for room-aware listener");
                callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorInvalidParameter, "Room ID required"));
                return null;
            }

            // Create a filtered callback that only processes events for this room
            var roomFilteredCallback = new Action<ViverseResult<NetworkEventData>>((result) =>
            {
                if (result?.IsSuccess == true && result.Data != null)
                {
                    // For matchmaking events, check if the event data contains our room ID
                    var eventData = result.Data;
                    if (IsEventForRoom(eventData, roomId))
                    {
                        Debug.Log($"[VIVERSE_ROOM_FILTER] Matchmaking event {eventData.EventType} matches room {roomId}");
                        callback?.Invoke(result);
                    }
                    else
                    {
                        Debug.Log($"[VIVERSE_ROOM_FILTER] Matchmaking event {eventData.EventType} filtered out (not for room {roomId})");
                    }
                }
                else
                {
                    // Forward error results without filtering
                    callback?.Invoke(result);
                }
            });

            // Register all matchmaking event types with the filtered callback
            return RegisterMatchmakingListener(MatchmakingEventType.RoomJoined, roomFilteredCallback) ??
                   RegisterMatchmakingListener(MatchmakingEventType.RoomLeft, roomFilteredCallback) ??
                   RegisterMatchmakingListener(MatchmakingEventType.ActorJoined, roomFilteredCallback) ??
                   RegisterMatchmakingListener(MatchmakingEventType.ActorLeft, roomFilteredCallback) ??
                   RegisterMatchmakingListener(MatchmakingEventType.RoomClosed, roomFilteredCallback);
        }

        /// <summary>
        /// Register a room-aware multiplayer event listener that filters events by room context
        /// </summary>
        /// <param name="roomId">The room ID to filter events for</param>
        /// <param name="callback">Callback for room-specific multiplayer events</param>
        /// <returns>TaskId for managing the listener</returns>
        public static string RegisterRoomMultiplayerListener(string roomId, Action<ViverseResult<NetworkEventData>> callback)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                Debug.LogError("Room ID cannot be null or empty for room-aware listener");
                callback?.Invoke(ViverseResult<NetworkEventData>.Failure(ViverseSDKReturnCode.ErrorInvalidParameter, "Room ID required"));
                return null;
            }

            // Create a filtered callback that only processes events for this room
            var roomFilteredCallback = new Action<ViverseResult<NetworkEventData>>((result) =>
            {
                if (result?.IsSuccess == true && result.Data != null)
                {
                    // For multiplayer events, assume they're already room-scoped by the multiplayer client
                    // In the future, we could add additional room filtering here if needed
                    Debug.Log($"[VIVERSE_ROOM_FILTER] Multiplayer event {result.Data.EventType} for room {roomId}");
                    callback?.Invoke(result);
                }
                else
                {
                    // Forward error results without filtering
                    callback?.Invoke(result);
                }
            });

            // Register all multiplayer event types with the filtered callback
            return RegisterMultiplayerListener(MultiplayerEventType.Message, roomFilteredCallback) ??
                   RegisterMultiplayerListener(MultiplayerEventType.Position, roomFilteredCallback) ??
                   RegisterMultiplayerListener(MultiplayerEventType.Competition, roomFilteredCallback) ??
                   RegisterMultiplayerListener(MultiplayerEventType.Leaderboard, roomFilteredCallback);
        }

        /// <summary>
        /// Helper method to check if an event belongs to a specific room
        /// </summary>
        /// <param name="eventData">The network event data to check</param>
        /// <param name="roomId">The room ID to match against</param>
        /// <returns>True if the event is for the specified room</returns>
        private static bool IsEventForRoom(NetworkEventData eventData, string roomId)
        {
            if (eventData == null || string.IsNullOrEmpty(eventData.EventData))
                return false;

            try
            {
                // Try to parse as RoomData first (most matchmaking events)
                var roomData = JsonUtility.FromJson<RoomData>(eventData.EventData);
                if (roomData != null && !string.IsNullOrEmpty(roomData.roomId))
                {
                    return roomData.roomId == roomId;
                }
                
                // Try alternative roomId field
                if (roomData != null && !string.IsNullOrEmpty(roomData.id))
                {
                    return roomData.id == roomId;
                }

                // For actor events, we'll accept them as room-relevant 
                // (actors are inherently associated with the current room context)
                var actorData = JsonUtility.FromJson<ActorInfo>(eventData.EventData);
                if (actorData != null && !string.IsNullOrEmpty(actorData.session_id))
                {
                    // Actor events are room-scoped by nature when received
                    return true;
                }

                Debug.LogWarning($"[VIVERSE_ROOM_FILTER] Could not determine room for event data: {eventData.EventData}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VIVERSE_ROOM_FILTER] Exception parsing event data for room filtering: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reset the event dispatcher (called during cleanup)
        /// </summary>
        public static void Reset()
        {
            try
            {
                // Create a copy of TaskIds to avoid dictionary modification during iteration
                var taskIdsCopy = new string[_activeCallbacks.Count];
                _activeCallbacks.Keys.CopyTo(taskIdsCopy, 0);
                
                // Unregister all active listeners using the copy
                foreach (var taskId in taskIdsCopy)
                {
                    try
                    {
                        ViverseEventDispatcher_UnregisterListener(taskId);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to unregister TaskId {taskId} during reset: {e.Message}");
                    }
                }

                _activeCallbacks.Clear();
                _viverseCore = null;
                _isInitialized = false;

                Debug.Log("ViverseEventDispatcher reset completed");
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception during ViverseEventDispatcher reset: {e.Message}");
            }
        }
    }
}