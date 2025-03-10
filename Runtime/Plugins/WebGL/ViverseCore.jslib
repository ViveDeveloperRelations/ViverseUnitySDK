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

    SSO_LoginWithRedirect: function(redirectUrl, taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      //console.log("SSO_LoginWithRedirect called with redirectUrl:", redirectUrl);

      try {
        const protocol = window.location.protocol;
        const hostname = window.location.hostname;
        const port = window.location.port ? `:${window.location.port}` : '';
        const pathname = window.location.pathname;
        const autoRedirectUrl = `${protocol}//${hostname}${port}${pathname}`;

        let redirectUrlToUse = '';
        if(redirectUrl !== null && redirectUrl !== undefined && redirectUrl !== "") {
            redirectUrlToUse = UTF8ToString(redirectUrl);
            //console.log('Using provided redirectUrl:', redirectUrlToUse);
        } else {
            redirectUrlToUse = autoRedirectUrl;
            //console.log('Using auto-detected redirectUrl:', redirectUrlToUse);
        }

        Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
            taskId,
            Module.ViverseCore.SSO.LoginWithRedirect(redirectUrlToUse),
            callbackWrapper
        );
      } catch (e) {
          console.error('SSO_LoginWithRedirect error:', e);
          // The enhanced wrapper will handle this error case
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
    SSO_HandleCallback: function(taskId, callback) {
      const callbackWrapper = ViverseCoreHelpers.createCallbackWrapper(callback);
      Module['ViverseAsyncHelper'].wrapAsyncWithPayload(
            taskId,
            Module.ViverseCore.SSO.HandleCallback(),
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

    // String management
    FreeString: function(ptr) {
        _free(ptr);
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
