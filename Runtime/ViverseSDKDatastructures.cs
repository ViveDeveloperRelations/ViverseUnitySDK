using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace ViverseWebGLAPI
{

    [Serializable, Preserve]
    public class LoginResult
    {
	    [Preserve] public string state;
        [Preserve] public string access_token;
        [Preserve] public string account_id;
        [Preserve] public int expires_in;
        [Preserve] public string token_type;
    }

    [Serializable, Preserve]
    public class AccessTokenResult
    {
        [Preserve] public string access_token;
        [Preserve] public string account_id;
        [Preserve] public int expires_in;
    }

    [Serializable, Preserve]
    public class OAuthCallbackResult
    {
        [Preserve] public bool detected;
        [Preserve] public string code;
        [Preserve] public string state;
    }

    [Serializable, Preserve]
    public class LeaderboardResult
    {
	    [Preserve] public LeaderboardEntry[] ranking;
	    [Preserve] public LeaderboardMeta meta;
        [Preserve] public int total_count;
    }
    [Serializable, Preserve]
    public class DisplayName
    {
	    [Preserve] public string lang;
	    [Preserve] public string name;
    }
    [Serializable, Preserve]
    public class LeaderboardMeta
    {
	    [Preserve] public string app_id;
	    [Preserve] public string meta_name;
	    [Preserve] public DisplayName[] display_name;
	    [Preserve] public int sort_type;
	    [Preserve] public int update_type;
	    [Preserve] public int data_type;
    }
    [Serializable,Preserve]
    public class LeaderboardEntry
    {
	    [Preserve] public string uid;
	    [Preserve] public string name;
	    [Preserve] public double value;
	    [Preserve] public int rank;
    }

    [Serializable, Preserve]
    public class Avatar
    {
	    [Preserve] public int id;
	    [Preserve] public bool isPrivate;
	    [Preserve] public string vrmUrl;
	    [Preserve] public string headIconUrl;
	    [Preserve] public string snapshot;

	    [Preserve] public double createTime; // <-- match JSON field name exactly
	    [Preserve] public double updateTime;

	    public long CreateTime => (long)createTime;
	    public long UpdateTime => (long)updateTime;
    }

    [Serializable, Preserve]
    public class UserProfile
    {
	    [Preserve] public string name;
	    [Preserve] public Avatar activeAvatar;
    }

    /// <summary>
    /// Represents a single achievement with its metadata
    /// </summary>
    [Serializable,Preserve]
    public class Achievement
    {
        [Preserve] public string api_name;
        [Preserve] public bool unlock;
    }

    /// <summary>
    /// Wrapper class for serializing a list of achievements
    /// </summary>
    [Serializable,Preserve]
    public class AchievementsWrapper
    {
        [Preserve] public Achievement[] achievements;
    }

    /// <summary>
    /// Detailed information about a user achievement
    /// </summary>
    [Serializable,Preserve]
    public class UserAchievementInfo
    {
        [Preserve] public string achievement_id;
        [Preserve] public string api_name;
        [Preserve] public string display_name;
        [Preserve] public string description;
        [Preserve] public bool is_achieved;
        [Preserve] public string achieved_icon;
        [Preserve] public string unachieved_icon;
        [Preserve] public int achieved_times;
    }

    /// <summary>
    /// Result of getting user achievements
    /// </summary>
    [Serializable,Preserve]
    public class UserAchievementResult
    {
        [Preserve] public UserAchievementInfo[] achievements;
        [Preserve] public int total;
    }

    /// <summary>
    /// Information about a successful achievement upload
    /// </summary>
    [Serializable,Preserve]
    public class SuccessAchievement
    {
        [Preserve] public string api_name;
        [Preserve] public double time_stamp;
        public long TimeStamp => (long)time_stamp;
    }

    /// <summary>
    /// Information about a failed achievement upload
    /// </summary>
    [Serializable, Preserve]
    public class FailureAchievement
    {
        [Preserve] public string api_name;
        [Preserve] public int code;
        [Preserve] public string message;
    }

    /// <summary>
    /// Success information for achievement uploads
    /// </summary>
    [Serializable,Preserve]
    public class SuccessInfo
    {
        [Preserve] public int total;
        [Preserve] public SuccessAchievement[] achievements;
    }

    /// <summary>
    /// Failure information for achievement uploads
    /// </summary>
    [Serializable,Preserve]
    public class FailureInfo
    {
        [Preserve] public int total;
        [Preserve] public FailureAchievement[] achievements;
    }

    /// <summary>
    /// Complete result of an achievement upload operation
    /// </summary>
    [Serializable,Preserve]
    public class AchievementUploadResult
    {
        [Preserve] public SuccessInfo success;
        [Preserve] public FailureInfo failure;
    }

    // ============================================
    // MULTIPLAYER DATA STRUCTURES (v1.2.9)
    // ============================================

    /// <summary>
    /// Matchmaking event types for event listener management
    /// </summary>
    public enum MatchmakingEventType
    {
        UNSET_VALUE = 0,
        RoomJoined = 1,
        RoomLeft = 2,
        ActorJoined = 3,
        ActorLeft = 4,
        RoomClosed = 5
    }

    /// <summary>
    /// Multiplayer communication event types for event listener management
    /// </summary>
    public enum MultiplayerEventType
    {
        UNSET_VALUE = 0,
        Message = 1,
        Position = 2,
        Competition = 3,
        Leaderboard = 4
    }

    /// <summary>
    /// Raw SDK event types for low-level SDK state changes
    /// These correspond to the raw JavaScript SDK events (onConnect, onJoinedLobby, etc.)
    /// </summary>
    public enum SDKEventType
    {
        UNSET_VALUE = 0,
        OnConnect = 1,
        OnJoinedLobby = 2,
        OnJoinRoom = 3,
        OnRoomListUpdate = 4,
        OnRoomActorChange = 5,
        OnRoomClosed = 6,
        OnError = 7,
        StateChange = 8
    }

    /// <summary>
    /// Entity type for position updates and notifications
    /// </summary>
    public enum EntityType
    {
        UNSET_VALUE = 0,
        Player = 1,
        Entity = 2
    }

    /// <summary>
    /// Extension methods for event type enums
    /// </summary>
    public static class EventTypeExtensions
    {
        /// <summary>
        /// Convert MatchmakingEventType to string for logging/debugging
        /// </summary>
        public static string ToLogString(this MatchmakingEventType eventType)
        {
            switch (eventType)
            {
                case MatchmakingEventType.UNSET_VALUE: return "unset";
                case MatchmakingEventType.RoomJoined: return "room_joined";
                case MatchmakingEventType.RoomLeft: return "room_left";
                case MatchmakingEventType.ActorJoined: return "actor_joined";
                case MatchmakingEventType.ActorLeft: return "actor_left";
                case MatchmakingEventType.RoomClosed: return "room_closed";
                default: return "unknown";
            }
        }

        /// <summary>
        /// Convert MultiplayerEventType to string for logging/debugging
        /// </summary>
        public static string ToLogString(this MultiplayerEventType eventType)
        {
            switch (eventType)
            {
                case MultiplayerEventType.UNSET_VALUE: return "unset";
                case MultiplayerEventType.Message: return "message";
                case MultiplayerEventType.Position: return "position";
                case MultiplayerEventType.Competition: return "competition";
                case MultiplayerEventType.Leaderboard: return "leaderboard";
                default: return "unknown";
            }
        }

        /// <summary>
        /// Parse string to MatchmakingEventType
        /// </summary>
        public static MatchmakingEventType ParseMatchmakingEventType(string eventTypeString)
        {
            switch (eventTypeString?.ToLowerInvariant())
            {
                case "room_joined": return MatchmakingEventType.RoomJoined;
                case "room_left": return MatchmakingEventType.RoomLeft;
                case "actor_joined": return MatchmakingEventType.ActorJoined;
                case "actor_left": return MatchmakingEventType.ActorLeft;
                case "room_closed": return MatchmakingEventType.RoomClosed;
                default: throw new ArgumentException($"Unknown matchmaking event type: {eventTypeString}");
            }
        }

        /// <summary>
        /// Parse string to MultiplayerEventType
        /// </summary>
        public static MultiplayerEventType ParseMultiplayerEventType(string eventTypeString)
        {
            switch (eventTypeString?.ToLowerInvariant())
            {
                case "message": return MultiplayerEventType.Message;
                case "position": return MultiplayerEventType.Position;
                case "competition": return MultiplayerEventType.Competition;
                case "leaderboard": return MultiplayerEventType.Leaderboard;
                default: throw new ArgumentException($"Unknown multiplayer event type: {eventTypeString}");
            }
        }

        /// <summary>
        /// Check if MatchmakingEventType is a valid event type (not UNSET_VALUE)
        /// </summary>
        public static bool IsValidEventType(this MatchmakingEventType eventType)
        {
            return eventType != MatchmakingEventType.UNSET_VALUE && Enum.IsDefined(typeof(MatchmakingEventType), eventType);
        }

        /// <summary>
        /// Check if MultiplayerEventType is a valid event type (not UNSET_VALUE)
        /// </summary>
        public static bool IsValidEventType(this MultiplayerEventType eventType)
        {
            return eventType != MultiplayerEventType.UNSET_VALUE && Enum.IsDefined(typeof(MultiplayerEventType), eventType);
        }

        /// <summary>
        /// Convert SDKEventType to string for logging/debugging
        /// </summary>
        public static string ToLogString(this SDKEventType eventType)
        {
            switch (eventType)
            {
                case SDKEventType.UNSET_VALUE: return "unset";
                case SDKEventType.OnConnect: return "onConnect";
                case SDKEventType.OnJoinedLobby: return "onJoinedLobby";
                case SDKEventType.OnJoinRoom: return "onJoinRoom";
                case SDKEventType.OnRoomListUpdate: return "onRoomListUpdate";
                case SDKEventType.OnRoomActorChange: return "onRoomActorChange";
                case SDKEventType.OnRoomClosed: return "onRoomClosed";
                case SDKEventType.OnError: return "onError";
                case SDKEventType.StateChange: return "stateChange";
                default: return "unknown";
            }
        }

        /// <summary>
        /// Parse string to SDKEventType
        /// </summary>
        public static SDKEventType ParseSDKEventType(string eventTypeString)
        {
            switch (eventTypeString?.ToLowerInvariant())
            {
                case "onconnect": return SDKEventType.OnConnect;
                case "onjoinedlobby": return SDKEventType.OnJoinedLobby;
                case "onjoinroom": return SDKEventType.OnJoinRoom;
                case "onroomlistupdate": return SDKEventType.OnRoomListUpdate;
                case "onroomactorchange": return SDKEventType.OnRoomActorChange;
                case "onroomclosed": return SDKEventType.OnRoomClosed;
                case "onerror": return SDKEventType.OnError;
                case "statechange": return SDKEventType.StateChange;
                default: throw new ArgumentException($"Unknown SDK event type: {eventTypeString}");
            }
        }

        /// <summary>
        /// Check if SDKEventType is a valid event type (not UNSET_VALUE)
        /// </summary>
        public static bool IsValidEventType(this SDKEventType eventType)
        {
            return eventType != SDKEventType.UNSET_VALUE && Enum.IsDefined(typeof(SDKEventType), eventType);
        }

        /// <summary>
        /// Check if EntityType is a valid entity type (not UNSET_VALUE)
        /// </summary>
        public static bool IsValidEntityType(this EntityType entityType)
        {
            return entityType != EntityType.UNSET_VALUE && Enum.IsDefined(typeof(EntityType), entityType);
        }

        /// <summary>
        /// Convert EntityType to string for logging/debugging
        /// </summary>
        public static string ToLogString(this EntityType entityType)
        {
            switch (entityType)
            {
                case EntityType.UNSET_VALUE: return "unset";
                case EntityType.Player: return "player";
                case EntityType.Entity: return "entity";
                default: return "unknown";
            }
        }
    }


    /// <summary>
    /// Actor information for multiplayer matchmaking with direct property access
    /// Based on actual usage patterns in working JavaScript examples
    /// Matches: properties: { level: playerLevel, skill: playerSkill }
    /// Eliminates SerializableKeyValuePair serialization issues and provides direct property access
    /// </summary>
    [Serializable, Preserve]
    public class ActorInfo
    {
        [Preserve] public string session_id;
        [Preserve] public string name;

        // Direct properties instead of SerializableKeyValuePair[] - matches JavaScript format
        [Preserve] public int level;
        [Preserve] public int skill;

        public ActorInfo()
        {
            level = 1;
            skill = 1;
        }

        public ActorInfo(string sessionId, string playerName, int playerLevel = 1, int playerSkill = 1)
        {
            session_id = sessionId;
            name = playerName;
            level = playerLevel;
            skill = playerSkill;
        }
    }


    /// <summary>
    /// Room configuration for creating multiplayer rooms with direct property access
    /// Based on actual usage patterns in working JavaScript examples
    /// Working examples use: properties: {} (empty)
    /// But Unity examples use purpose, created_by, so we include common fields
    /// Eliminates SerializableKeyValuePair serialization issues and provides direct property access
    /// </summary>
    [Serializable, Preserve]
    public class RoomInfo
    {
        [Preserve] public string name;
        [Preserve] public string mode;
        [Preserve] public int maxPlayers;
        [Preserve] public int minPlayers;

        // Common properties based on Unity usage patterns
        [Preserve] public string purpose;
        [Preserve] public string created_by;
        [Preserve] public string difficulty;
        [Preserve] public string game_type;

        public RoomInfo()
        {
            maxPlayers = 4;
            minPlayers = 1;
            purpose = "";
            created_by = "";
            difficulty = "";
            game_type = "";
        }

        public RoomInfo(string roomName, string gameMode, int max, int min)
        {
            name = roomName;
            mode = gameMode;
            maxPlayers = max;
            minPlayers = min;
            purpose = "";
            created_by = "";
            difficulty = "";
            game_type = "";
        }
    }


    /// <summary>
    /// Room data returned from matchmaking operations with direct property access
    /// Based on common room properties we might receive
    /// Eliminates SerializableKeyValuePair serialization issues and provides direct property access
    /// </summary>
    [Serializable, Preserve]
    public class RoomData
    {
        [Preserve] public string roomId;
        [Preserve] public string name;
        [Preserve] public string mode;
        [Preserve] public int currentPlayers;
        [Preserve] public int maxPlayers;
        [Preserve] public bool isJoinable;
        [Preserve] public string hostUserId;
        [Preserve] public double createTime;
		public long CreateTime => (long)createTime;

        // Common room properties that might be received from server
        [Preserve] public string purpose;
        [Preserve] public string created_by;
        [Preserve] public string difficulty;
        [Preserve] public string game_type;
        [Preserve] public string status;

        /// <summary>
        /// Compatibility property for JavaScript SDK which uses 'id' instead of 'roomId'
        /// </summary>
        public string id
        {
            get => roomId;
            set => roomId = value;
        }

        public RoomData()
        {
            roomId = "";
            name = "";
            mode = "";
            currentPlayers = 0;
            maxPlayers = 4;
            isJoinable = true;
            hostUserId = "";
            createTime = 0;
            purpose = "";
            created_by = "";
            difficulty = "";
            game_type = "";
            status = "";
        }
    }



    /// <summary>
    /// 3D/4D position data for multiplayer synchronization
    /// </summary>
    [Serializable, Preserve]
    public class PositionData
    {
        [Preserve] public float x;
        [Preserve] public float y;
        [Preserve] public float z;
        [Preserve] public float w;

        public PositionData() { }

        public PositionData(float x, float y, float z, float w = 0f)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public PositionData(Vector3 position, float w = 0f)
        {
            this.x = position.x;
            this.y = position.y;
            this.z = position.z;
            this.w = w;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    /// <summary>
    /// Position update notification from multiplayer system
    /// </summary>
    [Serializable, Preserve]
    public class PositionUpdate
    {
        [Preserve] public int entity_type; // Serialized as int for JSON compatibility
        [Preserve] public string user_id;
        [Preserve] public string entity_id;
        [Preserve] public PositionData data;

        /// <summary>
        /// Get the entity type as a strongly-typed enum
        /// </summary>
        public EntityType EntityType
        {
            get => (EntityType)entity_type;
            set => entity_type = (int)value;
        }

        public bool IsPlayerUpdate => EntityType == EntityType.Player;
        public bool IsEntityUpdate => EntityType == EntityType.Entity;
    }


    /// <summary>
    /// Competition action data for multiplayer action sync with direct property access
    /// Based on actual usage patterns in working JavaScript examples
    /// Matches: competition(actionName, actionMessage, actionId)
    /// And result: { action_name: string, successor: string }
    /// Eliminates SerializableKeyValuePair serialization issues and provides direct property access
    /// </summary>
    [Serializable, Preserve]
    public class CompetitionData
    {
        [Preserve] public string action_name;
        [Preserve] public string successor; // Winner ID
        [Preserve] public double timestamp;
		public long TimeStamp => (long)timestamp;

        // Common competition properties based on JavaScript API usage
        [Preserve] public string action_message;
        [Preserve] public string action_id;
        [Preserve] public string competition_type;
        [Preserve] public int score;

        public CompetitionData()
        {
            action_name = "";
            successor = "";
            action_message = "";
            action_id = "";
            competition_type = "";
            score = 0;
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Competition result from multiplayer action sync
    /// </summary>
    [Serializable, Preserve]
    public class CompetitionResult
    {
        [Preserve] public CompetitionData competition;
    }

    /// <summary>
    /// Individual real-time multiplayer leaderboard entry
    /// </summary>
    [Serializable, Preserve]
    public class RealtimeLeaderboardEntry
    {
        [Preserve] public string user_id;
        [Preserve] public float score;
        [Preserve] public int rank;
        [Preserve] public string name;        // Player display name
        [Preserve] public double timestamp;
        public long TimeStamp => (long)timestamp;
    }

    /// <summary>
    /// Real-time leaderboard update from multiplayer system
    /// Matches JavaScript structure: { leaderboard: [...] }
    /// </summary>
    [Serializable, Preserve]
    public class LeaderboardUpdate
    {
        [Preserve] public RealtimeLeaderboardEntry[] leaderboard;  // Array of leaderboard entries
        [Preserve] public double timestamp;
        public long TimeStamp => (long)timestamp;
        public LeaderboardUpdate()
        {
            leaderboard = new RealtimeLeaderboardEntry[0];
        }
    }

    /// <summary>
    /// General message from multiplayer communication
    /// Matches JavaScript structure: { type, text, timestamp, sender }
    /// </summary>
    [Serializable, Preserve]
    public class GeneralMessage
    {
        [Preserve] public string type;        // e.g., "chat"
        [Preserve] public string text;        // Message content
        [Preserve] public double timestamp;     // Unix timestamp
        public long TimeStamp => (long)timestamp;
        [Preserve] public string sender;      // Player name who sent the message

        public GeneralMessage()
        {
            type = "";
            text = "";
            sender = "";
        }
    }

    /// <summary>
    /// Multiplayer session information returned from initialization
    /// </summary>
    [Serializable, Preserve]
    public class MultiplayerSessionInfo
    {
        [Preserve] public string sessionId;
        [Preserve] public string roomId;
        [Preserve] public string userId;
        [Preserve] public string appId;
    }

    /// <summary>
    /// Remove notification for entities/players leaving
    /// </summary>
    [Serializable, Preserve]
    public class RemoveNotification
    {
        [Preserve] public int entity_type; // Serialized as int for JSON compatibility
        [Preserve] public string user_id;
        [Preserve] public string entity_id;
        [Preserve] public double timestamp;
        public long TimeStamp => (long)timestamp;
        /// <summary>
        /// Get the entity type as a strongly-typed enum
        /// </summary>
        public EntityType EntityType
        {
            get => (EntityType)entity_type;
            set => entity_type = (int)value;
        }

        public bool IsPlayerRemove => EntityType == EntityType.Player;
        public bool IsEntityRemove => EntityType == EntityType.Entity;
    }

    // ============================================
    // NETWORKING EVENT DATA (Standard ViverseResult<T> Pattern)
    // ============================================

    /// <summary>
    /// Event data structure for networking events, compatible with standard ViverseResult<T> pattern
    /// Used in Payload field of ViverseResult<NetworkEventData>
    /// </summary>
    [Serializable, Preserve]
    public class NetworkEventData
    {
        [Preserve] public int EventCategory; // 1 = Matchmaking, 2 = Multiplayer
        [Preserve] public int EventType;
        [Preserve] public string EventData; // JSON serialized payload
        [Preserve] public double Timestamp;
        public long TimeStamp => (long)Timestamp;
        public bool IsMatchmakingEvent => EventCategory == 1;
        public bool IsMultiplayerEvent => EventCategory == 2;

        public MatchmakingEventType MatchmakingEventType => (MatchmakingEventType)EventType;
        public MultiplayerEventType MultiplayerEventType => (MultiplayerEventType)EventType;

        public NetworkEventData()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public NetworkEventData(int category, int type, object payload = null)
        {
            EventCategory = category;
            EventType = type;
            EventData = payload != null ? JsonUtility.ToJson(payload) : "{}";
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Helper to parse event data to specific type
        /// </summary>
        public T ParseEventData<T>() where T : class
        {
            if (string.IsNullOrEmpty(EventData))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(EventData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse event data: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Raw SDK event data structure for low-level SDK state changes
    /// Used with ViverseResult<SDKEventData> pattern for strongly-typed SDK events
    /// </summary>
    [Serializable, Preserve]
    public class SDKEventData
    {
        [Preserve] public int EventType; // SDKEventType as int for JSON compatibility
        [Preserve] public string EventData; // JSON serialized payload
        [Preserve] public double Timestamp;
        public long TimeStamp => (long)Timestamp;

        public SDKEventType SDKEventType => (SDKEventType)EventType;

        public SDKEventData()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public SDKEventData(SDKEventType type, object payload = null)
        {
            EventType = (int)type;
            EventData = payload != null ? JsonUtility.ToJson(payload) : "{}";
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Helper to parse event data to specific type
        /// </summary>
        public T ParseEventData<T>() where T : class
        {
            if (string.IsNullOrEmpty(EventData))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(EventData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse SDK event data: {e.Message}");
                return null;
            }
        }
    }

    // ============================================
    // SDK EVENT PAYLOAD DATA STRUCTURES
    // ============================================

    /// <summary>
    /// Connection state data for OnConnect events
    /// </summary>
    [Serializable, Preserve]
    public class ConnectionEventData
    {
        [Preserve] public bool connected;
        [Preserve] public string message;
    }

    /// <summary>
    /// Lobby join data for OnJoinedLobby events
    /// </summary>
    [Serializable, Preserve]
    public class LobbyEventData
    {
        [Preserve] public bool joined;
        [Preserve] public string lobbyId;
    }

    /// <summary>
    /// Room join data for OnJoinRoom events
    /// </summary>
    [Serializable, Preserve]
    public class RoomJoinEventData
    {
        [Preserve] public string roomId;
        [Preserve] public string roomName;
        [Preserve] public int currentPlayers;
        [Preserve] public int maxPlayers;
    }

    /// <summary>
    /// Room list update data for OnRoomListUpdate events
    /// </summary>
    [Serializable, Preserve]
    public class RoomListEventData
    {
        [Preserve] public RoomData[] rooms;
        [Preserve] public int totalRooms;
    }

    /// <summary>
    /// Room actor change data for OnRoomActorChange events
    /// </summary>
    [Serializable, Preserve]
    public class RoomActorEventData
    {
        [Preserve] public ActorInfo[] actors;
        [Preserve] public int totalActors;
    }

    /// <summary>
    /// SDK error data for OnError events
    /// </summary>
    [Serializable, Preserve]
    public class SDKErrorEventData
    {
        [Preserve] public string error;
        [Preserve] public string code;
        [Preserve] public string details;
    }

    /// <summary>
    /// State change data for StateChange events
    /// </summary>
    [Serializable, Preserve]
    public class StateChangeEventData
    {
        [Preserve] public string state;
        [Preserve] public string previousState;
    }

    /// <summary>
    /// SDK state information for monitoring readiness and connection status
    /// </summary>
    [Serializable, Preserve]
    public class SDKStateInfo
    {
        [Preserve] public bool connected;
        [Preserve] public bool joinedLobby;
        [Preserve] public bool actorSet;
        [Preserve] public ActorStateInfo actorInfo; // Include actor information for validation
        [Preserve] public string currentRoomId;
        [Preserve] public string currentState;
        [Preserve] public int pendingOperations;
        [Preserve] public bool isReadyForRoomOperations;
    }

    /// <summary>
    /// Actor state information stored in SDK state
    /// </summary>
    [Serializable, Preserve]
    public class ActorStateInfo
    {
        [Preserve] public string session_id;
        [Preserve] public string name;
        [Preserve] public int level;
        [Preserve] public int skill;
    }
}

