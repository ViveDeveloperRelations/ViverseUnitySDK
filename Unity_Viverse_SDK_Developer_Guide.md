# Unity Viverse SDK Developer Guide

Based on analysis of `ViverseSDKSmokeTest.cs`, this guide provides comprehensive documentation for developers implementing the Viverse SDK API flow, covering authentication, avatar management, leaderboards, and achievements.

## Table of Contents
1. [Core Architecture](#core-architecture)
2. [Initialization Flow](#initialization-flow)
3. [Authentication (SSO) Service](#authentication-sso-service)
4. [Avatar Service](#avatar-service)
5. [Leaderboard Service](#leaderboard-service)
6. [Achievement System](#achievement-system)
7. [Known Limitations](#known-limitations)
8. [Error Handling Patterns](#error-handling-patterns)
9. [Best Practices](#best-practices)

## Core Architecture

### SDK Initialization Pattern
The Unity Viverse SDK follows a hierarchical initialization pattern:

```csharp
// Step 1: Initialize Core SDK
ViverseCore _core = new ViverseCore();
HostConfig _hostConfig = GetEnvironmentConfig();
ViverseResult<bool> initResult = await _core.Initialize(_hostConfig, cancellationToken);

// Step 2: Initialize individual services as needed
await _core.SSOService.Initialize(clientId);
await _core.AvatarService.Initialize();
await _core.LeaderboardService.Initialize();
```

### Environment Configuration
```csharp
private HostConfig GetEnvironmentConfig()
{
    HostConfigUtil.HostType hostType = 
        new HostConfigUtil().GetHostTypeFromPageURLIfPossible(Application.absoluteURL);
    return HostConfigLookup.HostTypeToDefaultHostConfig.TryGetValue(hostType, out var config)
        ? config
        : HostConfigLookup.HostTypeToDefaultHostConfig[HostConfigUtil.HostType.PROD];
}
```

### Application Configuration
```csharp
// Note: In production, ClientID and AppID will be the same value
// For development/testing, they may be different
private string AppID = "your-app-id-here";
private string ClientID = AppID; // Will be unified in production
```

## Initialization Flow

### Complete Initialization Sequence
```csharp
private async Task InitializeViverse()
{
    try
    {
        // 1. Get environment configuration
        _hostConfig = GetEnvironmentConfig();
        
        // 2. Initialize core SDK
        _core = new ViverseCore();
        ViverseResult<bool> initResult = await _core.Initialize(_hostConfig, destroyCancellationToken);
        if (!initResult.IsSuccess)
        {
            initResult.LogError("SDK Core Initialize");
            throw new Exception($"SDK initialization failed: {initResult.ErrorMessage}");
        }

        // 3. Initialize SSO (Authentication) - REQUIRED FIRST
        bool ssoSuccess = await InitializeSSO();
        if (!ssoSuccess) return;

        // 4. Initialize other services only after successful authentication
        await TestLeaderboardFunctionality();
        await TestAchievementFunctionality();
        await InitializeAvatarService();
    }
    catch (Exception e)
    {
        Debug.LogError($"Error during Viverse initialization: {e.Message}");
    }
}
```

### Critical Dependencies
- **SSO MUST be initialized first** before any authenticated services
- **Authentication token is required** for Avatar, Leaderboard, and Achievement services
- **Each service requires individual initialization** after core SDK setup

## Authentication (SSO) Service

### Service Initialization
```csharp
private async Task<bool> InitializeSSO()
{
    try
    {
        // Initialize SSO service with app ID (used as client ID)
        bool ssoInitSuccess = await _core.SSOService.Initialize(AppID);
        
        // Check for existing authentication
        if (await AlreadyHadAccessToken())
        {
            return true;
        }
        
        // Trigger new login if needed
        LoginResult loginResult = await DoLogin();
        return loginResult != null;
    }
    catch (Exception e)
    {
        Debug.LogError($"SSO initialization failed: {e.Message}");
        return false;
    }
}
```

### Checking Existing Authentication
```csharp
private async Task<bool> AlreadyHadAccessToken()
{
    ViverseResult<LoginResult> tokenResult = await _core.SSOService.CheckAuth();
    if (!tokenResult.IsSuccess)
    {
        tokenResult.LogError("Get Access Token");
        return false;
    }

    if (tokenResult.Data?.access_token != null)
    {
        Debug.Log("Got access token immediately, already logged in");
        _authKey = new AuthKey(tokenResult.Data.access_token);
        return true;
    }

    return false;
}
```

### Login Flow
```csharp
private async Task<LoginResult> DoLogin()
{
    Debug.Log("Initiating enhanced login with LoginWithWorlds");
    
    ViverseResult<LoginResult> loginResult = await _core.SSOService.LoginWithWorlds();
    if (!loginResult.IsSuccess)
    {
        loginResult.LogError("Login With Worlds failed");
        if (loginResult.IsRecoverableError())
        {
            Debug.LogWarning("Login failed with recoverable error - user may try again");
        }
        return null;
    }

    string authToken = loginResult?.Data?.access_token;
    if (string.IsNullOrEmpty(authToken))
    {
        Debug.LogError("Login failed: No auth token received");
        return null;
    }

    // Store authentication key for service access
    _authKey = new AuthKey(loginResult.Data.access_token);
    return loginResult.Data;
}
```

### Authentication Requirements
- **App ID**: Required for SSO initialization (serves as Client ID)
- **Access Token**: Required for all authenticated service calls
- **Auth Key Storage**: Must store `AuthKey` object for service authentication
- **Login State**: Check existing auth before triggering new login

**Note**: In production environments, the App ID and Client ID are unified into a single identifier. During development and testing, these may be separate values, but the App ID should be used consistently throughout your application.

## Avatar Service

### Service Initialization
```csharp
private async Task InitializeAvatarService()
{
    ViverseResult<bool> avatarInit = await _core.AvatarService.Initialize();
    if (!avatarInit.IsSuccess)
    {
        avatarInit.LogError("Initialize Avatar Service");
        return;
    }
    
    Debug.Log("✅ Avatar service initialized successfully");
    
    // Now safe to call avatar methods
    await TestGetProfile();
    await TestGetActiveAvatar();
    // ... other avatar operations
}
```

### User Profile Management
```csharp
// Get user profile information
private async Task TestGetProfile()
{
    ViverseResult<UserProfile> profile = await _core.AvatarService.GetProfile();
    
    if (profile.IsSuccess && profile.Data != null)
    {
        Debug.Log($"User Name: {profile.Data.name}");
        Debug.Log($"Active Avatar: {(profile.Data.activeAvatar != null ? "Present" : "null")}");
        
        if (profile.Data.activeAvatar != null)
        {
            Debug.Log($"Active Avatar ID: {profile.Data.activeAvatar.id}");
            Debug.Log($"Is Private: {profile.Data.activeAvatar.isPrivate}");
            Debug.Log($"VRM URL: {profile.Data.activeAvatar.vrmUrl}");
        }
    }
    else
    {
        profile.LogError("GetProfile");
    }
}
```

### Active Avatar Management
```csharp
// Get user's currently active avatar
private async Task TestGetActiveAvatar()
{
    ViverseResult<Avatar> activeAvatarResult = await _core.AvatarService.GetActiveAvatar();
    
    if (activeAvatarResult.IsSuccess)
    {
        if (activeAvatarResult.Data != null)
        {
            var avatar = activeAvatarResult.Data;
            Debug.Log($"Avatar ID: {avatar.id}");
            Debug.Log($"Is Private: {avatar.isPrivate}");
            Debug.Log($"VRM URL: {avatar.vrmUrl}");
            Debug.Log($"Head Icon URL: {avatar.headIconUrl}");
            Debug.Log($"Snapshot URL: {avatar.snapshot}");
            Debug.Log($"Create Time: {avatar.createTime}");
            Debug.Log($"Update Time: {avatar.updateTime}");
        }
        else
        {
            Debug.Log("No active avatar set");
        }
    }
    else
    {
        activeAvatarResult.LogError("GetActiveAvatar");
    }
}
```

### Avatar List Management
```csharp
// Get user's avatar collection
ViverseResult<AvatarListWrapper> avatarListResult = await _core.AvatarService.GetAvatarList();
if (avatarListResult.IsSuccess && avatarListResult.Data?.avatars != null)
{
    List<Avatar> avatarList = new List<Avatar>(avatarListResult.Data.avatars);
    Debug.Log($"Found {avatarList.Count} avatars");
    
    foreach (var avatar in avatarList)
    {
        Debug.Log($"Avatar ID: {avatar.id}, Private: {avatar.isPrivate}");
    }
}

// Get public avatars (no authentication required)
ViverseResult<AvatarListWrapper> publicAvatarsResult = await _core.AvatarService.GetPublicAvatarList();
if (publicAvatarsResult.IsSuccess && publicAvatarsResult.Data?.avatars != null)
{
    Debug.Log($"Found {publicAvatarsResult.Data.avatars.Length} public avatars");
}

// Get specific public avatar by ID
ViverseResult<Avatar> avatarByIdResult = await _core.AvatarService.GetPublicAvatarByID("avatar-id");
if (avatarByIdResult.IsSuccess && avatarByIdResult.Data != null)
{
    Debug.Log($"Retrieved avatar: {avatarByIdResult.Data.id}");
}
```

### Avatar Data Structure
```csharp
public class Avatar
{
    public string id;               // Avatar identifier
    public bool isPrivate;          // Private (user-owned) vs public avatar
    public string vrmUrl;           // URL to VRM model file
    public string headIconUrl;      // URL to avatar head icon
    public string snapshot;         // URL to avatar preview image
    public long createTime;         // Creation timestamp (milliseconds)
    public long updateTime;         // Last update timestamp (milliseconds)
}

public class UserProfile
{
    public string name;             // User display name
    public Avatar activeAvatar;     // Currently selected avatar (null if none)
}
```

## Leaderboard Service

### Service Setup
```csharp
// Initialize leaderboard service (requires authentication)
ViverseResult<bool> initResult = await _core.LeaderboardService.Initialize();
if (!initResult.IsSuccess)
{
    initResult.LogError("Initialize Leaderboard Service");
    return false;
}
```

### Score Upload
```csharp
private async Task<bool> TestPostScore(string leaderboardName)
{
    string appId = AppID; // Use the same App ID throughout
    string testScore = "1000";

    ViverseResult<LeaderboardResult> scoreResult = 
        await _core.LeaderboardService.UploadScore(appId, leaderboardName, testScore);
        
    if (!scoreResult.IsSuccess)
    {
        scoreResult.LogError("Upload Score");
        return false;
    }

    LeaderboardResult result = scoreResult.Data;
    Debug.Log($"Uploaded score. Total entries: {result.total_count}");
    
    if (result.ranking != null)
    {
        foreach (LeaderboardEntry entry in result.ranking)
        {
            Debug.Log($"Rank {entry.rank}: {entry.name} - {entry.value}");
        }
    }

    return true;
}
```

### Score Retrieval with Configuration
```csharp
private async Task<bool> TestGetScores(string leaderboardName)
{
    string appId = AppID; // Use the same App ID throughout
    
    // Multiple configuration examples
    LeaderboardConfig[] configs = new[]
    {
        // Default configuration
        LeaderboardConfig.CreateDefault(leaderboardName),
        
        // Global leaderboard, all time, top 100
        new LeaderboardConfig
        {
            Name = leaderboardName,
            RangeStart = 0,
            RangeEnd = 100,
            Region = LeaderboardRegion.Global,
            TimeRange = LeaderboardTimeRange.Alltime,
            AroundUser = false
        },
        
        // Local leaderboard, weekly, specific range
        new LeaderboardConfig
        {
            Name = leaderboardName,
            RangeStart = 10,
            RangeEnd = 50,
            Region = LeaderboardRegion.Local,
            TimeRange = LeaderboardTimeRange.Weekly,
            AroundUser = false
        },
        
        // Around user rankings (must set AroundUser first)
        new LeaderboardConfig
        {
            AroundUser = true,
            Name = leaderboardName,
            RangeStart = -5,    // 5 positions before user
            RangeEnd = 10,      // 10 positions after user
            Region = LeaderboardRegion.Global,
            TimeRange = LeaderboardTimeRange.Monthly,
        }
    };

    foreach (LeaderboardConfig config in configs)
    {
        ViverseResult<LeaderboardResult> result = 
            await _core.LeaderboardService.GetLeaderboardScores(appId, config);
            
        if (result.IsSuccess)
        {
            Debug.Log($"Total entries: {result.Data.total_count}");
            if (result.Data.ranking != null)
            {
                foreach (LeaderboardEntry entry in result.Data.ranking)
                {
                    Debug.Log($"Rank {entry.rank}: {entry.name} - {entry.value}");
                }
            }
        }
        else
        {
            result.LogError("Get Leaderboard Scores");
        }
    }

    return true;
}
```

### Leaderboard Data Structures
```csharp
public class LeaderboardConfig
{
    public string Name;                     // Leaderboard identifier
    public int RangeStart;                  // Starting position (0-based)
    public int RangeEnd;                    // Ending position
    public LeaderboardRegion Region;        // Global, Local, etc.
    public LeaderboardTimeRange TimeRange; // Alltime, Daily, Weekly, Monthly
    public bool AroundUser;                 // Center results around current user
}

public class LeaderboardResult
{
    public int total_count;                 // Total number of entries
    public LeaderboardEntry[] ranking;      // Array of leaderboard entries
}

public class LeaderboardEntry
{
    public int rank;                        // Player's rank position
    public string name;                     // Player display name
    public string value;                    // Player's score value
}

public enum LeaderboardRegion
{
    Global,
    Local
}

public enum LeaderboardTimeRange
{
    Alltime,
    Daily,
    Weekly,
    Monthly
}
```

## Achievement System

### Achievement Retrieval
```csharp
private async Task<bool> GetUserAchievements()
{
    string appId = AppID; // Use the same App ID throughout
    ViverseResult<UserAchievementResult> result = 
        await _core.LeaderboardService.GetUserAchievement(appId);

    if (!result.IsSuccess)
    {
        Debug.LogError($"Failed to get user achievements: {result.ErrorMessage}");
        return false;
    }

    Debug.Log($"Got {result.Data.total} achievements");

    if (result.Data.achievements != null && result.Data.achievements.Length > 0)
    {
        foreach (var achievement in result.Data.achievements)
        {
            Debug.Log($"Achievement: {achievement.display_name} ({achievement.api_name})");
            Debug.Log($"  Description: {achievement.description}");
            Debug.Log($"  Unlocked: {achievement.is_achieved} (times: {achievement.achieved_times})");
        }
    }

    return true;
}
```

### Single Achievement Unlock
```csharp
private async Task<bool> UnlockAchievement(string achievementId)
{
    string appId = AppID; // Use the same App ID throughout

    // Create achievement object to unlock
    var achievements = new List<Achievement>
    {
        new Achievement
        {
            api_name = achievementId,
            unlock = true
        }
    };

    // Submit unlock request
    ViverseResult<AchievementUploadResult> result =
        await _core.LeaderboardService.UploadUserAchievement(appId, achievements);

    if (!result.IsSuccess)
    {
        Debug.LogError($"Failed to upload achievement: {result.ErrorMessage}");
        return false;
    }

    // Process results
    int successCount = result.Data?.success?.total ?? 0;
    int failureCount = result.Data?.failure?.total ?? 0;

    Debug.Log($"Achievement upload - Success: {successCount}, Failures: {failureCount}");

    // Log successful achievements
    if (successCount > 0 && result.Data.success.achievements != null)
    {
        foreach (var achievement in result.Data.success.achievements)
        {
            Debug.Log($"Successfully unlocked: {achievement.api_name} at {achievement.time_stamp}");
        }
    }

    // Log failed achievements
    if (failureCount > 0 && result.Data.failure.achievements != null)
    {
        foreach (var achievement in result.Data.failure.achievements)
        {
            Debug.LogWarning($"Failed to unlock: {achievement.api_name}, Code: {achievement.code}, Message: {achievement.message}");
        }
    }

    return successCount > 0;
}
```

### Batch Achievement Operations
```csharp
private async Task<bool> UnlockAchievementsBatch(string[] achievementIds)
{
    string appId = AppID; // Use the same App ID throughout

    // Create achievement objects for all IDs
    var achievements = new List<Achievement>();
    foreach (var id in achievementIds)
    {
        achievements.Add(new Achievement { api_name = id, unlock = true });
    }

    // Submit batch request
    ViverseResult<AchievementUploadResult> result =
        await _core.LeaderboardService.UploadUserAchievement(appId, achievements);

    if (!result.IsSuccess)
    {
        Debug.LogError($"Failed to upload achievements batch: {result.ErrorMessage}");
        return false;
    }

    int successCount = result.Data?.success?.total ?? 0;
    int failureCount = result.Data?.failure?.total ?? 0;

    Debug.Log($"Batch achievement result: {successCount} succeeded, {failureCount} failed");
    return successCount > 0;
}
```

### Achievement State Tracking
```csharp
// Helper method to track achievement states for verification
private async Task<Dictionary<string, bool>> GetAchievementStates()
{
    var achievementStates = new Dictionary<string, bool>();

    string appId = AppID; // Use the same App ID throughout
    ViverseResult<UserAchievementResult> result = 
        await _core.LeaderboardService.GetUserAchievement(appId);

    if (result.IsSuccess && result.Data.achievements != null)
    {
        foreach (var achievement in result.Data.achievements)
        {
            achievementStates[achievement.api_name] = achievement.is_achieved;
        }
    }

    return achievementStates;
}

// Sequential testing with verification
private async Task TestSequentialAchievementUnlocking()
{
    string[] achievementIds = { "achievement1", "achievement2", "achievement3" };
    Dictionary<string, bool> initialStates = await GetAchievementStates();

    foreach (var achievementId in achievementIds)
    {
        bool wasUnlocked = await UnlockAchievement(achievementId);
        Dictionary<string, bool> currentStates = await GetAchievementStates();

        bool initialState = initialStates.ContainsKey(achievementId) && initialStates[achievementId];
        bool currentState = currentStates.ContainsKey(achievementId) && currentStates[achievementId];

        if (wasUnlocked && !initialState && currentState)
        {
            Debug.Log($"✓ Successfully unlocked: {achievementId}");
        }
        else if (initialState)
        {
            Debug.Log($"ℹ Already unlocked: {achievementId}");
        }
        else
        {
            Debug.LogWarning($"⚠ Failed to verify: {achievementId}");
        }

        initialStates = currentStates;
    }
}
```

### Achievement Data Structures
```csharp
public class Achievement
{
    public string api_name;         // Achievement identifier
    public bool unlock;             // True to unlock, false to reset (if supported)
}

public class UserAchievementResult
{
    public int total;                           // Total number of achievements
    public UserAchievementInfo[] achievements;  // Array of user's achievements
}

public class UserAchievementInfo
{
    public string api_name;         // Achievement identifier
    public string display_name;     // Human-readable name
    public string description;      // Achievement description
    public bool is_achieved;        // Whether user has unlocked this
    public int achieved_times;      // Number of times achieved (for repeatable achievements)
}

public class AchievementUploadResult
{
    public AchievementResultSection success;    // Successfully processed achievements
    public AchievementResultSection failure;    // Failed achievement operations
}

public class AchievementResultSection
{
    public int total;                           // Count of achievements in this section
    public AchievementProcessedInfo[] achievements; // Details for each achievement
}

public class AchievementProcessedInfo
{
    public string api_name;         // Achievement identifier
    public long time_stamp;         // Processing timestamp (for successes)
    public int code;                // Error code (for failures)
    public string message;          // Error message (for failures)
}
```

## Known Limitations

### Multiplayer Functionality (Currently Unavailable)
**⚠️ Important**: The Viverse Unity SDK v1.2.9 includes multiplayer-related APIs and data structures, but **multiplayer functionality is currently non-functional and stubbed**. 

The following services are present in the SDK but not operational:
- `ViverseCore.MatchmakingServiceClass` - Room discovery and joining
- `ViverseCore.MultiplayerServiceClass` - Real-time communication  
- `ViverseCore.PlayServiceClass` - Foundation multiplayer service
- `ViverseRoom.cs` - High-level room management API
- `ViverseEventDispatcher.cs` - Event routing system

**Status**: Updates are expected once related state management logic is finalized.

### Local Development Testing (Currently Non-Functional)
**⚠️ Important**: Local development testing flow is currently not operational.

While the SDK includes local testing infrastructure:
- Node.js development server at `Editor/NodeServerForTesting/`
- HTTPS certificate management with mkcert
- Local server setup scripts

**Current Status**: Local testing requires uploading builds to https://studio.viverse.com/ for testing.

**Workaround**: Use the production testing flow:
1. Build your WebGL application
2. Create a zip file of the Build directory
3. Upload to https://studio.viverse.com/ for testing

### Known Service Issues
- **Leaderboard score uploads may fail** when using the app ID directly
  - **Workaround**: Create leaderboard-specific app IDs in Viveport Console
- **Inconsistent `appid`/`clientid` handling** due to legacy design
  - **Status**: Will be resolved with Viverse Studio flow alignment

## Error Handling Patterns

### ViverseResult Pattern
All SDK operations return `ViverseResult<T>` objects that provide comprehensive error information:

```csharp
ViverseResult<T> result = await someOperation();

// Check success/failure
if (result.IsSuccess)
{
    T data = result.Data;  // Access successful result
}
else
{
    // Error handling with multiple approaches
    result.LogError("Operation Name");  // Comprehensive logging
    
    // Check if error is recoverable
    if (result.IsRecoverableError())
    {
        Debug.LogWarning("This error may be recoverable - retry may succeed");
    }
    
    // Access detailed error information
    Debug.LogError($"Error: {result.ErrorMessage}");
    Debug.LogError($"Return Code: {result.RawResult.ReturnCode}");
    Debug.LogError($"Raw Message: {result.RawResult.Message}");
}
```

### Safe Logging Extensions
The SDK provides safe logging extensions for detailed error reporting:

```csharp
// Comprehensive error logging
result.LogError("Operation Name");

// Detailed logging for debugging
result.LogDetailed("Operation Name");

// Safe access to result properties
string safeMessage = result.SafeMessage;
T safePayload = result.SafePayload;
```

### Common Error Scenarios

#### Authentication Errors
```csharp
// SSO initialization failure
if (!ssoResult.IsSuccess)
{
    if (ssoResult.RawResult.ReturnCode == (int)ViverseSDKReturnCode.ErrorNetworkTimeout)
    {
        Debug.LogWarning("Network timeout - check connectivity");
    }
    else if (ssoResult.RawResult.ReturnCode == (int)ViverseSDKReturnCode.ErrorInvalidParameter)
    {
        Debug.LogError("Invalid App ID or configuration");
    }
}
```

#### Service Initialization Errors
```csharp
// Avatar service initialization
if (!avatarInit.IsSuccess)
{
    avatarInit.LogError("Initialize Avatar Service");
    
    if (avatarInit.IsRecoverableError())
    {
        Debug.LogWarning("Avatar service initialization failed but may be recoverable");
    }
    return; // Don't proceed with avatar operations
}
```

#### API Call Errors
```csharp
// Leaderboard score upload
if (!scoreResult.IsSuccess)
{
    scoreResult.LogError("Upload Score");
    
    // Specific error handling
    switch (scoreResult.RawResult.ReturnCode)
    {
        case (int)ViverseSDKReturnCode.ErrorNetworkTimeout:
            Debug.LogWarning("Score upload timed out - may retry");
            break;
        case (int)ViverseSDKReturnCode.ErrorInvalidParameter:
            Debug.LogError("Invalid score format or App ID");
            break;
        case (int)ViverseSDKReturnCode.ErrorUnauthorized:
            Debug.LogError("Authentication required or expired");
            break;
    }
}
```

## Best Practices

### 1. Initialization Order
```csharp
// REQUIRED ORDER:
// 1. Core SDK initialization
// 2. SSO service initialization and authentication
// 3. Individual service initialization (Avatar, Leaderboard)
// 4. Service operations

await _core.Initialize(_hostConfig, cancellationToken);
await InitializeSSO();  // Must complete successfully first
await _core.AvatarService.Initialize();
await _core.LeaderboardService.Initialize();
```

### 2. Authentication State Management
```csharp
// Always check authentication before proceeding
if (_authKey == null)
{
    Debug.LogWarning("No auth key - cannot proceed with authenticated operations");
    return;
}

// Store authentication state for service access
private AuthKey _authKey;
private UserInfo _userInfo;
```

### 3. Async/Await Pattern
```csharp
// All SDK operations are async and must be awaited
ViverseResult<T> result = await _core.SomeService.SomeOperation();

// Use proper cancellation token handling
ViverseResult<bool> initResult = await _core.Initialize(_hostConfig, destroyCancellationToken);
```

### 4. Resource Management
```csharp
// Use try-catch for exception handling
try
{
    ViverseResult<T> result = await operation();
    if (!result.IsSuccess)
    {
        result.LogError("Operation Name");
        return;
    }
    // Process successful result
}
catch (Exception e)
{
    Debug.LogError($"Exception during operation: {e.Message}");
    Debug.LogException(e);
}
```

### 5. Service Configuration
```csharp
// Use environment-appropriate configuration
private HostConfig GetEnvironmentConfig()
{
    // Automatically detect environment from URL
    HostConfigUtil.HostType hostType = 
        new HostConfigUtil().GetHostTypeFromPageURLIfPossible(Application.absoluteURL);
    
    // Fall back to production if detection fails
    return HostConfigLookup.HostTypeToDefaultHostConfig.TryGetValue(hostType, out var config)
        ? config
        : HostConfigLookup.HostTypeToDefaultHostConfig[HostConfigUtil.HostType.PROD];
}
```

### 6. Error Recovery Patterns
```csharp
// Check for recoverable errors and provide user guidance
if (!result.IsSuccess)
{
    result.LogError("Operation Name");
    
    if (result.IsRecoverableError())
    {
        Debug.LogWarning("Operation failed but may be recoverable");
        // Implement retry logic or user notification
    }
    else
    {
        Debug.LogError("Operation failed with non-recoverable error");
        // Handle permanent failure
    }
}
```

### 7. Data Validation
```csharp
// Validate data before processing
if (result.IsSuccess && result.Data != null)
{
    // Safe to access result.Data
    ProcessData(result.Data);
}
else if (result.IsSuccess && result.Data == null)
{
    Debug.Log("Operation succeeded but returned no data");
}
else
{
    // Handle error case
    result.LogError("Operation Name");
}
```

### 8. Configuration Management
```csharp
// Centralize configuration values
public class ViverseConfig
{
    // Primary App ID (used for all services)
    public const string APP_ID = "your-app-id-here";
    
    // Note: In production, Client ID = App ID
    // For testing, these may be different values
    public const string CLIENT_ID = APP_ID; 
    
    // Leaderboard names
    public const string LEADERBOARD_SCORES = "main_scores";
    public const string LEADERBOARD_WEEKLY = "weekly_challenge";
    
    // Achievement IDs
    public const string ACHIEVEMENT_FIRST_PLAY = "first_play";
    public const string ACHIEVEMENT_HIGH_SCORE = "high_score";
}
```

This guide provides a comprehensive foundation for implementing the Unity Viverse SDK, focusing on the core services while following the patterns demonstrated in the working smoke test implementation.