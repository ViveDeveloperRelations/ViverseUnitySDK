# Version 0.0.8 - SDK v1.2.9 Migration - YYYY-MM-DD

### Changed
- **BREAKING**: Updated Viverse JavaScript SDK from v1.2.3 to v1.2.9
- **BREAKING**: Renamed authentication method `LoginWithRedirect` to `LoginWithWorlds` to match SDK v1.2.9 API
- **BREAKING**: Changed `expire_in` field to `expires_in` in authentication responses
- Updated token format for leaderboard initialization (now uses string instead of object)
- Improved error handling for undefined returns from `checkAuth()`
- Updated all samples (ViverseTestUI, ViverseSDKSmokeTest, HTML page) to use new authentication methods

### Added
- **OAuth Callback Detection**: Automatic OAuth callback detection and handling in `InitializeCore()`
- **Force Client Reinitialization**: Added `ForceReinitializeClient()` method for error recovery
- **Timeout Protection**: 10-second timeout protection for `CheckAuth()` operations using Promise.race()
- **Memory Leak Prevention**: Comprehensive cleanup system (`_cleanupClientInstances()`) preventing memory leaks during client reinitialization
- **Defensive Programming**: SafeCheckAuth pattern handles undefined returns from authentication flow
- Enhanced error messages and logging for authentication failures
- **NEW: Room-Based Multiplayer System** - Added comprehensive multiplayer support following v1.2.9 SDK architecture
  - Added `ViverseRoom` class for strongly-typed multiplayer event subscriptions
  - Implemented proper service flow: PlayService → MatchmakingService → MultiplayerService
  - Added `ViverseCore.CreateRoomManager(appId)` for creating room managers
  - Fixed data structures to match JavaScript SDK v1.2.9 (GeneralMessage, LeaderboardUpdate, etc.)
  - Added comprehensive logging for debugging multiplayer parsing failures
  - Enhanced ViverseEventDispatcher with room-aware event routing
  - Updated ViverseSDKSmokeTest with complete room-based multiplayer testing
  - Fixed null reference exceptions and race conditions in event handling
  - Added EntityType enum with UNSET_VALUE = 0 pattern for initialization safety
- Local server allows compression by default both brotli and gzip in unity builds
- User is recommended to use decompression fallback by default to prevent issues with servers that don't support decompression

### Fixed
- **Memory Leaks**: Comprehensive cleanup prevents orphaned event listeners and client references during reinitialization
- **Authentication Timeouts**: Prevent hanging CheckAuth() calls with 10-second timeout protection
- **OAuth Flow Issues**: Automatic client reinitialization when OAuth callback parameters detected
- **Authentication State Corruption**: ForceReinitializeClient() provides recovery mechanism for corrupted auth state
- Token format compatibility with v1.2.9 leaderboard API
- CheckAuth undefined handling to prevent runtime errors
- Fixed JavaScript memory management in multiplayer event handlers

### Deprecated
- `LoginWithRedirect` method (will redirect to `LoginWithWorlds` automatically)

### Migration Guide
To update from v0.0.7 to v0.0.8:
1. **OAuth Handling**: No changes needed - OAuth callbacks are now handled automatically in `InitializeCore()`
2. **Error Recovery**: Use `ForceReinitializeClient()` method for authentication recovery instead of manual client recreation
3. **Authentication**: Replace all calls to `LoginWithRedirect` with `LoginWithWorlds`
4. **Data Fields**: Update any code parsing `expire_in` to use `expires_in`
5. **Memory Management**: Remove any manual cleanup code - memory leak prevention is now automatic
6. Test authentication flow thoroughly as behavior may differ
7. No manual changes needed for most cases - deprecated methods will automatically redirect

### Benefits for Developers
- **Simplified OAuth**: Authentication "just works" without manual OAuth callback handling
- **Better Reliability**: Timeout protection prevents hanging authentication calls
- **Memory Safety**: No more memory leaks during client reinitialization
- **Error Recovery**: Built-in recovery mechanisms for authentication issues
- **Null-Safe Logging**: Extension methods prevent NullReferenceExceptions in error handling
- **Smart Recovery Detection**: Automatic detection of recoverable errors

### v0.0.8 Safety Improvements
```csharp
// ❌ Before (Vulnerable to NRE)
Debug.LogError($"Error: {result.RawResult.Message}");     // May throw NRE
Debug.LogError($"Payload: {result.RawResult.Payload}");   // May throw NRE

// ✅ After (Null-Safe)
result.LogError("OperationName");                         // Safe extension method
Debug.LogError($"Error: {result.SafeMessage}");           // Never null
Debug.LogError($"Details: {result.GetErrorDetails()}");   // Parsed with fallback

if (result.IsRecoverableError()) { /* Smart recovery */ } // Built-in detection
```

# unreleased

# Version 0.0.7 - 25 april 2025
- node server dependency defaults set correctly, so fresh installs should work after express default version went from 4x to 5x (which is not compatible with unity's npm/npx)

# Version 0.0.6 - 13 march 2025
- initial mac in-editor browser support

# Version 0.0.5 - 11 march 2025
- Link to leaderboard/achievement setup document

# Version 0.0.4 - 11 march 2025
- Add in achievement api calls, updated smoketest and viversetestui.cs/configurabledriver scene to include them

# Version 0.0.3 - 10 march 2025
- Visual references for README instructions

# Version 0.0.2 - 10 march 2025
- avatar previews are more consistently centered
- urp fixes to camera preview
- fix package importing occasionally hanging
- decompression fallback turn off by default
- WebGL Settings window "Disable Decompression Fallback" toggle now also sets compression format to None for improved browser compatibility


# Version 0.0.1 - 3 march 2025
- initial version

