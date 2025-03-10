var ViverseUtilsLib = {
    $ViverseUtils: {}, // Will be populated by jspre file
    // Cookie functions
    Utils_Cookie_SetItem: function(name, value, days) {
        return Module.ViverseUtils.Cookie_SetItem(name, value, days);
    },

    Utils_Cookie_GetItem: function(name) {
        return Module.ViverseUtils.Cookie_GetItem(name);
    },

    Utils_Cookie_DeleteItem: function(name) {
        return Module.ViverseUtils.Cookie_DeleteItem(name);
    },

    // Session Storage functions
    Utils_SessionStorage_GetItem: function(key) {
        return Module.ViverseUtils.SessionStorage_GetItem(key);
    },

    Utils_SessionStorage_SetItem: function(key, value) {
        return Module.ViverseUtils.SessionStorage_SetItem(key, value);
    },

    Utils_SessionStorage_RemoveItem: function(key) {
        return Module.ViverseUtils.SessionStorage_RemoveItem(key);
    },

    Utils_SessionStorage_Clear: function() {
        return Module.ViverseUtils.SessionStorage_Clear();
    },

    // Device Detection functions
    Utils_GetUserAgent: function() {
        return Module.ViverseUtils.GetUserAgent();
    },

    Utils_IsMobile: function() {
        return Module.ViverseUtils.IsMobile();
    },

    Utils_IsMobileIOS: function() {
        return Module.ViverseUtils.IsMobileIOS();
    },

    Utils_IsHtcMobileVR: function() {
        return Module.ViverseUtils.IsHtcMobileVR();
    },

    // WebXR functions
    Utils_IsVRSupported: function() {
        return Module.ViverseUtils.IsVRSupported();
    },

    Utils_IsInXR: function() {
        return Module.ViverseUtils.IsInXR();
    },

    Utils_ToggleVR: function() {
        return Module.ViverseUtils.ToggleVR();
    },

    Utils_Free_String: function(ptr) {
        return Module.ViverseUtils.Free_String(ptr);
    }
};

// Ensure dependencies are correctly resolved before execution
autoAddDeps(ViverseUtilsLib, '$ViverseUtils');
mergeInto(LibraryManager.library, ViverseUtilsLib);