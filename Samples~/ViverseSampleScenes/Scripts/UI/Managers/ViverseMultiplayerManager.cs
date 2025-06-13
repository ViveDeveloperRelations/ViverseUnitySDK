using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using ViverseUI.Infrastructure;
using ViverseWebGLAPI;

namespace ViverseUI.Managers
{
    /// <summary>
    /// Manages multiplayer room operations and real-time events
    /// </summary>
    public class ViverseMultiplayerManager : ViverseManagerBase
    {
        // UI Elements
        private TextField _roomIdInput;
        private Button _subscribeRoomButton;
        private Button _unsubscribeRoomButton;
        private Button _clearEventsButton;
        private TextField _eventLogResult;
        private Label _roomStatusLabel;
        private TextField _testMessageInput;
        private Button _sendTestEventButton;

        // State
        private ViverseRoom _currentRoom;
        private readonly List<string> _eventLog = new List<string>();
        private int _eventCounter = 0;
        private bool _isSubscribed = false;

        // Events
        public event Action<string> OnMultiplayerEvent;

        /// <summary>
        /// Whether currently subscribed to a room
        /// </summary>
        public bool IsSubscribed => _isSubscribed;

        /// <summary>
        /// Current room ID if subscribed
        /// </summary>
        public string CurrentRoomId => _currentRoom?.RoomId;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="context">Service context</param>
        /// <param name="root">Root UI element</param>
        public ViverseMultiplayerManager(IViverseServiceContext context, VisualElement root)
            : base(context, root)
        {
        }

        /// <summary>
        /// Initialize UI elements
        /// </summary>
        protected override void InitializeUIElements()
        {
            _roomIdInput = Root.Q<TextField>("room-id-input");
            _subscribeRoomButton = Root.Q<Button>("subscribe-room-button");
            _unsubscribeRoomButton = Root.Q<Button>("unsubscribe-room-button");
            _clearEventsButton = Root.Q<Button>("clear-events-button");
            _eventLogResult = Root.Q<TextField>("event-log-result");
            _roomStatusLabel = Root.Q<Label>("room-status-label");
            _testMessageInput = Root.Q<TextField>("test-message-input");
            _sendTestEventButton = Root.Q<Button>("send-test-event-button");

            if (_subscribeRoomButton == null || _unsubscribeRoomButton == null)
            {
                Debug.LogError("Multiplayer UI elements not found. Check UXML structure.");
            }
        }

        /// <summary>
        /// Setup event handlers
        /// </summary>
        protected override void SetupEventHandlers()
        {
            if (_subscribeRoomButton != null)
                _subscribeRoomButton.clicked += () => SubscribeToRoom();

            if (_unsubscribeRoomButton != null)
                _unsubscribeRoomButton.clicked += async () => await UnsubscribeFromRoom();

            if (_clearEventsButton != null)
                _clearEventsButton.clicked += ClearEventLog;

            if (_sendTestEventButton != null)
                _sendTestEventButton.clicked += SendTestEvent;
        }

        /// <summary>
        /// Load initial state
        /// </summary>
        protected override void LoadInitialState()
        {
            UpdateSubscriptionState(false);

            // Set default room ID for testing
            if (_roomIdInput != null && string.IsNullOrEmpty(_roomIdInput.value))
            {
                _roomIdInput.value = "test-room-001";
            }
        }

        /// <summary>
        /// Cleanup event handlers
        /// </summary>
        protected override void CleanupEventHandlers()
        {
            if (_subscribeRoomButton != null)
                _subscribeRoomButton.clicked -= () => SubscribeToRoom();

            if (_unsubscribeRoomButton != null)
                _unsubscribeRoomButton.clicked -= async () => await UnsubscribeFromRoom();

            if (_clearEventsButton != null)
                _clearEventsButton.clicked -= ClearEventLog;

            if (_sendTestEventButton != null)
                _sendTestEventButton.clicked -= SendTestEvent;
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        protected override void CleanupResources()
        {
            if (_currentRoom != null)
            {
                try
                {
                    _currentRoom.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error disposing room: {e.Message}");
                }
                finally
                {
                    _currentRoom = null;
                }
            }

            _eventLog.Clear();
            _eventCounter = 0;
        }

        /// <summary>
        /// Join or create a room following proper multiplayer flow:
        /// Initialize Play → Initialize Matchmaking → Set Actor → Create/Join Room → Initialize Multiplayer → Start real-time communication
        /// </summary>
        private async void SubscribeToRoom()
        {
            if (!CheckInitialization()) return;

            string roomName = _roomIdInput?.value?.Trim();
            if (string.IsNullOrEmpty(roomName))
            {
                UIState.ShowError("Please enter a Room Name");
                return;
            }

            UIState.SetLoading(true, $"Joining/creating room {roomName}...");

            try
            {
                // ✅ Step 1: Create room manager with test app ID
                string testAppId = "64aa6613-4e6c-4db4-b270-67744e953ce0";
                _currentRoom = Context.Core.CreateRoomManager(testAppId);

                if (_currentRoom == null)
                {
                    UIState.ShowError("Failed to create room manager");
                    return;
                }

                // ✅ Step 2: Initialize services (Play → Matchmaking)
                AddEventToLog("Initializing multiplayer services...");
                var servicesResult = await _currentRoom.InitializeServices();
                if (!servicesResult.IsSuccess)
                {
                    // ✅ Use safe logging extension for comprehensive error reporting
                    servicesResult.LogError("Initialize Multiplayer Services");
                    UIState.ShowError($"Failed to initialize services: {servicesResult.ErrorMessage}");
                    return;
                }

                // ✅ Step 3: Set up actor information for this player session
                var actorInfo = new ActorInfo
                {
                    session_id = $"ui-test-session-{DateTime.Now:yyyyMMddHHmmss}",
                    name = $"UITestPlayer-{UnityEngine.Random.Range(1000, 9999)}",
                    level = 1,
                    skill = 50
                };

                // ✅ Step 4: Set up room configuration
                var roomInfo = new RoomInfo
                {
                    name = roomName,
                    mode = "ui_test",
                    maxPlayers = 4,
                    minPlayers = 1,
                    purpose = "ui_test",
                    created_by = "unity_ui",
                    difficulty = "normal",
                    game_type = "test"
                };

                AddEventToLog($"Attempting to join/create room: {roomName}");

                // ✅ Step 5: Join or create room (this includes multiplayer initialization)
                var roomResult = await _currentRoom.JoinOrCreateRoom(actorInfo, roomInfo);
                if (!roomResult.IsSuccess)
                {
                    // ✅ Use safe logging extension for comprehensive error reporting
                    roomResult.LogError("Join Or Create Room");
                    UIState.ShowError($"Failed to join/create room: {roomResult.ErrorMessage}");
                    return;
                }

                // ✅ Step 6: Subscribe to room events (now safe because room is joined and multiplayer initialized)
                SubscribeToRoomEvents();

                UpdateSubscriptionState(true);

                string roomAction = _currentRoom.IsRoomOwner ? "Created" : "Joined";
                string successMessage = $"✅ {roomAction} room: {_currentRoom.RoomId}";
                AddEventToLog(successMessage);
                AddEventToLog($"Room: {roomResult.Data.name} ({roomResult.Data.currentPlayers}/{roomResult.Data.maxPlayers} players)");

                UIState.ShowMessage($"Successfully {roomAction.ToLower()} room {roomName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in room operation: {e.Message}");
                UIState.ShowError($"Error in room operation: {e.Message}");

                // Cleanup on failure
                if (_currentRoom != null)
                {
                    try
                    {
                        _currentRoom.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        Debug.LogWarning($"Error disposing room during cleanup: {disposeEx.Message}");
                    }
                    finally
                    {
                        _currentRoom = null;
                    }
                }

                UpdateSubscriptionState(false);
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }

        /// <summary>
        /// Leave room and unsubscribe from all events following proper cleanup flow
        /// </summary>
        private async Task UnsubscribeFromRoom()
        {
            if (_currentRoom == null)
            {
                UIState.ShowMessage("Not currently in any room");
                return;
            }

            UIState.SetLoading(true, "Leaving room...");

            try
            {
                string roomId = _currentRoom.RoomId;
                string roomAction = _currentRoom.IsRoomOwner ? "Closing" : "Leaving";

                AddEventToLog($"{roomAction} room: {roomId}");

                // ✅ Properly leave the room using the correct API
                var leaveResult = await _currentRoom.LeaveRoom();
                if (!leaveResult.IsSuccess)
                {
                    // ✅ Use safe logging extension for comprehensive error reporting
                    leaveResult.LogError("Leave Room");
                    UIState.ShowError($"Failed to leave room: {leaveResult.ErrorMessage}");
                    return;
                }

                // ✅ Clean up room manager and events
                _currentRoom.Dispose();
                _currentRoom = null;

                UpdateSubscriptionState(false);
                AddEventToLog($"✅ Successfully left room: {roomId}");
                UIState.ShowMessage($"Successfully left room {roomId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error leaving room: {e.Message}");
                UIState.ShowError($"Error leaving room: {e.Message}");

                // Force cleanup even if leave failed
                try
                {
                    if (_currentRoom != null)
                    {
                        _currentRoom.Dispose();
                        _currentRoom = null;
                    }
                }
                catch (Exception disposeEx)
                {
                    Debug.LogWarning($"Error during forced cleanup: {disposeEx.Message}");
                }
                finally
                {
                    UpdateSubscriptionState(false);
                }
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }

        /// <summary>
        /// Subscribe to all room events
        /// </summary>
        private void SubscribeToRoomEvents()
        {
            if (_currentRoom == null) return;

            // Room lifecycle events
            _currentRoom.OnRoomJoined += OnRoomJoined;
            _currentRoom.OnRoomLeft += OnRoomLeft;
            _currentRoom.OnActorJoined += OnActorJoined;
            _currentRoom.OnActorLeft += OnActorLeft;
            _currentRoom.OnRoomClosed += OnRoomClosed;

            // Multiplayer communication events
            _currentRoom.OnGeneralMessage += OnGeneralMessage;
            _currentRoom.OnPositionUpdate += OnPositionUpdate;
            _currentRoom.OnCompetitionResult += OnCompetitionResult;
            _currentRoom.OnLeaderboardUpdate += OnLeaderboardUpdate;
        }

        /// <summary>
        /// Handle room joined event
        /// </summary>
        private void OnRoomJoined(ViverseResult<RoomData> result)
        {
            if (result.IsSuccess && result.Data != null)
            {
                var room = result.Data;
                AddEventToLog($"✓ Room Joined: {room.name} ({room.roomId}) - Players: {room.currentPlayers}/{room.maxPlayers}");
            }
            else
            {
                AddEventToLog($"✗ Room Join Failed: {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// Handle room left event
        /// </summary>
        private void OnRoomLeft(ViverseResult<RoomData> result)
        {
            AddEventToLog("✓ Left room");
        }

        /// <summary>
        /// Handle actor joined event
        /// </summary>
        private void OnActorJoined(ViverseResult<ActorInfo> result)
        {
            if (result.IsSuccess && result.Data != null)
            {
                var actor = result.Data;
                AddEventToLog($"👤 Player Joined: {actor.name} (ID: {actor.session_id})");
            }
        }

        /// <summary>
        /// Handle actor left event
        /// </summary>
        private void OnActorLeft(ViverseResult<ActorInfo> result)
        {
            if (result.IsSuccess && result.Data != null)
            {
                var actor = result.Data;
                AddEventToLog($"👤 Player Left: {actor.name} (ID: {actor.session_id})");
            }
        }

        /// <summary>
        /// Handle room closed event
        /// </summary>
        private void OnRoomClosed(ViverseResult<RoomData> result)
        {
            AddEventToLog("🚪 Room Closed");
            UpdateSubscriptionState(false);
        }

        /// <summary>
        /// Handle general message event
        /// </summary>
        private void OnGeneralMessage(ViverseResult<GeneralMessage> result)
        {
            if (result.IsSuccess && result.Data != null)
            {
                var message = result.Data;
                var time = DateTimeOffset.FromUnixTimeMilliseconds(message.TimeStamp).ToString("HH:mm:ss");
                AddEventToLog($"💬 [{time}] {message.sender}: {message.text}");
            }
        }

        /// <summary>
        /// Handle position update event
        /// </summary>
        private void OnPositionUpdate(ViverseResult<PositionUpdate> result)
        {
            if (result.IsSuccess && result.Data != null)
            {
                var update = result.Data;
                var entityInfo = update.IsPlayerUpdate ? $"Player {update.user_id}" : $"Entity {update.entity_id}";
                AddEventToLog($"📍 Position Update: {entityInfo} -> ({update.data.x:F2}, {update.data.y:F2}, {update.data.z:F2})");
            }
        }

        /// <summary>
        /// Handle competition result event
        /// </summary>
        private void OnCompetitionResult(ViverseResult<CompetitionResult> result)
        {
            if (result.IsSuccess && result.Data != null)
            {
                var competition = result.Data.competition;
                AddEventToLog($"🏆 Competition: {competition.action_name} - Winner: {competition.successor}");
            }
        }

        /// <summary>
        /// Handle leaderboard update event
        /// </summary>
        private void OnLeaderboardUpdate(ViverseResult<LeaderboardUpdate> result)
        {
            if (result.IsSuccess && result.Data != null)
            {
                var leaderboard = result.Data;
                var entryCount = leaderboard.leaderboard?.Length ?? 0;
                AddEventToLog($"📊 Leaderboard Update: {entryCount} entries");
            }
        }

        /// <summary>
        /// Add event to log and update display
        /// </summary>
        /// <param name="eventText">Event text to add</param>
        private void AddEventToLog(string eventText)
        {
            _eventCounter++;
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] #{_eventCounter}: {eventText}";

            _eventLog.Add(logEntry);

            // Keep only last 50 events to prevent memory issues
            if (_eventLog.Count > 50)
            {
                _eventLog.RemoveAt(0);
            }

            UpdateEventLogDisplay();
            OnMultiplayerEvent?.Invoke(logEntry);
        }

        /// <summary>
        /// Update event log display
        /// </summary>
        private void UpdateEventLogDisplay()
        {
            if (_eventLogResult == null) return;

            string logText = string.Join("\n", _eventLog);
            _eventLogResult.value = logText;

            // Auto-scroll to bottom would require additional UI manipulation
        }

        /// <summary>
        /// Clear event log
        /// </summary>
        private void ClearEventLog()
        {
            _eventLog.Clear();
            _eventCounter = 0;
            UpdateEventLogDisplay();
            UIState.ShowMessage("Event log cleared");
        }

        /// <summary>
        /// Send test event to log
        /// </summary>
        private void SendTestEvent()
        {
            string message = _testMessageInput?.value?.Trim();
            if (string.IsNullOrEmpty(message))
            {
                message = "Test event message";
            }

            AddEventToLog($"🧪 Test Event: {message}");

            if (_testMessageInput != null)
                _testMessageInput.value = "";
        }

        /// <summary>
        /// Update room state and UI to reflect proper multiplayer flow
        /// </summary>
        /// <param name="isInRoom">Whether currently in a multiplayer room</param>
        private void UpdateSubscriptionState(bool isInRoom)
        {
            _isSubscribed = isInRoom;

            // Update button states and text to reflect room actions
            if (_subscribeRoomButton != null)
            {
                _subscribeRoomButton.SetEnabled(!isInRoom);
                _subscribeRoomButton.text = isInRoom ? "Already In Room" : "Join/Create Room";
            }

            if (_unsubscribeRoomButton != null)
            {
                _unsubscribeRoomButton.SetEnabled(isInRoom);
                string leaveText = _currentRoom?.IsRoomOwner == true ? "Close Room" : "Leave Room";
                _unsubscribeRoomButton.text = isInRoom ? leaveText : "Not In Room";
            }

            if (_sendTestEventButton != null)
                _sendTestEventButton.SetEnabled(isInRoom);

            // Update status label with proper room information
            if (_roomStatusLabel != null)
            {
                if (isInRoom && _currentRoom != null)
                {
                    string roomType = _currentRoom.IsRoomOwner ? "Owner" : "Member";
                    _roomStatusLabel.text = $"✅ {roomType} of room: {_currentRoom.RoomId}";
                }
                else
                {
                    _roomStatusLabel.text = "❌ Not connected to any room";
                }
            }
        }

        /// <summary>
        /// Get current event log as string array
        /// </summary>
        /// <returns>Array of log entries</returns>
        public string[] GetEventLog()
        {
            return _eventLog.ToArray();
        }

        /// <summary>
        /// Force unsubscribe from current room (cleanup method)
        /// </summary>
        public void ForceUnsubscribe()
        {
            if (_currentRoom != null)
            {
                try
                {
                    _currentRoom.Dispose();
                    _currentRoom = null;
                    UpdateSubscriptionState(false);
                    AddEventToLog("Force unsubscribed from room");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error force unsubscribing: {e.Message}");
                }
            }
        }
    }
}
