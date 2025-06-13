Module['ViverseCore'] = {
    $deps: ['ViverseReturnCodes', 'ViverseAsyncHelper'],
    _isLoaded: false,
    _hasStartedLoading: false,
    _config: {
        avatarHost: 'https://sdk-api.viverse.com/',
        leaderboardHost: 'https://www.viveport.com/',
        communityHost: 'https://www.viverse.com/'
    },
    _token: null,
    get ReturnCode() {
        return Module['ViverseReturnCodes'];
    },

    // Helper function to handle SDK functions that may return either promises or direct values
    _ensurePromise: function(value) {
        if (value === null || value === undefined) {
            // Handle null/undefined returns by rejecting with specific error code
            console.warn('SDK function returned null/undefined, treating as ERROR_SDK_RETURNED_NULL');
            const error = new Error('SDK function returned null or undefined');
            error.viverseReturnCode = Module.ViverseReturnCodes.ERROR_SDK_RETURNED_NULL;
            return Promise.reject(error);
        }

        if (value && typeof value.then === 'function') {
            // It's already a promise
            return value;
        } else {
            // It's a direct value, wrap it in a resolved promise
            return Promise.resolve(value);
        }
    },

    // Core initialization
    Initialize: async function() {
        if (this._isLoaded) return this.ReturnCode.SUCCESS;
        if (!this._hasStartedLoading) {
            this._hasStartedLoading = true;
            try {
                await this._loadSDK();
                this._isLoaded = true;
                console.log('loaded Viverse SDK:');
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('Failed to load Viverse SDK:', e);
                return this.ReturnCode.ERROR_SDK_NOT_LOADED;
            }
        }
        console.error('module not loaded for Viverse SDK:');
        return this.ReturnCode.ERROR_MODULE_NOT_LOADED;
    },
    _loadSDK: function() {
        return new Promise((resolve, reject) => {
            if (typeof globalThis.viverse !== 'undefined') {
                resolve(true);
                return;
            }

            const script = document.createElement('script');
            script.src = 'https://www.viverse.com/static-assets/viverse-sdk/1.2.9/viverse-sdk.umd.js';
            script.onload = () => resolve(true);
            script.onerror = () => reject(new Error('Failed to load Viverse SDK'));
            document.head.appendChild(script);
        });
    },

    // SSO Functions
    SSO: {
        get ReturnCode() {
            return Module['ViverseReturnCodes'];
        },
        InitializeClient: function(clientId, domain, cookieDomain, forceReinit = false) {
            if (!globalThis.viverse) return this.ReturnCode.ERROR_SDK_NOT_LOADED;
            console.log('InitializeClient:', clientId, domain, cookieDomain, 'forceReinit:', forceReinit);

            // Allow reinitialization if forced (for OAuth callbacks)
            if (forceReinit || !globalThis._viverseInitPromise) {
                if (forceReinit) {
                    console.log('Force reinitializing client...');
                    this._cleanupClientInstances();
                }

                globalThis._viverseInitPromise = this._createClientWhenReady(clientId, domain, cookieDomain);
                globalThis._viverseClientReady = globalThis._viverseInitPromise;
            }

            return this.ReturnCode.SUCCESS;
        },

        InitializeClientAsync: async function(clientId, domain, cookieDomain, forceReinit = false) {
            if (!globalThis.viverse) {
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_SDK_NOT_LOADED,
                    message: 'Viverse SDK not loaded',
                    payload: null
                });
            }

            console.log('InitializeClientAsync:', clientId, domain, cookieDomain, 'forceReinit:', forceReinit);

            try {
                // Allow reinitialization if forced (for OAuth callbacks)
                if (forceReinit || !globalThis._viverseInitPromise) {
                    if (forceReinit) {
                        console.log('Force reinitializing client...');
                        this._cleanupClientInstances();
                    }

                    globalThis._viverseInitPromise = this._createClientWhenReady(clientId, domain, cookieDomain);
                    globalThis._viverseClientReady = globalThis._viverseInitPromise;
                }

                // Wait for the initialization to complete
                await globalThis._viverseClientReady;

                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Client initialized successfully',
                    payload: JSON.stringify({ clientId: clientId, domain: domain })
                });
            } catch (e) {
                console.error('InitializeClientAsync Error:', e);

                // Use specific error codes based on error type
                let errorCode = this.ReturnCode.ERROR_EXCEPTION;
                if (e.viverseReturnCode) {
                    errorCode = e.viverseReturnCode;
                } else if (e.message && e.message.includes('null or undefined')) {
                    errorCode = this.ReturnCode.ERROR_SDK_RETURNED_NULL;
                }

                return Promise.resolve({
                    returnCode: errorCode,
                    message: 'Client initialization failed: ' + e.message,
                    payload: null
                });
            }
        },

        _createClientWhenReady: function(clientId, domain, cookieDomain) {
            const doInit = () => {
                try {
                    if (!globalThis.viverseClient) {
                        console.log('Creating viverse client instance...');
                        globalThis.viverseClient = new globalThis.viverse.client({
                            clientId: clientId,
                            domain: domain,
                            cookieDomain: cookieDomain || ''
                        });
                        console.log('Viverse client created successfully');
                    }
                    return this.ReturnCode.SUCCESS;
                } catch (e) {
                    console.error('SSO Init Error:', e);
                    return this.ReturnCode.ERROR_EXCEPTION;
                }
            };

            // If page is already loaded, initialize immediately
            if (document.readyState === 'complete') {
                console.log('Page already loaded, initializing client immediately');
                return Promise.resolve(doInit.call(this));
            }

            // Otherwise, wait for page load with proper cleanup tracking
            console.log('Waiting for page load to initialize client...');
            return new Promise((resolve) => {
                const loadHandler = () => {
                    console.log('Page loaded, initializing client now');
                    resolve(doInit.call(this));
                };

                // Use { once: true } to automatically remove the listener
                window.addEventListener('load', loadHandler, { once: true });

                // Store handler reference for potential cleanup (though { once: true } should handle it)
                if (!globalThis._viverseLoadHandlers) {
                    globalThis._viverseLoadHandlers = new Set();
                }
                globalThis._viverseLoadHandlers.add(loadHandler);
            });
        },

        LoginWithWorlds: async function(stateParam) {
            // Wait for client initialization if needed
            if (globalThis._viverseClientReady) {
                await globalThis._viverseClientReady;
            }

            if (!globalThis.viverseClient) {
                console.log("Could not find viverse client");
                return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
            }

            try {
                // Build config object if state provided
                const config = stateParam ? { state: stateParam } : undefined;

                // v1.2.9 uses loginWithWorlds
                const loginResult = globalThis.viverseClient.loginWithWorlds(config);
                await Module.ViverseCore._ensurePromise(loginResult);

                return Promise.resolve(this.ReturnCode.SUCCESS);
            } catch (e) {
                console.error('LoginWithWorlds Error:', e);

                // Use specific error codes based on error type
                if (e.viverseReturnCode) {
                    return Promise.resolve(e.viverseReturnCode);
                } else if (e.message && e.message.includes('null or undefined')) {
                    return Promise.resolve(this.ReturnCode.ERROR_SDK_RETURNED_NULL);
                }

                return Promise.resolve(this.ReturnCode.ERROR_EXCEPTION);
            }
        },


        Logout: async function(redirectUrl) {
          // Wait for client initialization if needed
          if (globalThis._viverseClientReady) {
              await globalThis._viverseClientReady;
          }

          if (!globalThis.viverseClient) {
            console.log("Could not find viverse client");
            return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
          }

          try {
            console.log("Logging out with redirect url:", redirectUrl);
            const logoutResult = globalThis.viverseClient.logout({ redirectionUrl: redirectUrl });
            await Module.ViverseCore._ensurePromise(logoutResult);
            console.log("Logging out with redirect url finished:", redirectUrl);

            // Comprehensive cleanup using shared method
            this._cleanupClientInstances();
            Module.ViverseCore._isLoaded = false;

            //temporarily allow these cookies, assume user has accepted eula
/*
            // Clear any stored cookies/storage
            function deleteCookie(name) {
              try {
                document.cookie = name + "=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/; domain=" + location.hostname + ";";
              }catch (e) {
                console.log(`Error clearing cookie ${name} with domain:`, e);
              }
              try {
                document.cookie = name + "=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";
              }catch (e){
                console.log(`Error clearing cookie ${name} without domain:`, e);
              }
            }

            deleteCookie("_htc_access_token_production");
            deleteCookie("_htc_access_token_stage");
            deleteCookie("_htc_access_token_dev");
            */
            return Promise.resolve(this.ReturnCode.SUCCESS);
          } catch (e) {
            console.error('Logout Error:', e);

            // Use specific error codes based on error type
            if (e.viverseReturnCode) {
                return Promise.resolve(e.viverseReturnCode);
            } else if (e.message && e.message.includes('null or undefined')) {
                return Promise.resolve(this.ReturnCode.ERROR_SDK_RETURNED_NULL);
            }

            return Promise.resolve(this.ReturnCode.ERROR_EXCEPTION);
          }
        },


        GetAccessToken: async function() {
            // Wait for client initialization if needed
            if (globalThis._viverseClientReady) {
                await globalThis._viverseClientReady;
            }

            if (!globalThis.viverseClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const tokenCall = globalThis.viverseClient.getToken({detailedResponse: true});
                const tokenResponse = await Module.ViverseCore._ensurePromise(tokenCall);
                if (!tokenResponse) return Promise.resolve(this.ReturnCode.ERROR_NOT_FOUND);
                //FIXME: debug logging
                console.log('tokenResponse:', JSON.stringify(tokenResponse));
                if(tokenResponse.access_token === undefined) {
                    console.log("Got an token response but no access token");
                    return Promise.resolve(this.ReturnCode.ERROR_NOT_FOUND);
                }

                // If token exists, update the core token and return it
                Module.ViverseCore._token = tokenResponse;
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Operation completed successfully',
                    payload: JSON.stringify(tokenResponse)
                });
            } catch (e) {
                console.error('Get Token Error:', e);

                // Use specific error codes based on error type
                let errorCode = this.ReturnCode.ERROR_EXCEPTION;
                let errorMessage = 'Failed to get access token';

                if (e.viverseReturnCode) {
                    errorCode = e.viverseReturnCode;
                    errorMessage = 'Access token request failed - ' + e.message;
                } else if (e.message && e.message.includes('null or undefined')) {
                    errorCode = this.ReturnCode.ERROR_SDK_RETURNED_NULL;
                    errorMessage = 'Access token request failed - SDK returned null/undefined (possible authentication issue)';
                }

                return Promise.resolve({
                    returnCode: errorCode,
                    message: errorMessage,
                    payload: JSON.stringify({ error: e.message })
                });
            }
        },

        SafeCheckAuth: async function() {
            // Wait for client initialization if needed
            if (globalThis._viverseClientReady) {
                await globalThis._viverseClientReady;
            }

            if (!globalThis.viverseClient) {
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_MODULE_NOT_LOADED,
                    message: 'Viverse client not loaded',
                    payload: null
                });
            }

            try {
                let checkAuthCall = globalThis.viverseClient.checkAuth();

                // Handle undefined return (v1.2.9 behavior)
                if (!checkAuthCall || typeof checkAuthCall.then !== 'function') {
                    console.warn('checkAuth() returned non-Promise value:', checkAuthCall);
                    checkAuthCall = Promise.resolve(checkAuthCall);
                }

                // ADD TIMEOUT PROTECTION (like JavaScript examples)
                const timeoutPromise = new Promise((_, reject) => {
                    const timeoutError = new Error('checkAuth() timed out after 10 seconds');
                    timeoutError.viverseReturnCode = Module.ViverseReturnCodes.ERROR_AUTHENTICATION_TIMEOUT;
                    setTimeout(() => reject(timeoutError), 10000);
                });

                const result = await Promise.race([checkAuthCall, timeoutPromise]);

                if (result && result.access_token) {
                    Module.ViverseCore._token = result;

                    // ✅ Auto-initialize dependent services when token becomes available
                    await Module.ViverseCore._autoInitializeDependentServices();

                    return Promise.resolve({
                        returnCode: this.ReturnCode.SUCCESS,
                        message: 'Authentication valid',
                        payload: JSON.stringify(result)
                    });
                } else {
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_UNAUTHORIZED,
                        message: 'Not authenticated',
                        payload: null
                    });
                }
            } catch (e) {
                console.error('CheckAuth Error:', e);

                // Use specific error codes based on error type
                let errorCode = this.ReturnCode.ERROR_EXCEPTION;
                let errorMessage = 'CheckAuth failed';

                if (e.viverseReturnCode) {
                    errorCode = e.viverseReturnCode;
                    errorMessage = 'CheckAuth failed - ' + e.message;
                } else if (e.message && e.message.includes('timed out')) {
                    errorCode = this.ReturnCode.ERROR_AUTHENTICATION_TIMEOUT;
                    errorMessage = 'Authentication check timed out - this usually indicates network issues or authentication state corruption';
                } else if (e.message && e.message.includes('null or undefined')) {
                    errorCode = this.ReturnCode.ERROR_SDK_RETURNED_NULL;
                    errorMessage = 'CheckAuth failed - SDK returned null/undefined';
                }

                return Promise.resolve({
                    returnCode: errorCode,
                    message: errorMessage,
                    payload: JSON.stringify({ error: e.message })
                });
            }
        },

        // Comprehensive cleanup to prevent memory leaks
        _cleanupClientInstances: function() {
            console.log('Cleaning up client instances to prevent memory leaks...');

            // Clear matchmaking event listeners
            if (Module.ViverseCore.Matchmaking && Module.ViverseCore.Matchmaking._eventListeners) {
                console.log('Cleaning up matchmaking event listeners...');
                Module.ViverseCore.Matchmaking._eventListeners.forEach((listener, listenerId) => {
                    if (globalThis.matchmakingClient) {
                        const jsEventName = Module.ViverseCore.MatchmakingEventType.toJSEventName(listener.eventType);
                        if (jsEventName !== 'unknown') {
                            globalThis.matchmakingClient.off(jsEventName, listener.callback);
                        }
                    }
                });
                Module.ViverseCore.Matchmaking._eventListeners.clear();
            }

            // Clear multiplayer event listeners
            if (Module.ViverseCore.Multiplayer && Module.ViverseCore.Multiplayer._eventListeners) {
                console.log('Cleaning up multiplayer event listeners...');
                Module.ViverseCore.Multiplayer._eventListeners.clear();
                // Note: Multiplayer SDK doesn't provide off() methods, so we just clear our tracking
            }

            // Clear Play system references - now using globalThis pattern
            // Note: Play and Matchmaking clients are now stored in globalThis, not local variables

            // Multiplayer client now stored in globalThis
            globalThis.multiplayerClient = null;

            // Clear all client instances
            globalThis.viverseClient = null;
            globalThis.avatarClient = null;
            globalThis.gameDashboardClient = null;
            globalThis.matchmakingClient = null;
            globalThis.playClient = null;

            // Clear initialization promises
            globalThis._viverseInitPromise = null;
            globalThis._viverseClientReady = null;

            // Clear cached tokens
            Module.ViverseCore._token = null;

            // Clear any pending DOM event listeners (defensive cleanup)
            if (globalThis._viverseLoadHandlers) {
                globalThis._viverseLoadHandlers.forEach(handler => {
                    try {
                        window.removeEventListener('load', handler);
                    } catch (e) {
                        // Ignore errors - handler may already be removed
                    }
                });
                globalThis._viverseLoadHandlers.clear();
            }

            console.log('Client cleanup completed');
        },

        DetectAndHandleOAuthCallback: function(clientId, domain, cookieDomain) {
            try {
                const urlParams = new URLSearchParams(window.location.search);
                const hasCode = urlParams.has('code');
                const hasState = urlParams.has('state');

                if (hasCode && hasState) {
                    console.log('OAuth callback detected, reinitializing client...');

                    // Cleanup existing instances first
                    this._cleanupClientInstances();

                    // Create new client instance
                    globalThis.viverseClient = new globalThis.viverse.client({
                        clientId: clientId,
                        domain: domain,
                        cookieDomain: cookieDomain || ''
                    });

                    console.log('Client reinitialized after OAuth callback');

                    return {
                        returnCode: this.ReturnCode.SUCCESS,
                        message: 'OAuth callback handled, client reinitialized',
                        payload: JSON.stringify({
                            detected: true,
                            code: urlParams.get('code'),
                            state: urlParams.get('state')
                        })
                    };
                }

                return {
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'No OAuth callback detected',
                    payload: JSON.stringify({ detected: false })
                };
            } catch (e) {
                console.error('OAuth callback detection error:', e);

                // Use specific error codes based on error type
                let errorCode = this.ReturnCode.ERROR_OAUTH_CALLBACK_FAILED;
                if (e.viverseReturnCode) {
                    errorCode = e.viverseReturnCode;
                } else if (e.message && e.message.includes('null or undefined')) {
                    errorCode = this.ReturnCode.ERROR_SDK_RETURNED_NULL;
                }

                return {
                    returnCode: errorCode,
                    message: 'OAuth callback detection failed: ' + e.message,
                    payload: JSON.stringify({ error: e.message })
                };
            }
        }
    },

    // Avatar Functions
    Avatar: {
        get ReturnCode() {
            return Module['ViverseReturnCodes'];
        },
        Initialize: async function() {
            //FIXME: separate status and error code for no token set. maybe just pass it in as a parameter
            if (!globalThis.viverse || !Module.ViverseCore._token) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                globalThis.avatarClient = new globalThis.viverse.avatar({
                    baseURL: Module.ViverseCore._config.avatarHost,
                    token: Module.ViverseCore._token.access_token
                });
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Avatar service initialized successfully',
                    payload: null
                });
            } catch (e) {
                console.error('Avatar Init Error:', e);

                // Use specific error codes based on error type
                let errorCode = this.ReturnCode.ERROR_EXCEPTION;
                let errorMessage = 'Failed to initialize avatar service';

                if (e.viverseReturnCode) {
                    errorCode = e.viverseReturnCode;
                    errorMessage = 'Avatar initialization failed - ' + e.message;
                } else if (e.message && e.message.includes('null or undefined')) {
                    errorCode = this.ReturnCode.ERROR_SDK_RETURNED_NULL;
                    errorMessage = 'Avatar initialization failed - SDK returned null/undefined';
                }

                return Promise.resolve({
                    returnCode: errorCode,
                    message: errorMessage,
                    payload: JSON.stringify({ error: e.message })
                });
            }
        },

        GetProfile: async function() {
            if (!globalThis.avatarClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const profileCall = globalThis.avatarClient.getProfile();
                const profile = await Module.ViverseCore._ensurePromise(profileCall);
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Operation completed successfully',
                    payload:  JSON.stringify(profile)
                });
            } catch (e) {
                console.error('Get Profile Error:', e);

                // Use specific error codes based on error type
                if (e.viverseReturnCode) {
                    return Promise.resolve({
                        returnCode: e.viverseReturnCode,
                        message: 'Get profile failed - ' + e.message,
                        payload: JSON.stringify({ error: e.message })
                    });
                } else if (e.message && e.message.includes('null or undefined')) {
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_SDK_RETURNED_NULL,
                        message: 'Get profile failed - SDK returned null/undefined',
                        payload: JSON.stringify({ error: e.message })
                    });
                }

                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Get profile failed - ' + e.message,
                    payload: JSON.stringify({ error: e.message })
                });
            }
        },

        GetAvatarList: async function() {
            if (!globalThis.avatarClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const avatarsCall = globalThis.avatarClient.getAvatarList();
                const avatars = await Module.ViverseCore._ensurePromise(avatarsCall);
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Operation completed successfully',
                    payload: JSON.stringify(avatars)
                });
            } catch (e) {
                console.error('Get Avatars Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Exception thrown getting avatars',
                    payload: e.toString()
                });
            }
        },
        GetPublicAvatarList: async function() {
          if (!globalThis.avatarClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

          try {
            const publicAvatarsCall = globalThis.avatarClient.getPublicAvatarList();
            const publicAvatars = await Module.ViverseCore._ensurePromise(publicAvatarsCall);
            return Promise.resolve({
              returnCode: this.ReturnCode.SUCCESS,
              message: 'Operation completed successfully',
              payload: JSON.stringify(publicAvatars)
            });
          } catch (e) {
            console.error('Get Public Avatars Error:', e);
            return Promise.resolve({
              returnCode: this.ReturnCode.ERROR_EXCEPTION,
              message: 'Exception thrown getting public avatars',
              payload: e.toString()
            });
          }
        },

        GetActiveAvatar: async function() {
          if (!globalThis.avatarClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

          try {
            const activeAvatarCall = globalThis.avatarClient.getActiveAvatar();
            const activeAvatar = await Module.ViverseCore._ensurePromise(activeAvatarCall);
            return Promise.resolve({
              returnCode: this.ReturnCode.SUCCESS,
              message: 'Operation completed successfully',
              payload: JSON.stringify(activeAvatar)
            });
          } catch (e) {
            console.error('Get Active Avatar Error:', e);
            return Promise.resolve({
              returnCode: this.ReturnCode.ERROR_EXCEPTION,
              message: 'Exception thrown getting active avatar',
              payload: e.toString()
            });
          }
        },

        GetPublicAvatarByID: async function(avatarId) {
          if (!globalThis.avatarClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

          if (!avatarId) {
            return Promise.resolve({
              returnCode: this.ReturnCode.ERROR_INVALID_PARAMETER,
              message: 'Avatar ID cannot be null or empty',
              payload: null
            });
          }

          try {
            const avatarCall = globalThis.avatarClient.getPublicAvatarByID(avatarId);
            const avatar = await Module.ViverseCore._ensurePromise(avatarCall);
            return Promise.resolve({
              returnCode: this.ReturnCode.SUCCESS,
              message: 'Operation completed successfully',
              payload: JSON.stringify(avatar)
            });
          } catch (e) {
            console.error('Get Public Avatar By ID Error:', e);
            return Promise.resolve({
              returnCode: this.ReturnCode.ERROR_EXCEPTION,
              message: 'Exception thrown getting public avatar by ID',
              payload: e.toString()
            });
          }
        }
    },

    // Leaderboard Functions
    Leaderboard: {
        get ReturnCode() {
            return Module['ViverseReturnCodes'];
        },
        Initialize: async function() {
            if (!globalThis.viverse || !Module.ViverseCore._token) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                globalThis.gameDashboardClient = new globalThis.viverse.gameDashboard({
                    baseURL: Module.ViverseCore._config.leaderboardHost,
                    communityBaseURL: Module.ViverseCore._config.communityHost,
                    token: Module.ViverseCore._token.access_token  // v1.2.9 expects string, not object
                });
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Leaderboard service initialized successfully',
                    payload: null
                });
            } catch (e) {
                console.error('Leaderboard Init Error:', e);

                // Use specific error codes based on error type
                let errorCode = this.ReturnCode.ERROR_EXCEPTION;
                let errorMessage = 'Failed to initialize leaderboard service';

                if (e.viverseReturnCode) {
                    errorCode = e.viverseReturnCode;
                    errorMessage = 'Leaderboard initialization failed - ' + e.message;
                } else if (e.message && e.message.includes('null or undefined')) {
                    errorCode = this.ReturnCode.ERROR_SDK_RETURNED_NULL;
                    errorMessage = 'Leaderboard initialization failed - SDK returned null/undefined';
                }

                return Promise.resolve({
                    returnCode: errorCode,
                    message: errorMessage,
                    payload: JSON.stringify({ error: e.message })
                });
            }
        },

        UploadScore: async function(appId, leaderboardName, score) {
            if (!globalThis.gameDashboardClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const uploadCall = globalThis.gameDashboardClient.uploadLeaderboardScore(
                    appId,
                    [{ name: leaderboardName, value: score }]
                );
                const result = await Module.ViverseCore._ensurePromise(uploadCall);
                const jsonStr = JSON.stringify(result);
                //TODO: data validation on return result
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Operation completed successfully',
                    payload: jsonStr
                });
            } catch (e) {
                console.error('Upload Score Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Exception thrown',
                    payload: e.toString()
                });
            }
        },
        GetLeaderboard: async function(appID, configJson) {
            if (!globalThis.gameDashboardClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
            console.log('GetLeaderboard:', appID, configJson);

            try {
                // Parse the config JSON from C#
                const config = JSON.parse(configJson);

                // Validate required fields
                if (!config.name || typeof config.range_start !== 'number' || typeof config.range_end !== 'number') {
                    console.error('Invalid config:', config);
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_INVALID_PARAMETER,
                        message: 'Invalid leaderboard configuration',
                        payload: null
                    });
                }

                // Call the game dashboard client with the config
                const leaderboardCall = globalThis.gameDashboardClient.getLeaderboard(
                    appID,
                    {
                        name: config.name,
                        range_start: config.range_start,
                        range_end: config.range_end,
                        region: config.region || 'global',
                        time_range: config.time_range || 'alltime',
                        around_user: config.around_user || false
                    }
                );
                const result = await Module.ViverseCore._ensurePromise(leaderboardCall);

                if (!result) {
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_NOT_FOUND,
                        message: 'No leaderboard data found',
                        payload: null
                    });
                }

                const jsonStr = JSON.stringify(result);
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Operation completed successfully',
                    payload: jsonStr
                });
            } catch (e) {
                console.error('Get Leaderboard Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Exception thrown: ' + e.message,
                    payload: null
                });
            }
        },

        UploadUserAchievement: async function(appId, achievementsJson) {
          if (!globalThis.gameDashboardClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

          try {
            // Parse the achievements JSON
            const achievements = JSON.parse(achievementsJson).achievements;
            if (!achievements || !Array.isArray(achievements)) {
              return Promise.resolve({
                returnCode: this.ReturnCode.ERROR_INVALID_PARAMETER,
                message: 'Invalid achievements data',
                payload: null
              });
            }

            // Call the game dashboard client
            const uploadCall = globalThis.gameDashboardClient.uploadUserAchievement(
              appId,
              achievements
            );
            const result = await Module.ViverseCore._ensurePromise(uploadCall);

            // Return the result
            return Promise.resolve({
              returnCode: this.ReturnCode.SUCCESS,
              message: 'Operation completed successfully',
              payload: JSON.stringify(result)
            });
          } catch (e) {
            console.error('Upload User Achievement Error:', e);
            return Promise.resolve({
              returnCode: this.ReturnCode.ERROR_EXCEPTION,
              message: 'Exception thrown: ' + e.message,
              payload: null
            });
          }
        },

        GetUserAchievement: async function(appId) {
          if (!globalThis.gameDashboardClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

          try {
            // Call the game dashboard client
            const achievementCall = globalThis.gameDashboardClient.getUserAchievement(appId);
            const result = await Module.ViverseCore._ensurePromise(achievementCall);
            if (!result) {
              return Promise.resolve({
                returnCode: this.ReturnCode.ERROR_NOT_FOUND,
                message: 'No achievement data found',
                payload: null
              });
            }

            // Return the result
            return Promise.resolve({
              returnCode: this.ReturnCode.SUCCESS,
              message: 'Operation completed successfully',
              payload: JSON.stringify(result)
            });
          } catch (e) {
            console.error('Get User Achievement Error:', e);
            return Promise.resolve({
              returnCode: this.ReturnCode.ERROR_EXCEPTION,
              message: 'Exception thrown: ' + e.message,
              payload: null
            });
          }
        }
      },

    // ============================================
    // MULTIPLAYER EVENT TYPE ENUMS (v1.2.9)
    // ============================================

    // Matchmaking event types (must match C# MatchmakingEventType enum)
    MatchmakingEventType: {
        UNSET_VALUE: 0,
        RoomJoined: 1,
        RoomLeft: 2,
        ActorJoined: 3,
        ActorLeft: 4,
        RoomClosed: 5,

        // Helper functions
        toString: function(eventType) {
            switch(eventType) {
                case this.UNSET_VALUE: return 'unset';
                case this.RoomJoined: return 'room_joined';
                case this.RoomLeft: return 'room_left';
                case this.ActorJoined: return 'actor_joined';
                case this.ActorLeft: return 'actor_left';
                case this.RoomClosed: return 'room_closed';
                default: return 'unknown';
            }
        },

        fromString: function(eventTypeString) {
            switch(eventTypeString.toLowerCase()) {
                case 'unset': return this.UNSET_VALUE;
                case 'room_joined': return this.RoomJoined;
                case 'room_left': return this.RoomLeft;
                case 'actor_joined': return this.ActorJoined;
                case 'actor_left': return this.ActorLeft;
                case 'room_closed': return this.RoomClosed;
                default: return -1;
            }
        },

        toJSEventName: function(eventType) {
            switch(eventType) {
                case this.UNSET_VALUE: return 'unknown';
                case this.RoomJoined: return 'roomJoined';
                case this.RoomLeft: return 'roomLeft';
                case this.ActorJoined: return 'actorJoined';
                case this.ActorLeft: return 'actorLeft';
                case this.RoomClosed: return 'roomClosed';
                default: return 'unknown';
            }
        },

        isValidEventType: function(eventType) {
            return eventType !== this.UNSET_VALUE && eventType >= this.RoomJoined && eventType <= this.RoomClosed;
        }
    },

    // Multiplayer event types (must match C# MultiplayerEventType enum)
    MultiplayerEventType: {
        UNSET_VALUE: 0,
        Message: 1,
        Position: 2,
        Competition: 3,
        Leaderboard: 4,

        // Helper functions
        toString: function(eventType) {
            switch(eventType) {
                case this.UNSET_VALUE: return 'unset';
                case this.Message: return 'message';
                case this.Position: return 'position';
                case this.Competition: return 'competition';
                case this.Leaderboard: return 'leaderboard';
                default: return 'unknown';
            }
        },

        fromString: function(eventTypeString) {
            switch(eventTypeString.toLowerCase()) {
                case 'unset': return this.UNSET_VALUE;
                case 'message': return this.Message;
                case 'position': return this.Position;
                case 'competition': return this.Competition;
                case 'leaderboard': return this.Leaderboard;
                default: return -1;
            }
        },

        isValidEventType: function(eventType) {
            return eventType !== this.UNSET_VALUE && eventType >= this.Message && eventType <= this.Leaderboard;
        }
    },

    // SDK event types (must match C# SDKEventType enum)
    SDKEventType: {
        UNSET_VALUE: 0,
        OnConnect: 1,
        OnJoinedLobby: 2,
        OnJoinRoom: 3,
        OnRoomListUpdate: 4,
        OnRoomActorChange: 5,
        OnRoomClosed: 6,
        OnError: 7,
        StateChange: 8,

        // Helper functions
        toString: function(eventType) {
            switch(eventType) {
                case this.UNSET_VALUE: return 'unset';
                case this.OnConnect: return 'onConnect';
                case this.OnJoinedLobby: return 'onJoinedLobby';
                case this.OnJoinRoom: return 'onJoinRoom';
                case this.OnRoomListUpdate: return 'onRoomListUpdate';
                case this.OnRoomActorChange: return 'onRoomActorChange';
                case this.OnRoomClosed: return 'onRoomClosed';
                case this.OnError: return 'onError';
                case this.StateChange: return 'stateChange';
                default: return 'unknown';
            }
        },

        fromString: function(eventTypeString) {
            switch(eventTypeString.toLowerCase()) {
                case 'unset': return this.UNSET_VALUE;
                case 'onconnect': return this.OnConnect;
                case 'onjoinedlobby': return this.OnJoinedLobby;
                case 'onjoinroom': return this.OnJoinRoom;
                case 'onroomlistupdate': return this.OnRoomListUpdate;
                case 'onroomactorchange': return this.OnRoomActorChange;
                case 'onroomclosed': return this.OnRoomClosed;
                case 'onerror': return this.OnError;
                case 'statechange': return this.StateChange;
                default: return -1;
            }
        },

        isValidEventType: function(eventType) {
            return eventType !== this.UNSET_VALUE && eventType >= this.OnConnect && eventType <= this.StateChange;
        }
    },

    // Global SDK state management (like client_script_example_modified)
    _sdkState: {
        connected: false,
        joinedLobby: false,
        actorSet: false,
        actorInfo: null, // Store current actor information
        currentRoomId: null,
        currentState: 'unknown',
        pendingOperations: new Map()
    },

    // Helper function to convert Unity's properties array format to JavaScript object format
    _convertUnityPropertiesToJSFormat: function(data, objectName = 'data') {
        if (data.properties && Array.isArray(data.properties)) {
            console.log(`Converting Unity ${objectName} properties array to JavaScript object format...`);
            const convertedProperties = {};
            data.properties.forEach(prop => {
                if (prop.key) {
                    // Try to parse numbers, keep strings as strings
                    const numValue = parseFloat(prop.value);
                    convertedProperties[prop.key] = !isNaN(numValue) && isFinite(numValue) ? numValue : prop.value;
                }
            });
            data.properties = convertedProperties;
            console.log(`Converted ${objectName} properties:`, data.properties);
        }
    },

    // Helper functions for event-driven operations
    _createPendingOperation: function(operationType, timeoutMs = 10000) {
        const operationId = `${operationType}_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

        return new Promise((resolve, reject) => {
            const timeoutId = setTimeout(() => {
                this._sdkState.pendingOperations.delete(operationId);
                reject(new Error(`Operation ${operationType} timed out after ${timeoutMs}ms`));
            }, timeoutMs);

            this._sdkState.pendingOperations.set(operationId, {
                operationType,
                resolve,
                reject,
                timeoutId,
                createdAt: Date.now()
            });
        });
    },

    _resolvePendingOperation: function(operationType, result) {
        // Find and resolve the most recent pending operation of this type
        for (let [operationId, operation] of this._sdkState.pendingOperations.entries()) {
            if (operation.operationType === operationType) {
                clearTimeout(operation.timeoutId);
                this._sdkState.pendingOperations.delete(operationId);

                if (result.success) {
                    operation.resolve(result);
                } else {
                    operation.reject(new Error(result.error || 'Operation failed'));
                }
                return true;
            }
        }
        return false;
    },

    _rejectAllPendingOperations: function(reason = 'SDK state changed') {
        for (let [operationId, operation] of this._sdkState.pendingOperations.entries()) {
            clearTimeout(operation.timeoutId);
            operation.reject(new Error(reason));
        }
        this._sdkState.pendingOperations.clear();
    },

    // Legacy function - kept for debugging/monitoring purposes
    // Actor validation - checks if actor is properly set with required information
    _validateActorState: function(operationName) {
        if (!this._sdkState.connected) {
            return {
                valid: false,
                error: `${operationName} requires connection to matchmaking server`,
                code: Module.ViverseReturnCodes.ERROR_MODULE_NOT_LOADED
            };
        }

        if (!this._sdkState.actorSet || !this._sdkState.actorInfo) {
            return {
                valid: false,
                error: `${operationName} requires actor to be set first. Call SetActor() before room operations.`,
                code: Module.ViverseReturnCodes.ERROR_INVALID_STATE
            };
        }

        const actor = this._sdkState.actorInfo;
        if (!actor.session_id || !actor.name) {
            return {
                valid: false,
                error: `${operationName} requires valid actor with session_id and name`,
                code: Module.ViverseReturnCodes.ERROR_INVALID_PARAMETER
            };
        }

        return { valid: true };
    },

    // NOTE: Room operations now work after connection only, lobby join is optional
    _isReadyForRoomOperations: function() {
        return this._sdkState.connected && this._sdkState.actorSet;
    },

    // NEW: Function for operations that only need connection (like GetAvailableRooms)
    _isReadyForRoomListing: function() {
        return this._sdkState.connected; // Only requires connection, not lobby join
    },

    // Clear actor state (e.g., on disconnect or error)
    _clearActorState: function() {
        console.log('Clearing actor state due to disconnection or error');
        this._sdkState.actorSet = false;
        this._sdkState.actorInfo = null;
    },

    // Play System Functions - using globalThis pattern like client_script_example_modified
    Play: {
        get ReturnCode() {
            return Module['ViverseReturnCodes'];
        },

        Initialize: async function() {
            if (!globalThis.viverse) {
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_SDK_NOT_LOADED,
                    message: 'Viverse SDK not loaded',
                    payload: null
                });
            }

            try {
                console.log('Creating play client...');
                globalThis.playClient = new globalThis.viverse.play();

                // Wait for the underlying playSDK to be ready (critical for multiplayer functionality)
                console.log('Waiting for playSDK to be ready...');
                let attempts = 0;
                const maxAttempts = 50; // 5 seconds maximum wait

                while (!globalThis.playClient.playSDK && attempts < maxAttempts) {
                    await new Promise(resolve => setTimeout(resolve, 100));
                    attempts++;
                }

                if (!globalThis.playClient.playSDK) {
                    console.error('Play client playSDK not ready after timeout');
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_TIMEOUT,
                        message: 'Play client playSDK not ready after 5 second timeout',
                        payload: null
                    });
                }

                console.log('Play client initialized with playSDK ready');
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Play client initialized successfully with playSDK ready',
                    payload: null
                });
            } catch (e) {
                console.error('Play Init Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Exception during play initialization: ' + e.message,
                    payload: null
                });
            }
        },

        NewMatchmakingClient: async function(appId, debugMode) {
            if (!globalThis.playClient) {
                console.error('Play client not initialized');
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_MODULE_NOT_LOADED,
                    message: 'Play client not initialized',
                    payload: null
                });
            }

            // Verify playSDK is ready before creating matchmaking client
            if (!globalThis.playClient.playSDK) {
                console.error('Play client playSDK not ready');
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_MODULE_NOT_LOADED,
                    message: 'Play client playSDK not ready - call Initialize() first',
                    payload: null
                });
            }

            try {
                console.log('Creating matchmaking client with appId:', appId, 'debugMode:', debugMode);
                const clientResult = globalThis.playClient.newMatchmakingClient(appId, debugMode);
                globalThis.matchmakingClient = await Module.ViverseCore._ensurePromise(clientResult);

                // Validate that the matchmaking client was created successfully
                if (!globalThis.matchmakingClient) {
                    console.error('Matchmaking client creation returned null/undefined');
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_UNKNOWN,
                        message: 'Matchmaking client creation failed - returned null',
                        payload: null
                    });
                }

                // Verify the client has required methods
                if (typeof globalThis.matchmakingClient.setActor !== 'function' ||
                    typeof globalThis.matchmakingClient.createRoom !== 'function' ||
                    typeof globalThis.matchmakingClient.joinRoom !== 'function') {
                    console.error('Matchmaking client missing required methods');
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_UNKNOWN,
                        message: 'Matchmaking client missing required methods',
                        payload: null
                    });
                }

                console.log('Assigned matchmaking client to globalThis:', {
                    matchmakingClient: !!globalThis.matchmakingClient,
                    hasSetActor: typeof globalThis.matchmakingClient?.setActor === 'function'
                });

                // Set up ALL event listeners immediately (like in client_script_example_modified)
                console.log('Setting up matchmaking client event listeners...');

                let connected = false;
                let joinedLobby = false;
                let currentState = 'unknown';

                // Critical connection events - update global state
                globalThis.matchmakingClient.on("onConnect", (data) => {
                    console.log("🔌 [MATCHMAKING_EVENT] onConnect triggered");
                    console.log("🔌 [MATCHMAKING_DATA] Connect data:", JSON.stringify(data || { connected: true }));
                    connected = true;
                    Module.ViverseCore._sdkState.connected = true;

                    // Dispatch to C# if dispatcher available
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingRawEvent('onConnect', JSON.stringify(data || { connected: true }));
                    }
                    
                    console.log("✅ [SEQUENCING_MILESTONE] Step 1/2 Complete: Connected to matchmaking server");
                });

                globalThis.matchmakingClient.on("onJoinedLobby", (data) => {
                    console.log("🏛️ [MATCHMAKING_EVENT] onJoinedLobby triggered - CRITICAL for room operations");
                    console.log("🏛️ [MATCHMAKING_DATA] Lobby data:", JSON.stringify(data || { joined: true }));
                    joinedLobby = true;
                    Module.ViverseCore._sdkState.joinedLobby = true;

                    // Dispatch to C# if dispatcher available
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingRawEvent('onJoinedLobby', JSON.stringify(data || { joined: true }));
                    }
                    
                    console.log("✅ [SEQUENCING_MILESTONE] Step 2/2 Complete: Joined lobby - GetAvailableRooms now available");
                    console.log("💡 [SEQUENCING_READY] SDK ready for: SetActor, CreateRoom, JoinRoom, GetAvailableRooms");
                });

                // Room events - update global state
                globalThis.matchmakingClient.on("onJoinRoom", (room) => {
                    console.log("🚪 [MATCHMAKING_EVENT] onJoinRoom triggered");
                    console.log("🚪 [MATCHMAKING_DATA] Room join data:", JSON.stringify(room || {}));
                    if (room && room.id) {
                        console.log(`🚪 [ROOM_SUCCESS] Successfully joined room: ${room.id}`);
                        console.log(`🚪 [ROOM_INFO] Room details: ${room.name || 'Unknown'} (${room.currentPlayers || 0}/${room.maxPlayers || 0} players)`);
                        Module.ViverseCore._sdkState.currentRoomId = room.id;

                        // Resolve any pending join room operations
                        Module.ViverseCore._resolvePendingOperation('joinRoom', { success: true, room: room });
                    } else {
                        console.error("🚪 [ROOM_ERROR] Invalid room data received onJoinRoom:", room);
                        Module.ViverseCore._resolvePendingOperation('joinRoom', { success: false, error: 'Invalid room data' });
                    }

                    // Dispatch to C# if dispatcher available
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingRawEvent('onJoinRoom', JSON.stringify(room || {}));
                    }
                });

                globalThis.matchmakingClient.on("onRoomListUpdate", (rooms) => {
                    console.log(`📋 [MATCHMAKING_EVENT] onRoomListUpdate triggered - Room discovery event`);
                    console.log(`📋 [ROOM_LIST] Found ${rooms?.length || 0} available rooms`);
                    
                    if (rooms && Array.isArray(rooms) && rooms.length > 0) {
                        console.log(`📋 [ROOM_DETAILS] Available rooms:`);
                        rooms.forEach((room, index) => {
                            console.log(`📋   ${index + 1}. ID: ${room.id || room.roomId || 'Unknown'}, Name: ${room.name || 'Unnamed'}, Players: ${room.currentPlayers || 0}/${room.maxPlayers || 0}`);
                        });
                    } else {
                        console.log(`📋 [ROOM_LIST_EMPTY] No rooms currently available - first to create will be room owner`);
                    }

                    // Dispatch to C# if dispatcher available
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingRawEvent('onRoomListUpdate', JSON.stringify(rooms || []));
                    }
                });

                globalThis.matchmakingClient.on("onRoomActorChange", (actors) => {
                    console.log(`👥 [MATCHMAKING_EVENT] onRoomActorChange triggered - Player activity event`);
                    console.log(`👥 [ACTOR_COUNT] Current player count: ${actors?.length || 0}`);
                    
                    if (actors && Array.isArray(actors) && actors.length > 0) {
                        console.log(`👥 [ACTOR_DETAILS] Players in room:`);
                        actors.forEach((actor, index) => {
                            console.log(`👥   ${index + 1}. Session: ${actor.session_id || 'Unknown'}, Name: ${actor.name || 'Unnamed'}`);
                        });
                    } else {
                        console.log(`👥 [ACTOR_EMPTY] No players currently in room`);
                    }

                    // Dispatch to C# if dispatcher available
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingRawEvent('onRoomActorChange', JSON.stringify(actors || []));
                    }
                });

                globalThis.matchmakingClient.on("onRoomClosed", (data) => {
                    console.log("🚪 [MATCHMAKING_EVENT] onRoomClosed triggered - Room termination event");
                    console.log("🚪 [ROOM_CLOSED] Room has been closed by owner or system");
                    
                    // Clear room state since we're no longer in the room
                    Module.ViverseCore._sdkState.currentRoomId = null;
                    console.log("🔄 [ROOM_STATE] Cleared room ID - no longer in any room");

                    // Dispatch to C# if dispatcher available
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingRawEvent('onRoomClosed', JSON.stringify(data || { closed: true }));
                    }
                });

                // Error handling
                globalThis.matchmakingClient.on("onError", (error) => {
                    console.error("❌ [MATCHMAKING_EVENT] onError triggered - System error event");
                    console.error("❌ [ERROR_DETAILS] Error data:", JSON.stringify(error || { message: 'Unknown error' }));

                    // Dispatch to C# if dispatcher available
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingRawEvent('onError', JSON.stringify(error || { message: 'Unknown error' }));
                    }
                });

                // State tracking
                globalThis.matchmakingClient.on("stateChange", (state) => {
                    console.log("🔄 [MATCHMAKING_EVENT] stateChange triggered - SDK state transition");
                    console.log(`🔄 [STATE_CHANGE] New state: ${state || 'unknown'} (was: ${currentState})`);
                    
                    // Handle disconnection - clear actor and room state since server forgets everything
                    if (state === 'Disconnected' || state === 'Error' || state === 'Reconnecting') {
                        console.log('🚨 [STATE_CHANGE] Disconnection detected - clearing all state since server forgets actor and room info');
                        Module.ViverseCore._clearActorState();
                        Module.ViverseCore._sdkState.connected = false;
                        Module.ViverseCore._sdkState.joinedLobby = false;
                        Module.ViverseCore._sdkState.currentRoomId = null; // Server disconnection means we're no longer in any room
                        
                        // Clear any pending operations since they're now invalid
                        Module.ViverseCore._rejectAllPendingOperations('Connection lost - server disconnection detected');
                        
                        console.log('🔄 [STATE_CLEANUP] All state cleared - will need fresh SetActor and room operations on reconnection');
                    }
                    
                    currentState = state;

                    // Dispatch to C# if dispatcher available
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingRawEvent('stateChange', JSON.stringify({ state: state, previousState: currentState }));
                    }
                });

                console.log("Matchmaking client event listeners attached.");

                // NEW FLOW: Wait for connection first (enables GetAvailableRooms)
                console.log('Waiting for matchmaking client connection...');
                console.log('📋 SEQUENCING: Connect → GetAvailableRooms Available → Join Lobby → Full Room Operations');
                let attempts = 0;
                const maxAttempts = 150; // 15 seconds maximum wait

                // Step 1: Wait for connection (enables GetAvailableRooms)
                while (!connected && attempts < maxAttempts) {
                    await new Promise(resolve => setTimeout(resolve, 100));
                    attempts++;

                    // Log progress every 2 seconds
                    if (attempts % 20 === 0) {
                        console.log(`📋 SEQUENCING_UPDATE: Connected: ${connected}, State: ${currentState} (attempt ${attempts}/${maxAttempts})`);
                    }
                }

                if (!connected) {
                    console.error('💥 SEQUENCING_FAILED: Matchmaking client failed to connect within timeout');
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_TIMEOUT,
                        message: 'Matchmaking client failed to connect to server within 15 seconds',
                        payload: null
                    });
                }

                console.log('✅ [SEQUENCING_MILESTONE] Connected - SetActor and room operations now available!');

                // Brief optional wait for lobby join (100ms max)
                console.log('🕰️ Brief wait for optional lobby join...');
                const lobbyWaitAttempts = 1; // Just 100ms
                for (let i = 0; i < lobbyWaitAttempts && !joinedLobby; i++) {
                    await new Promise(resolve => setTimeout(resolve, 100));
                }
                
                if (joinedLobby) {
                    console.log('✅ [LOBBY_BONUS] Lobby join completed quickly - enhanced features available');
                } else {
                    console.log('💡 [LOBBY_SKIP] Proceeding without lobby join - all basic room operations ready');
                }

                // Return success regardless of lobby join status
                console.log('🎉 [SEQUENCING_COMPLETE] Matchmaking client ready - all room operations available');
                const lobbyStatus = joinedLobby ? 'with enhanced lobby features' : 'with basic room operations';
                console.log(`🎉 [OPERATIONS_READY] Connected successfully ${lobbyStatus}`);
                
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: `Matchmaking client connected successfully - all room operations available ${lobbyStatus}`,
                    payload: JSON.stringify({ appId: appId, debugMode: debugMode, connected: true, joinedLobby: joinedLobby })
                });

                // This code is unreachable since we return success above regardless of lobby join status
            } catch (e) {
                console.error('NewMatchmakingClient Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Exception during matchmaking client creation: ' + e.message,
                    payload: null
                });
            }
        }
    },

    // Matchmaking System Functions
    Matchmaking: {
        get ReturnCode() {
            return Module['ViverseReturnCodes'];
        },
        _eventListeners: new Map(), // Map<listenerId, {eventType, callback}>

        SetActor: async function(actorJson) {
            if (!globalThis.matchmakingClient) {
                console.error('Matchmaking client not available for SetActor');
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_MODULE_NOT_LOADED,
                    message: 'Matchmaking client not available - call NewMatchmakingClient() first',
                    payload: null
                });
            }

            // Note: Don't check global SDK state here because each matchmaking client has its own connection state
            // The matchmaking client will return appropriate error if not connected


            try {
                const actorData = JSON.parse(actorJson);

                // Validate required actor fields
                if (!actorData.session_id || !actorData.name) {
                    console.error('Actor data missing required fields (session_id, name)');
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_INVALID_PARAMETER,
                        message: 'Actor data missing required fields: session_id and name are required',
                        payload: null
                    });
                }

                console.log('Setting actor with session_id:', actorData.session_id, 'name:', actorData.name);

                // Convert Unity's properties array format to JavaScript object format if needed
                Module.ViverseCore._convertUnityPropertiesToJSFormat(actorData, 'actor');

                // Call setActor directly without delays (matching working JavaScript examples)
                console.log('Calling setActor on globalThis.matchmakingClient...');
                console.log('Final actor data being sent:', actorData);
                const actorResult = await globalThis.matchmakingClient.setActor(actorData);
                console.log('setActor completed with result:', actorResult);

                // Check result
                if (actorResult && actorResult.success) {
                    // Success case - update global state with actor information
                    console.log('SetActor succeeded:', actorResult.message || 'Actor set successfully');
                    Module.ViverseCore._sdkState.actorSet = true;
                    Module.ViverseCore._sdkState.actorInfo = actorData; // Store actor info for validation
                    console.log('Actor set successfully on server - SDK now ready for room operations');
                    console.log('Stored actor info:', Module.ViverseCore._sdkState.actorInfo);
                    
                    return Promise.resolve({
                        returnCode: this.ReturnCode.SUCCESS,
                        message: 'Actor set successfully',
                        payload: actorJson
                    });
                } else {
                    // Failure case
                    const errorMsg = actorResult?.message || actorResult?.error || 'SetActor failed';
                    console.error('SetActor failed:', errorMsg);
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_UNKNOWN,
                        message: 'SetActor failed: ' + errorMsg,
                        payload: null
                    });
                }
            } catch (e) {
                console.error('SetActor Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Failed to set actor: ' + e.message,
                    payload: null
                });
            }
        },

        CreateRoom: async function(roomJson) {
            if (!globalThis.matchmakingClient) {
                console.error('Matchmaking client not available for CreateRoom');
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_MODULE_NOT_LOADED,
                    message: 'Matchmaking client not available - call NewMatchmakingClient() first',
                    payload: null
                });
            }

            // Validate actor state before room operations to prevent early disconnects
            const actorValidation = Module.ViverseCore._validateActorState('CreateRoom');
            if (!actorValidation.valid) {
                console.error('CreateRoom failed actor validation:', actorValidation.error);
                return Promise.resolve({
                    returnCode: actorValidation.code,
                    message: actorValidation.error,
                    payload: null
                });
            }

            try {
                const roomData = JSON.parse(roomJson);

                // Validate required room fields
                if (!roomData.name || !roomData.mode || typeof roomData.maxPlayers !== 'number') {
                    console.error('Room data missing required fields (name, mode, maxPlayers)');
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_INVALID_PARAMETER,
                        message: 'Room data missing required fields: name, mode, and maxPlayers are required',
                        payload: null
                    });
                }

                // Convert Unity's properties array format to JavaScript object format if needed
                Module.ViverseCore._convertUnityPropertiesToJSFormat(roomData, 'room');

                console.log('Creating room:', roomData.name, 'mode:', roomData.mode, 'maxPlayers:', roomData.maxPlayers);
                console.log('Final room data being sent:', roomData);

                const roomResult = globalThis.matchmakingClient.createRoom(roomData);
                const result = await Module.ViverseCore._ensurePromise(roomResult);

                // Validate the result contains room information
                if (!result || (!result.roomId && !result.id)) {
                    console.error('CreateRoom failed - no room ID returned');
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_UNKNOWN,
                        message: 'CreateRoom failed - no room ID returned from server',
                        payload: null
                    });
                }

                console.log('Room created successfully with ID:', result.roomId || result.id);
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Room created successfully',
                    payload: JSON.stringify(result)
                });
            } catch (e) {
                console.error('CreateRoom Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Failed to create room: ' + e.message,
                    payload: null
                });
            }
        },

        JoinRoom: async function(roomId) {
            if (!globalThis.matchmakingClient) {
                console.error('Matchmaking client not available for JoinRoom');
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_MODULE_NOT_LOADED,
                    message: 'Matchmaking client not available - call NewMatchmakingClient() first',
                    payload: null
                });
            }

            // Validate actor state before room operations to prevent early disconnects
            const actorValidation = Module.ViverseCore._validateActorState('JoinRoom');
            if (!actorValidation.valid) {
                console.error('JoinRoom failed actor validation:', actorValidation.error);
                return Promise.resolve({
                    returnCode: actorValidation.code,
                    message: actorValidation.error,
                    payload: null
                });
            }

            // Validate room ID parameter
            if (!roomId || typeof roomId !== 'string') {
                console.error('Invalid room ID provided for JoinRoom');
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_INVALID_PARAMETER,
                    message: 'Room ID is required and must be a string',
                    payload: null
                });
            }

            try {
                console.log('Joining room with ID:', roomId);
                const joinResult = globalThis.matchmakingClient.joinRoom(roomId);
                const result = await Module.ViverseCore._ensurePromise(joinResult);

                // Validate the result contains room information
                if (!result || (!result.roomId && !result.id)) {
                    console.error('JoinRoom failed - no room data returned');
                    return Promise.resolve({
                        returnCode: this.ReturnCode.ERROR_UNKNOWN,
                        message: 'JoinRoom failed - no room data returned from server',
                        payload: null
                    });
                }

                console.log('Room joined successfully with ID:', result.roomId || result.id);
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Room joined successfully',
                    payload: JSON.stringify(result)
                });
            } catch (e) {
                console.error('JoinRoom Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Failed to join room: ' + e.message,
                    payload: null
                });
            }
        },

        LeaveRoom: async function() {
            if (!globalThis.matchmakingClient) {
                return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
            }

            // Validate actor state before room operations to prevent early disconnects
            const actorValidation = Module.ViverseCore._validateActorState('LeaveRoom');
            if (!actorValidation.valid) {
                console.error('LeaveRoom failed actor validation:', actorValidation.error);
                return Promise.resolve({
                    returnCode: actorValidation.code,
                    message: actorValidation.error,
                    payload: null
                });
            }

            try {
                console.log('Leaving room');
                await globalThis.matchmakingClient.leaveRoom();

                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Room left successfully',
                    payload: null
                });
            } catch (e) {
                console.error('LeaveRoom Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Failed to leave room: ' + e.message,
                    payload: null
                });
            }
        },

        CloseRoom: async function() {
            if (!globalThis.matchmakingClient) {
                return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
            }

            // Validate actor state before room operations to prevent early disconnects
            const actorValidation = Module.ViverseCore._validateActorState('CloseRoom');
            if (!actorValidation.valid) {
                console.error('CloseRoom failed actor validation:', actorValidation.error);
                return Promise.resolve({
                    returnCode: actorValidation.code,
                    message: actorValidation.error,
                    payload: null
                });
            }

            try {
                console.log('Closing room');
                await globalThis.matchmakingClient.closeRoom();

                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Room closed successfully',
                    payload: null
                });
            } catch (e) {
                console.error('CloseRoom Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Failed to close room: ' + e.message,
                    payload: null
                });
            }
        },

        GetAvailableRooms: async function() {
            if (!globalThis.matchmakingClient) {
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_MODULE_NOT_LOADED,
                    message: 'Matchmaking client not available - call NewMatchmakingClient() first',
                    payload: null
                });
            }

            // Note: Don't check global SDK state here because each matchmaking client has its own connection state
            // The matchmaking client will return appropriate error if not connected

            try {
                console.log('Getting available rooms (connection-only requirement)');
                const rooms = await globalThis.matchmakingClient.getAvailableRooms();

                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Available rooms retrieved successfully',
                    payload: JSON.stringify(rooms)
                });
            } catch (e) {
                console.error('GetAvailableRooms Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Failed to get available rooms: ' + e.message,
                    payload: null
                });
            }
        },

        GetMyRoomActors: async function() {
            if (!globalThis.matchmakingClient) {
                return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
            }

            try {
                console.log('Getting room actors');
                const actors = await globalThis.matchmakingClient.getMyRoomActors();

                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Room actors retrieved successfully',
                    payload: JSON.stringify(actors)
                });
            } catch (e) {
                console.error('GetMyRoomActors Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Failed to get room actors: ' + e.message,
                    payload: null
                });
            }
        },

        // Event listener management
        RegisterEventListener: function(eventType, listenerId) {
            if (!globalThis.matchmakingClient) {
                console.error('Matchmaking client not initialized');
                return this.ReturnCode.ERROR_MODULE_NOT_LOADED;
            }

            try {
                console.log('Registering event listener:', eventType, listenerId);

                // Validate event type (must not be UNSET_VALUE)
                if (!Module.ViverseCore.MatchmakingEventType.isValidEventType(eventType)) {
                    console.error('Invalid matchmaking event type:', eventType, 'Expected valid enum value (not UNSET_VALUE)');
                    return this.ReturnCode.ERROR_INVALID_PARAMETER;
                }

                const callback = (data) => {
                    console.log('Matchmaking event received:', eventType, data);
                    // Route event to C# through global event dispatcher
                    if (globalThis.ViverseEventDispatcher) {
                        globalThis.ViverseEventDispatcher.dispatchMatchmakingEvent(eventType, JSON.stringify(data));
                    }
                };

                this._eventListeners.set(listenerId, { eventType, callback });

                // Convert enum value to JavaScript event name and register
                const jsEventName = Module.ViverseCore.MatchmakingEventType.toJSEventName(eventType);
                if (jsEventName === 'unknown') {
                    console.warn('Unknown matchmaking event type:', eventType);
                    return this.ReturnCode.ERROR_INVALID_PARAMETER;
                }

                globalThis.matchmakingClient.on(jsEventName, callback);
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('RegisterEventListener Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        UnregisterEventListener: function(listenerId) {
            try {
                const listener = this._eventListeners.get(listenerId);
                if (!listener) {
                    console.warn('Listener not found:', listenerId);
                    return this.ReturnCode.ERROR_NOT_FOUND;
                }

                console.log('Unregistering event listener:', listenerId);

                // Remove from matchmaking client using enum conversion
                if (globalThis.matchmakingClient) {
                    const jsEventName = Module.ViverseCore.MatchmakingEventType.toJSEventName(listener.eventType);
                    if (jsEventName !== 'unknown') {
                        globalThis.matchmakingClient.off(jsEventName, listener.callback);
                    }
                }

                this._eventListeners.delete(listenerId);
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('UnregisterEventListener Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        }
    },

    // Multiplayer Communication Functions
    Multiplayer: {
        get ReturnCode() {
            return Module['ViverseReturnCodes'];
        },
        // Multiplayer client now stored in globalThis.multiplayerClient
        _eventListeners: new Map(), // Map<listenerId, {eventType, callback}>

        Initialize: async function(roomId, appId) {
            if (!globalThis.play) {
                console.error('globalThis.play not available');
                return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
            }

            try {
                console.log('Initializing multiplayer client with roomId:', roomId, 'appId:', appId);
                globalThis.multiplayerClient = new globalThis.play.MultiplayerClient(roomId, appId);

                const initResult = globalThis.multiplayerClient.init();
                const sessionInfo = await Module.ViverseCore._ensurePromise(initResult);
                console.log('Multiplayer client initialized with session info:', sessionInfo);

                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Multiplayer client initialized successfully',
                    payload: JSON.stringify(sessionInfo)
                });
            } catch (e) {
                console.error('Multiplayer Initialize Error:', e);
                return Promise.resolve({
                    returnCode: this.ReturnCode.ERROR_EXCEPTION,
                    message: 'Failed to initialize multiplayer client: ' + e.message,
                    payload: null
                });
            }
        },

        // General Messaging
        SendMessage: function(messageJson) {
            if (!globalThis.multiplayerClient) {
                console.error('Multiplayer client not initialized');
                return this.ReturnCode.ERROR_MODULE_NOT_LOADED;
            }

            try {
                const message = JSON.parse(messageJson);
                console.log('Sending general message:', message);
                globalThis.multiplayerClient.general.sendMessage(message);
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('SendMessage Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        // Network Sync (Position)
        UpdateMyPosition: function(positionJson) {
            if (!globalThis.multiplayerClient) {
                console.error('Multiplayer client not initialized');
                return this.ReturnCode.ERROR_MODULE_NOT_LOADED;
            }

            try {
                const position = JSON.parse(positionJson);
                console.log('Updating my position:', position);
                globalThis.multiplayerClient.networksync.updateMyPosition(position);
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('UpdateMyPosition Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        UpdateEntityPosition: function(entityId, positionJson) {
            if (!globalThis.multiplayerClient) {
                console.error('Multiplayer client not initialized');
                return this.ReturnCode.ERROR_MODULE_NOT_LOADED;
            }

            try {
                const position = JSON.parse(positionJson);
                console.log('Updating entity position:', entityId, position);
                globalThis.multiplayerClient.networksync.updateEntityPosition(entityId, position);
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('UpdateEntityPosition Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        // Action Sync (Competition)
        SendCompetition: function(actionName, actionMessage, actionId) {
            if (!globalThis.multiplayerClient) {
                console.error('Multiplayer client not initialized');
                return this.ReturnCode.ERROR_MODULE_NOT_LOADED;
            }

            try {
                console.log('Sending competition:', actionName, actionMessage, actionId);
                globalThis.multiplayerClient.actionsync.competition(actionName, actionMessage, actionId);
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('SendCompetition Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        // Real-time Leaderboard
        UpdateLeaderboard: function(score) {
            if (!globalThis.multiplayerClient) {
                console.error('Multiplayer client not initialized');
                return this.ReturnCode.ERROR_MODULE_NOT_LOADED;
            }

            try {
                console.log('Updating leaderboard score:', score);
                globalThis.multiplayerClient.leaderboard.leaderboardUpdate(score);
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('UpdateLeaderboard Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        // Event Listener Management
        RegisterMessageListener: function(listenerId) {
            return this._registerListener(listenerId, Module.ViverseCore.MultiplayerEventType.Message, (data) => {
                console.log('Message received:', data);
                if (globalThis.ViverseEventDispatcher) {
                    globalThis.ViverseEventDispatcher.dispatchMultiplayerEvent(Module.ViverseCore.MultiplayerEventType.Message, JSON.stringify(data));
                }
            });
        },

        RegisterPositionListener: function(listenerId) {
            return this._registerListener(listenerId, Module.ViverseCore.MultiplayerEventType.Position, (data) => {
                console.log('Position update received:', data);
                if (globalThis.ViverseEventDispatcher) {
                    globalThis.ViverseEventDispatcher.dispatchMultiplayerEvent(Module.ViverseCore.MultiplayerEventType.Position, JSON.stringify(data));
                }
            });
        },

        RegisterCompetitionListener: function(listenerId) {
            return this._registerListener(listenerId, Module.ViverseCore.MultiplayerEventType.Competition, (data) => {
                console.log('Competition result received:', data);
                if (globalThis.ViverseEventDispatcher) {
                    globalThis.ViverseEventDispatcher.dispatchMultiplayerEvent(Module.ViverseCore.MultiplayerEventType.Competition, JSON.stringify(data));
                }
            });
        },

        RegisterLeaderboardListener: function(listenerId) {
            return this._registerListener(listenerId, Module.ViverseCore.MultiplayerEventType.Leaderboard, (data) => {
                console.log('Leaderboard update received:', data);
                if (globalThis.ViverseEventDispatcher) {
                    globalThis.ViverseEventDispatcher.dispatchMultiplayerEvent(Module.ViverseCore.MultiplayerEventType.Leaderboard, JSON.stringify(data));
                }
            });
        },

        _registerListener: function(listenerId, eventType, callback) {
            if (!globalThis.multiplayerClient) {
                console.error('Multiplayer client not initialized');
                return this.ReturnCode.ERROR_MODULE_NOT_LOADED;
            }

            try {
                console.log('Registering multiplayer listener:', eventType, listenerId);

                // Validate event type (must not be UNSET_VALUE)
                if (!Module.ViverseCore.MultiplayerEventType.isValidEventType(eventType)) {
                    console.error('Invalid multiplayer event type:', eventType, 'Expected valid enum value (not UNSET_VALUE)');
                    return this.ReturnCode.ERROR_INVALID_PARAMETER;
                }

                this._eventListeners.set(listenerId, { eventType, callback });

                // Register with the appropriate multiplayer event using enum
                switch (eventType) {
                    case Module.ViverseCore.MultiplayerEventType.Message:
                        globalThis.multiplayerClient.general.onMessage(callback);
                        break;
                    case Module.ViverseCore.MultiplayerEventType.Position:
                        globalThis.multiplayerClient.networksync.onNotifyPositionUpdate(callback);
                        break;
                    case Module.ViverseCore.MultiplayerEventType.Competition:
                        globalThis.multiplayerClient.actionsync.onCompetition(callback);
                        break;
                    case Module.ViverseCore.MultiplayerEventType.Leaderboard:
                        globalThis.multiplayerClient.leaderboard.onLeaderboardUpdate(callback);
                        break;
                    default:
                        console.warn('Unknown multiplayer event type:', eventType);
                        return this.ReturnCode.ERROR_INVALID_PARAMETER;
                }

                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('RegisterListener Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        UnregisterListener: function(listenerId) {
            try {
                const listener = this._eventListeners.get(listenerId);
                if (!listener) {
                    console.warn('Multiplayer listener not found:', listenerId);
                    return this.ReturnCode.ERROR_NOT_FOUND;
                }

                console.log('Unregistering multiplayer listener:', listenerId);

                // Note: The JavaScript SDK doesn't provide off() methods for multiplayer events
                // We'll just remove from our internal tracking
                this._eventListeners.delete(listenerId);
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('UnregisterListener Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        }
    },

    // Configuration Functions
    SetAvatarHost: function(host) {
      this._config.avatarHost = host;
    },

    SetLeaderboardHosts: function(baseURL, communityBaseURL) {
        this._config.leaderboardHost = baseURL;
        this._config.communityHost = communityBaseURL;
    },

    // Auto-initialization of dependent services when token becomes available
    _autoInitializeDependentServices: async function() {
        console.log('Auto-initializing dependent services...');

        if (!this._token || !this._token.access_token) {
            console.log('No token available, skipping auto-initialization');
            return;
        }

        try {
            // Initialize Avatar service if not already initialized
            if (!globalThis.avatarClient) {
                console.log('Auto-initializing Avatar service...');
                const avatarResult = await this.Avatar.Initialize();
                if (avatarResult && avatarResult.returnCode === this.ReturnCode.SUCCESS) {
                    console.log('Avatar service auto-initialized successfully');
                } else {
                    console.warn('Avatar service auto-initialization failed:', avatarResult);
                }
            } else {
                console.log('Avatar service already initialized');
            }

            // Initialize Leaderboard service if not already initialized
            if (!globalThis.gameDashboardClient) {
                console.log('Auto-initializing Leaderboard service...');
                const leaderboardResult = await this.Leaderboard.Initialize();
                if (leaderboardResult && leaderboardResult.returnCode === this.ReturnCode.SUCCESS) {
                    console.log('Leaderboard service auto-initialized successfully');
                } else {
                    console.warn('Leaderboard service auto-initialization failed:', leaderboardResult);
                }
            } else {
                console.log('Leaderboard service already initialized');
            }

            console.log('Auto-initialization of dependent services completed');
        } catch (e) {
            console.error('Error during auto-initialization of dependent services:', e);
        }
    },

    // Get current SDK state for debugging and monitoring
    GetSDKState: function() {
        return JSON.stringify({
            connected: this._sdkState.connected,
            joinedLobby: this._sdkState.joinedLobby,
            actorSet: this._sdkState.actorSet,
            actorInfo: this._sdkState.actorInfo, // Include actor information for validation
            currentRoomId: this._sdkState.currentRoomId,
            currentState: this._sdkState.currentState,
            pendingOperations: this._sdkState.pendingOperations.size,
            isReadyForRoomOperations: this._isReadyForRoomOperations()
        });
    }
};

// Expose ViverseEventDispatcher to globalThis for JavaScript->C# event bridging using callbacks
if (typeof globalThis !== 'undefined') {
    globalThis.ViverseEventDispatcher = {
        dispatchMatchmakingEvent: function(eventType, eventData) {
            try {
                console.log('ViverseEventDispatcher.dispatchMatchmakingEvent called:', eventType, eventData);

                // Use registered callback instead of SendMessage
                if (globalThis.ViverseEventCallbacks && globalThis.ViverseEventCallbacks.matchmakingCallback) {
                    // Create combined event message: "eventType|eventData"
                    const eventMessage = eventType + '|' + (eventData || '{}');

                    // Allocate string in Unity heap and call callback (same pattern as ViverseAsyncHelper.safeCallback)
                    const length = lengthBytesUTF8(eventMessage) + 1;
                    const ptr = _malloc(length);
                    try {
                        stringToUTF8(eventMessage, ptr, length);
                        globalThis.ViverseEventCallbacks.matchmakingCallback(ptr);
                    } finally {
                        _free(ptr);
                    }

                    console.log('Matchmaking event dispatched via callback');
                } else {
                    console.warn('Matchmaking event callback not registered');
                }
            } catch (e) {
                console.error('Error in dispatchMatchmakingEvent:', e);
            }
        },

        dispatchMultiplayerEvent: function(eventType, eventData) {
            try {
                console.log('ViverseEventDispatcher.dispatchMultiplayerEvent called:', eventType, eventData);

                // Use registered callback instead of SendMessage
                if (globalThis.ViverseEventCallbacks && globalThis.ViverseEventCallbacks.multiplayerCallback) {
                    // Create combined event message: "eventType|eventData"
                    const eventMessage = eventType + '|' + (eventData || '{}');

                    // Allocate string in Unity heap and call callback (same pattern as ViverseAsyncHelper.safeCallback)
                    const length = lengthBytesUTF8(eventMessage) + 1;
                    const ptr = _malloc(length);
                    try {
                        stringToUTF8(eventMessage, ptr, length);
                        globalThis.ViverseEventCallbacks.multiplayerCallback(ptr);
                    } finally {
                        _free(ptr);
                    }

                    console.log('Multiplayer event dispatched via callback');
                } else {
                    console.warn('Multiplayer event callback not registered');
                }
            } catch (e) {
                console.error('Error in dispatchMultiplayerEvent:', e);
            }
        },

        dispatchMatchmakingRawEvent: function(eventName, eventData) {
            try {
                console.log('ViverseEventDispatcher.dispatchMatchmakingRawEvent called:', eventName, eventData);

                // Convert string event name to enum value using SDKEventType
                const eventType = Module.ViverseCore.SDKEventType.fromString(eventName);
                if (eventType === -1) {
                    console.warn('Unknown SDK event type:', eventName);
                    return;
                }

                // Create strongly-typed SDK event data
                const sdkEventData = {
                    EventType: eventType,
                    EventData: eventData || '{}',
                    Timestamp: Date.now()
                };

                // Use registered callback for raw SDK events
                if (globalThis.ViverseEventCallbacks && globalThis.ViverseEventCallbacks.matchmakingRawCallback) {
                    // Send JSON-serialized SDKEventData
                    const eventMessage = JSON.stringify(sdkEventData);

                    // Allocate string in Unity heap and call callback (same pattern as ViverseAsyncHelper.safeCallback)
                    const length = lengthBytesUTF8(eventMessage) + 1;
                    const ptr = _malloc(length);
                    try {
                        stringToUTF8(eventMessage, ptr, length);
                        globalThis.ViverseEventCallbacks.matchmakingRawCallback(ptr);
                    } finally {
                        _free(ptr);
                    }

                    console.log('Raw SDK event dispatched via callback:', eventName, 'enum:', eventType);
                } else {
                    console.log('No raw SDK event callback registered - event not dispatched to C#');
                }
            } catch (e) {
                console.error('Error in dispatchMatchmakingRawEvent:', e);
            }
        }
    };
}
