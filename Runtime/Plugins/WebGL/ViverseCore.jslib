var ViverseCoreLib = {
    $deps: ['ViverseReturnCodes', 'ViverseAsyncHelper', 'ViverseCore'],

    // Core initialization
    ViverseSDK_Initialize: function(taskId, callback) {
      console.log("ViverseSDK_Initialize called with taskId:", taskId);
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(taskId, Module.ViverseCore.Initialize(), callbackWrapper);
    },

    // SSO functions
    SSO_InitializeClient: function(clientId, domain, cookieDomain) {
        try {
          return Module.ViverseCore.SSO.InitializeClient(
              UTF8ToString(clientId),
              UTF8ToString(domain),
              cookieDomain ? UTF8ToString(cookieDomain) : undefined
            );
        } catch (e) {
            console.error('SSO_InitializeClient error:', e);
            return Module.ViverseReturnCodes.ERROR_EXCEPTION;
        }
    },

    SSO_InitializeClientAsync: function(clientId, domain, cookieDomain, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("SSO_InitializeClientAsync called with clientId:", UTF8ToString(clientId));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.SSO.InitializeClientAsync(
            UTF8ToString(clientId),
            UTF8ToString(domain),
            cookieDomain ? UTF8ToString(cookieDomain) : undefined
          ),
          callbackWrapper
        );
      } catch (e) {
        console.error('SSO_InitializeClientAsync error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    SSO_LoginWithWorlds: function(state, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("SSO_LoginWithWorlds called with state:", state);

      try {
        let stateToUse = null;
        if(state !== null && state !== undefined && state !== "") {
            stateToUse = UTF8ToString(state);
            console.log('Using provided state:', stateToUse);
        }

        // Enhanced LoginWithWorlds that uses event listeners instead of polling
        const enhancedLoginWithWorlds = async () => {
          // Check if we're already in an OAuth callback
          const urlParams = new URLSearchParams(window.location.search);
          const hasCode = urlParams.has('code');
          const hasState = urlParams.has('state');

          if (hasCode && hasState) {
            console.log('OAuth callback detected in LoginWithWorlds, processing...');
            try {
              // Use SafeCheckAuth to handle the callback
              const authResult = await Module.ViverseCore.SSO.SafeCheckAuth();
              if (authResult && authResult.access_token) {
                console.log('OAuth callback processed successfully');
                return authResult;
              }
            } catch (callbackError) {
              console.error('Error processing OAuth callback:', callbackError);
            }
          }

          // Check if already authenticated
          try {
            const existingAuth = await Module.ViverseCore.SSO.SafeCheckAuth();
            if (existingAuth && existingAuth.access_token) {
              console.log('Already authenticated, returning existing token');
              return existingAuth;
            }
          } catch (authCheckError) {
            console.log('No existing authentication found, proceeding with login');
          }

          // Proceed with login flow - first try the original SDK call
          console.log('Calling original loginWithWorlds...');

          try {
            // Call the original SDK method and check its return value
            const originalResult = await Module.ViverseCore.SSO.LoginWithWorlds(stateToUse);

            // Log what the original method returned
            if (originalResult === undefined) {
              console.log('Original loginWithWorlds returned undefined (expected)');
            } else if (originalResult === null) {
              console.log('Original loginWithWorlds returned null');
            } else {
              console.log('Original loginWithWorlds returned:', JSON.stringify(originalResult, null, 2));

              // Check if the original result has auth data
              if (originalResult && originalResult.access_token) {
                console.log('Original loginWithWorlds returned auth tokens directly!');
                return originalResult;
              }
            }
          } catch (loginError) {
            // Original loginWithWorlds may throw if redirect happens, that's expected
            console.log('Original loginWithWorlds threw (may be expected for redirect):', loginError.message);
          }

          // If we reach here, the original call didn't return auth tokens
          // Use event-driven approach to wait for completion
          console.log('Setting up event listeners to wait for OAuth completion...');

          return new Promise(async (resolve, reject) => {
            const timeoutMs = 10000; // 10 seconds timeout
            let authCheckInterval = null;
            let isResolved = false;

            // Setup timeout
            const timeoutId = setTimeout(() => {
              if (!isResolved) {
                isResolved = true;
                enhancedCleanup();
                reject(new Error(`Login timeout - OAuth flow did not complete within ${timeoutMs/1000} seconds. Please try again or refresh the page.`));
              }
            }, timeoutMs);

            // Setup auth check interval (lightweight check every 500ms)
            const checkAuth = async () => {
              if (isResolved) return;

              try {
                // Check URL for OAuth callback
                const currentUrl = new URLSearchParams(window.location.search);
                if (currentUrl.has('code') && currentUrl.has('state')) {
                  console.log('OAuth callback detected');
                  const authResult = await Module.ViverseCore.SSO.SafeCheckAuth();
                  if (authResult && authResult.access_token) {
                    isResolved = true;
                    enhancedCleanup();
                    console.log('Authentication completed via OAuth callback');
                    resolve(authResult);
                    return;
                  }
                }

                // Check SDK cache for auth
                const authCheck = await Module.ViverseCore.SSO.SafeCheckAuth();
                if (authCheck && authCheck.access_token) {
                  isResolved = true;
                  enhancedCleanup();
                  console.log('Authentication completed');
                  resolve(authCheck);
                }
              } catch (error) {
                // Continue checking
              }
            };

            // Setup message listener for postMessage auth responses
            const messageHandler = async (event) => {
              if (isResolved) return;

              // Check for auth response messages
              if (event.data && (
                event.data.methods === "VIVERSE_SDK/login:ack" ||
                event.data.methods === "VIVERSE_SDK/checkAuth:ack" ||
                (event.data.auth_resp && event.data.auth_resp.access_token)
              )) {
                console.log('Received auth message event');

                // Try to get the token
                try {
                  const authResult = await Module.ViverseCore.SSO.SafeCheckAuth();
                  if (authResult && authResult.access_token) {
                    isResolved = true;
                    enhancedCleanup();
                    console.log('Authentication completed via message event');
                    resolve(authResult);
                  }
                } catch (error) {
                  console.error('Error processing auth message:', error);
                }
              }
            };

            // Setup storage event listener for cross-tab auth
            const storageHandler = async (event) => {
              if (isResolved) return;

              // Check if auth-related storage changed
              if (event.key && event.key.includes('@viverse')) {
                console.log('Auth storage event detected');
                try {
                  const authResult = await Module.ViverseCore.SSO.SafeCheckAuth();
                  if (authResult && authResult.access_token) {
                    isResolved = true;
                    enhancedCleanup();
                    console.log('Authentication completed via storage event');
                    resolve(authResult);
                  }
                } catch (error) {
                  // Continue listening
                }
              }
            };

            // Enhanced cleanup function to prevent leaks
            const cleanup = () => {
              try {
                clearTimeout(timeoutId);
                if (authCheckInterval) {
                  clearInterval(authCheckInterval);
                  authCheckInterval = null;
                }
                window.removeEventListener('message', messageHandler);
                window.removeEventListener('storage', storageHandler);
                console.log('LoginWithWorlds cleanup completed - all listeners removed');
              } catch (cleanupError) {
                console.warn('Error during LoginWithWorlds cleanup:', cleanupError);
              }
            };

            // Additional safety: cleanup on page unload to prevent leaks across navigations
            const unloadHandler = () => {
              if (!isResolved) {
                console.log('Page unloading - cleaning up LoginWithWorlds listeners');
                isResolved = true;
                cleanup();
              }
            };
            window.addEventListener('beforeunload', unloadHandler);

            // Enhanced cleanup that also removes unload handler
            const enhancedCleanup = () => {
              cleanup();
              window.removeEventListener('beforeunload', unloadHandler);
            };

            // Attach listeners
            window.addEventListener('message', messageHandler);
            window.addEventListener('storage', storageHandler);

            // Start periodic auth check (in case events are missed)
            authCheckInterval = setInterval(checkAuth, 500);

            // Do an immediate check in case auth completed during the original call
            checkAuth();
          });
        };

        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
            taskId,
            enhancedLoginWithWorlds(),
            callbackWrapper
        );
      } catch (e) {
          console.error('SSO_LoginWithWorlds error:', e);
          Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
              taskId,
              Promise.reject(e),
              callbackWrapper
          );
      }
    },

    SSO_Logout: function(redirectUrl, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.SSO.Logout(UTF8ToString(redirectUrl)),
        callbackWrapper
      );
    },

    SSO_GetAccessToken: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
            taskId,
            Module.ViverseCore.SSO.GetAccessToken(),
            callbackWrapper
        );
    },

    SSO_CheckAuth: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
            taskId,
            Module.ViverseCore.SSO.SafeCheckAuth(),
            callbackWrapper
        );
    },

    SSO_DetectAndHandleOAuthCallback: function(clientId, domain, cookieDomain, taskId, callback) {
        const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);

        try {
            const result = Module.ViverseCore.SSO.DetectAndHandleOAuthCallback(
                UTF8ToString(clientId),
                UTF8ToString(domain),
                cookieDomain ? UTF8ToString(cookieDomain) : undefined
            );

            Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
                taskId,
                Promise.resolve(result),
                callbackWrapper
            );
        } catch (e) {
            console.error('SSO_DetectAndHandleOAuthCallback error:', e);
            Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
                taskId,
                Promise.reject(e),
                callbackWrapper
            );
        }
    },

    SSO_ForceReinitializeClient: function(clientId, domain, cookieDomain, taskId, callback) {
        const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);

        try {
            // Force reinitialization by calling InitializeClient with forceReinit=true
            const result = Module.ViverseCore.SSO.InitializeClient(
                UTF8ToString(clientId),
                UTF8ToString(domain),
                cookieDomain ? UTF8ToString(cookieDomain) : undefined,
                true // forceReinit
            );

            Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
                taskId,
                Promise.resolve({
                    returnCode: result,
                    message: result === Module.ViverseReturnCodes.SUCCESS ? 'Client reinitialized successfully' : 'Client reinitialization failed',
                    payload: null
                }),
                callbackWrapper
            );
        } catch (e) {
            console.error('SSO_ForceReinitializeClient error:', e);
            Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
                taskId,
                Promise.reject(e),
                callbackWrapper
            );
        }
    },

    // Avatar functions
    ViverseSDK_SetAvatarHost: function(host) {
        Module.ViverseCore.SetAvatarHost(UTF8ToString(host));
    },

    Avatar_Initialize: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Avatar.Initialize(),
        callbackWrapper
      );
    },

    Avatar_GetProfile: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Avatar.GetProfile(),
        callbackWrapper
      );
    },

    Avatar_GetAvatarList: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Avatar.GetAvatarList(),
        callbackWrapper
      );
    },

    Avatar_GetPublicAvatarList: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Avatar.GetPublicAvatarList(),
        callbackWrapper
      );
    },

    Avatar_GetActiveAvatar: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Avatar.GetActiveAvatar(),
        callbackWrapper
      );
    },

    Avatar_GetPublicAvatarByID: function(avatarId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Avatar.GetPublicAvatarByID(UTF8ToString(avatarId)),
        callbackWrapper
      );
    },

    // Leaderboard functions
    ViverseSDK_SetLeaderboardHosts: function(baseURL, communityBaseURL) {
        Module.ViverseCore.SetLeaderboardHosts(
            UTF8ToString(baseURL),
            UTF8ToString(communityBaseURL)
        );
    },

    Leaderboard_Initialize: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
            taskId,
            Module.ViverseCore.Leaderboard.Initialize(),
            callbackWrapper
        );
    },

    Leaderboard_UploadScore: function(appId, leaderboardName, score, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
            taskId,
            Module.ViverseCore.Leaderboard.UploadScore(
                UTF8ToString(appId),
                UTF8ToString(leaderboardName),
                UTF8ToString(score)
            ),
            callbackWrapper
        );
    },

    Leaderboard_GetLeaderboard: function(appId, configJson, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
            taskId,
            Module.ViverseCore.Leaderboard.GetLeaderboard(
                UTF8ToString(appId),
                UTF8ToString(configJson)
            ),
            callbackWrapper
        );
    },

    Achievement_UploadUserAchievement: function(appId, achievementsJson, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Leaderboard.UploadUserAchievement(
          UTF8ToString(appId),
          UTF8ToString(achievementsJson)
        ),
        callbackWrapper
      );
    },

    Achievement_GetUserAchievement: function(appId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Leaderboard.GetUserAchievement(
          UTF8ToString(appId)
        ),
        callbackWrapper
      );
    },

    // Play System functions
    Play_Initialize: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Play.Initialize(),
        callbackWrapper
      );
    },

    Play_NewMatchmakingClient: function(appId, debugMode, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Play_NewMatchmakingClient called with appId:", UTF8ToString(appId), "debugMode:", debugMode);

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Play.NewMatchmakingClient(UTF8ToString(appId), debugMode),
          callbackWrapper
        );
      } catch (e) {
        console.error('Play_NewMatchmakingClient error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    // Matchmaking functions
    Matchmaking_SetActor: function(actorJson, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Matchmaking.SetActor(UTF8ToString(actorJson)),
        callbackWrapper
      );
    },

    Matchmaking_CreateRoom: function(roomJson, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Matchmaking.CreateRoom(UTF8ToString(roomJson)),
        callbackWrapper
      );
    },

    Matchmaking_JoinRoom: function(roomId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Matchmaking.JoinRoom(UTF8ToString(roomId)),
        callbackWrapper
      );
    },

    Matchmaking_LeaveRoom: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Matchmaking.LeaveRoom(),
        callbackWrapper
      );
    },

    Matchmaking_CloseRoom: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Matchmaking.CloseRoom(),
        callbackWrapper
      );
    },

    Matchmaking_GetAvailableRooms: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Matchmaking.GetAvailableRooms(),
        callbackWrapper
      );
    },

    Matchmaking_GetMyRoomActors: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Matchmaking.GetMyRoomActors(),
        callbackWrapper
      );
    },

    // Matchmaking event management
    Matchmaking_RegisterEventListener: function(eventType, listenerId) {
      try {
        // eventType is now an integer enum value from C#
        return Module.ViverseCore.Matchmaking.RegisterEventListener(
          eventType,  // Pass enum value directly
          UTF8ToString(listenerId)
        );
      } catch (e) {
        console.error('Matchmaking_RegisterEventListener error:', e);
        return Module.ViverseReturnCodes.ERROR_EXCEPTION;
      }
    },

    Matchmaking_UnregisterEventListener: function(listenerId) {
      try {
        return Module.ViverseCore.Matchmaking.UnregisterEventListener(
          UTF8ToString(listenerId)
        );
      } catch (e) {
        console.error('Matchmaking_UnregisterEventListener error:', e);
        return Module.ViverseReturnCodes.ERROR_EXCEPTION;
      }
    },

    // Multiplayer functions
    Multiplayer_Initialize: function(roomId, appId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
        taskId,
        Module.ViverseCore.Multiplayer.Initialize(UTF8ToString(roomId), UTF8ToString(appId)),
        callbackWrapper
      );
    },

    // Multiplayer communication (standardized async pattern)
    Multiplayer_SendMessage: function(messageContent, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_SendMessage called with messageContent:", UTF8ToString(messageContent));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.SendMessage(UTF8ToString(messageContent)),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_SendMessage error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    Multiplayer_UpdateMyPosition: function(positionJson, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_UpdateMyPosition called with positionJson:", UTF8ToString(positionJson));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.UpdateMyPosition(UTF8ToString(positionJson)),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_UpdateMyPosition error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    Multiplayer_UpdateEntityPosition: function(entityId, positionJson, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_UpdateEntityPosition called with entityId:", UTF8ToString(entityId), "positionJson:", UTF8ToString(positionJson));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.UpdateEntityPosition(
            UTF8ToString(entityId),
            UTF8ToString(positionJson)
          ),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_UpdateEntityPosition error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    Multiplayer_SendCompetition: function(actionName, actionMessage, actionId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_SendCompetition called with actionName:", UTF8ToString(actionName));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.SendCompetition(
            UTF8ToString(actionName),
            UTF8ToString(actionMessage),
            UTF8ToString(actionId)
          ),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_SendCompetition error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    Multiplayer_UpdateLeaderboard: function(score, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_UpdateLeaderboard called with score:", score);

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.UpdateLeaderboard(score),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_UpdateLeaderboard error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    // Multiplayer event listener management (standardized async pattern)
    Multiplayer_RegisterMessageListener: function(listenerId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_RegisterMessageListener called with listenerId:", UTF8ToString(listenerId));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.RegisterMessageListener(UTF8ToString(listenerId)),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_RegisterMessageListener error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    Multiplayer_RegisterPositionListener: function(listenerId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_RegisterPositionListener called with listenerId:", UTF8ToString(listenerId));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.RegisterPositionListener(UTF8ToString(listenerId)),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_RegisterPositionListener error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    Multiplayer_RegisterCompetitionListener: function(listenerId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_RegisterCompetitionListener called with listenerId:", UTF8ToString(listenerId));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.RegisterCompetitionListener(UTF8ToString(listenerId)),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_RegisterCompetitionListener error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    Multiplayer_RegisterLeaderboardListener: function(listenerId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_RegisterLeaderboardListener called with listenerId:", UTF8ToString(listenerId));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.RegisterLeaderboardListener(UTF8ToString(listenerId)),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_RegisterLeaderboardListener error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    Multiplayer_UnregisterListener: function(listenerId, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      console.log("Multiplayer_UnregisterListener called with listenerId:", UTF8ToString(listenerId));

      try {
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Module.ViverseCore.Multiplayer.UnregisterListener(UTF8ToString(listenerId)),
          callbackWrapper
        );
      } catch (e) {
        console.error('Multiplayer_UnregisterListener error:', e);
        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
          taskId,
          Promise.reject(e),
          callbackWrapper
        );
      }
    },

    // Event dispatcher functions using callback pattern instead of SendMessage
    ViverseEventDispatcher_RegisterMatchmakingCallback: function(callback) {
        try {
            if (!globalThis.ViverseEventCallbacks) {
                globalThis.ViverseEventCallbacks = {};
            }

            const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
            globalThis.ViverseEventCallbacks.matchmakingCallback = callbackWrapper;
            console.log('Matchmaking event callback registered');
            return Module.ViverseReturnCodes.SUCCESS;
        } catch (e) {
            console.error('Error registering matchmaking callback:', e);
            return Module.ViverseReturnCodes.ERROR_EXCEPTION;
        }
    },

    ViverseEventDispatcher_RegisterMatchmakingRawCallback: function(taskId, callback) {
        console.log("ViverseEventDispatcher_RegisterMatchmakingRawCallback called with taskId:", taskId);

        try {
            if (!globalThis.ViverseEventCallbacks) {
                globalThis.ViverseEventCallbacks = {};
            }

            // Create a DEDICATED callback wrapper for static event dispatching (completely separate from async)
            const staticEventCallbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
            globalThis.ViverseEventCallbacks.matchmakingRawCallback = staticEventCallbackWrapper;

            // Create a SEPARATE callback wrapper ONLY for the async registration response
            const asyncRegistrationCallbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);

            // Use standard async pattern to confirm registration (uses DIFFERENT callback)
            Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
                taskId,
                Promise.resolve({
                    message: "Raw SDK event callback registered successfully",
                    registered: true
                }),
                asyncRegistrationCallbackWrapper
            );

            console.log('Raw SDK event callback registered - static events will use separate callback from async responses');
        } catch (e) {
            console.error('ViverseEventDispatcher_RegisterMatchmakingRawCallback error:', e);
            const errorCallbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
            Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
                taskId,
                Promise.reject(e),
                errorCallbackWrapper
            );
        }
    },

    ViverseEventDispatcher_RegisterSDKEventCallback: function(callback) {
        try {
            if (!globalThis.ViverseEventCallbacks) {
                globalThis.ViverseEventCallbacks = {};
            }

            const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
            globalThis.ViverseEventCallbacks.sdkEventCallback = callbackWrapper;
            console.log('SDK event callback registered');
            return Module.ViverseReturnCodes.SUCCESS;
        } catch (e) {
            console.error('Error registering SDK event callback:', e);
            return Module.ViverseReturnCodes.ERROR_EXCEPTION;
        }
    },

    ViverseEventDispatcher_RegisterMultiplayerCallback: function(callback) {
        try {
            if (!globalThis.ViverseEventCallbacks) {
                globalThis.ViverseEventCallbacks = {};
            }

            const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
            globalThis.ViverseEventCallbacks.multiplayerCallback = callbackWrapper;
            console.log('Multiplayer event callback registered');
            return Module.ViverseReturnCodes.SUCCESS;
        } catch (e) {
            console.error('Error registering multiplayer callback:', e);
            return Module.ViverseReturnCodes.ERROR_EXCEPTION;
        }
    },

    ViverseEventDispatcher_UnregisterCallbacks: function() {
        try {
            if (globalThis.ViverseEventCallbacks) {
                globalThis.ViverseEventCallbacks = {};
            }
            console.log('Event callbacks unregistered');
            return Module.ViverseReturnCodes.SUCCESS;
        } catch (e) {
            console.error('Error unregistering callbacks:', e);
            return Module.ViverseReturnCodes.ERROR_EXCEPTION;
        }
    },

    // ✅ Missing functions expected by C# ViverseEventDispatcher
    ViverseEventDispatcher_RegisterMatchmakingListener: function(eventTypeName, callback) {
        try {
            if (!globalThis.ViverseEventCallbacks) {
                globalThis.ViverseEventCallbacks = {};
            }

            const eventTypeNameStr = UTF8ToString(eventTypeName);
            const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);

            // Generate unique TaskId for this listener
            const taskId = 'matchmaking_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);

            // Store callback with TaskId
            globalThis.ViverseEventCallbacks[taskId] = {
                callback: callbackWrapper,
                eventType: eventTypeNameStr,
                listenerType: 'matchmaking'
            };

            console.log('Matchmaking event listener registered:', eventTypeNameStr, 'TaskId:', taskId);

            // Return TaskId as string (C# expects string return)
            return taskId;
        } catch (e) {
            console.error('Error registering matchmaking listener:', e);
            return 0; // Return null pointer on error
        }
    },

    ViverseEventDispatcher_RegisterMultiplayerListener: function(eventTypeName, callback) {
        try {
            if (!globalThis.ViverseEventCallbacks) {
                globalThis.ViverseEventCallbacks = {};
            }

            const eventTypeNameStr = UTF8ToString(eventTypeName);
            const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);

            // Generate unique TaskId for this listener
            const taskId = 'multiplayer_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);

            // Store callback with TaskId
            globalThis.ViverseEventCallbacks[taskId] = {
                callback: callbackWrapper,
                eventType: eventTypeNameStr,
                listenerType: 'multiplayer'
            };

            console.log('Multiplayer event listener registered:', eventTypeNameStr, 'TaskId:', taskId);

            // Return TaskId as string (C# expects string return)
            return taskId;
        } catch (e) {
            console.error('Error registering multiplayer listener:', e);
            return 0; // Return null pointer on error
        }
    },

    ViverseEventDispatcher_UnregisterListener: function(taskId) {
        try {
            const taskIdStr = UTF8ToString(taskId);

            if (globalThis.ViverseEventCallbacks && globalThis.ViverseEventCallbacks[taskIdStr]) {
                delete globalThis.ViverseEventCallbacks[taskIdStr];
                console.log('Event listener unregistered:', taskIdStr);
                return Module.ViverseReturnCodes.SUCCESS;
            } else {
                console.warn('Event listener not found for TaskId:', taskIdStr);
                return Module.ViverseReturnCodes.ERROR_NOT_FOUND;
            }
        } catch (e) {
            console.error('Error unregistering listener:', e);
            return Module.ViverseReturnCodes.ERROR_EXCEPTION;
        }
    },

    // String management
    FreeString: function(ptr) {
        _free(ptr);
    },

    // SDK State access
    ViverseCore_GetSDKState: function() {
        try {
            console.log("Calling getsdkstate");
            const stateJson = Module.ViverseCore.GetSDKState();
            console.log("returned sdk state json"+stateJson);
            return stateJson;
        } catch (e) {
            console.error('Error getting SDK state:', e);
            return '{"error": "Exception getting SDK state"}';
        }
    },
    // Create a wrapper function that returns the makeDynCall result
    //TODO: see if there's a way to move this to a shared helper file, since the unity preprocessor breaks javascript parsing in some cases (like for doxygen)
    $ViverseCoreHelpers: {
      createCallbackWrapper: function(callback) {
        var dynCallFn = {{{ makeDynCall('vi', 'callback') }}};
        return function(ptr) {
          dynCallFn(ptr);
        };
      }
    }
};

// Add dependencies
autoAddDeps(ViverseCoreLib, '$deps');
autoAddDeps(ViverseCoreLib, '$ViverseCoreHelpers');
mergeInto(LibraryManager.library, ViverseCoreLib);
