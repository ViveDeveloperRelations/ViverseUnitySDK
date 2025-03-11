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
            script.src = 'https://www.viverse.com/static-assets/viverse-sdk/1.2.3/viverse-sdk.umd.js';
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
        InitializeClient: function(clientId, domain, cookieDomain) {
            if (!globalThis.viverse) return this.ReturnCode.ERROR_SDK_NOT_LOADED;
            console.log('InitializeClient:', clientId, domain, cookieDomain);

            try {
                globalThis.viverseClient = new globalThis.viverse.client({
                    clientId: clientId,
                    domain: domain,
                    cookieDomain: cookieDomain === undefined ? '' : cookieDomain
                });
                return this.ReturnCode.SUCCESS;
            } catch (e) {
                console.error('SSO Init Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        LoginWithRedirect: async function(redirectUrl) {
            if (!globalThis.viverseClient) {
                console.log("Could not find viverse client");
                return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
            }

            try {
                await globalThis.viverseClient.loginWithRedirect({ redirectionUrl: redirectUrl });
                return Promise.resolve(this.ReturnCode.SUCCESS);
            } catch (e) {
                console.error('Login Error:', e);
                return Promise.resolve(this.ReturnCode.ERROR_EXCEPTION);
            }
        },

        Logout: async function(redirectUrl) {
          if (!globalThis.viverseClient) {
            console.log("Could not find viverse client");
            return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);
          }

          try {
            console.log("Logging out with redirect url:", redirectUrl);
            await globalThis.viverseClient.logout({ redirectionUrl: redirectUrl });
            console.log("Logging out with redirect url finished:", redirectUrl);

            // Clear all tokens and state
            Module.ViverseCore._token = null;
            Module.ViverseCore._isLoaded = false;

            // Reset all client instances
            if(globalThis.gameDashboardClient) {
              globalThis.gameDashboardClient = null;
            }
            if(globalThis.avatarClient) {
              globalThis.avatarClient = null;
            }
            globalThis.viverseClient = null;

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
            return Promise.resolve(this.ReturnCode.ERROR_EXCEPTION);
          }
        },

        HandleCallback: async function() {
            if (!globalThis.viverseClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const result = await globalThis.viverseClient.handleRedirectCallback();
                if (!result) return Promise.resolve(this.ReturnCode.ERROR_NOT_FOUND);
                const jsonStr = JSON.stringify(result);
                console.log('handleRedirectCallback:', jsonStr);

                //get access token since this is desired by the internal apis more than likely and different than the loginresult
                //relying on side effect of setting this._token of getaccesstoken here
                //FIXME: make sure that this._token is set in a more sane way
                //await this.GetAccessToken();
                //this doesn't work right away, need to wait

                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Operation completed successfully',
                    payload: jsonStr
                });
            } catch (e) {
                console.error('Callback Error:', e);
                return Promise.resolve(this.ReturnCode.ERROR_EXCEPTION);
            }
        },

        GetAccessToken: async function() {
            if (!globalThis.viverseClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const tokenResponse = await globalThis.viverseClient.getToken({detailedResponse: true});
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
                return Promise.resolve(this.ReturnCode.ERROR_EXCEPTION);
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
                return Promise.resolve(this.ReturnCode.SUCCESS);
            } catch (e) {
                console.error('Avatar Init Error:', e);
                return this.ReturnCode.ERROR_EXCEPTION;
            }
        },

        GetProfile: async function() {
            if (!globalThis.avatarClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const profile = await globalThis.avatarClient.getProfile();
                return Promise.resolve({
                    returnCode: this.ReturnCode.SUCCESS,
                    message: 'Operation completed successfully',
                    payload:  JSON.stringify(profile)
                });
            } catch (e) {
                console.error('Get Profile Error:', e);
                return Promise.resolve(this.ReturnCode.ERROR_EXCEPTION);
            }
        },

        GetAvatarList: async function() {
            if (!globalThis.avatarClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const avatars = await globalThis.avatarClient.getAvatarList();
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
            const publicAvatars = await globalThis.avatarClient.getPublicAvatarList();
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
                    token: {
                        accessToken: Module.ViverseCore._token.access_token
                    }
                });
                return Promise.resolve(this.ReturnCode.SUCCESS);
            } catch (e) {
                console.error('Leaderboard Init Error:', e);
                return Promise.resolve(this.ReturnCode.ERROR_EXCEPTION);
            }
        },

        UploadScore: async function(appId, leaderboardName, score) {
            if (!globalThis.gameDashboardClient) return Promise.resolve(this.ReturnCode.ERROR_MODULE_NOT_LOADED);

            try {
                const result = await globalThis.gameDashboardClient.uploadLeaderboardScore(
                    appId,
                    [{ name: leaderboardName, value: score }]
                );
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
                const result = await globalThis.gameDashboardClient.getLeaderboard(
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
        }
    },

    // Configuration Functions
    SetAvatarHost: function(host) {
      this._config.avatarHost = host;
    },

    SetLeaderboardHosts: function(baseURL, communityBaseURL) {
        this._config.leaderboardHost = baseURL;
        this._config.communityHost = communityBaseURL;
    }
};
