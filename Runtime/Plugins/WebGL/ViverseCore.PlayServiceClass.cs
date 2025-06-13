using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ViverseWebGLAPI
{
    public partial class ViverseCore
    {
        /// <summary>
        /// Service class for managing the Viverse Play system and matchmaking client creation
        /// </summary>
        public class PlayServiceClass
        {
            [DllImport("__Internal")]
            private static extern void Play_Initialize(int taskId, Action<string> callback);

            [DllImport("__Internal")]
            private static extern void Play_NewMatchmakingClient(string appId, bool debugMode, int taskId, Action<string> callback);

            private bool _isInitialized = false;

            /// <summary>
            /// Initialize the Play system
            /// </summary>
            /// <returns>Result indicating success or failure</returns>
            public async Task<ViverseResult<bool>> Initialize()
            {
                try
                {
                    var result = await CallNativeViverseFunction(Play_Initialize);
                    
                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        _isInitialized = true;
                        Debug.Log("Play service initialized successfully");
                        return ViverseResult<bool>.Success(true, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to initialize Play service: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
                        return ViverseResult<bool>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during Play service initialization: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during Play initialization: {e.Message}"
                    };
                    return ViverseResult<bool>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Create a new matchmaking client for the specified app
            /// </summary>
            /// <param name="appId">Application ID for matchmaking</param>
            /// <param name="debugMode">Enable debug mode for detailed logging</param>
            /// <returns>Result containing the created MatchmakingServiceClass</returns>
            public async Task<ViverseResult<MatchmakingServiceClass>> NewMatchmakingClient(string appId, bool debugMode = false)
            {
                if (!_isInitialized)
                {
                    var notInitializedResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorModuleNotLoaded,
                        Message = "Play service not initialized. Call Initialize() first."
                    };
                    return ViverseResult<MatchmakingServiceClass>.Failure(notInitializedResult);
                }

                if (string.IsNullOrEmpty(appId))
                {
                    var invalidParamResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorInvalidParameter,
                        Message = "App ID cannot be null or empty"
                    };
                    return ViverseResult<MatchmakingServiceClass>.Failure(invalidParamResult);
                }

                try
                {
                    void ClientWrapper(int taskId, Action<string> callback)
                    {
                        Play_NewMatchmakingClient(appId, debugMode, taskId, callback);
                    }

                    var result = await CallNativeViverseFunction(ClientWrapper);

                    if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success)
                    {
                        var matchmakingService = new MatchmakingServiceClass();
                        Debug.Log($"Matchmaking client created successfully for app: {appId}");
                        return ViverseResult<MatchmakingServiceClass>.Success(matchmakingService, result);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to create matchmaking client: {ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode)}");
                        return ViverseResult<MatchmakingServiceClass>.Failure(result);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception during matchmaking client creation: {e.Message}");
                    var errorResult = new ViverseSDKReturn
                    {
                        ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                        Message = $"Exception during matchmaking client creation: {e.Message}"
                    };
                    return ViverseResult<MatchmakingServiceClass>.Failure(errorResult);
                }
            }

            /// <summary>
            /// Check if the Play service is initialized
            /// </summary>
            public bool IsInitialized => _isInitialized;

            /// <summary>
            /// Reset the initialization state (used during logout/cleanup)
            /// </summary>
            internal void Reset()
            {
                _isInitialized = false;
                Debug.Log("Play service reset");
            }
        }
    }
}