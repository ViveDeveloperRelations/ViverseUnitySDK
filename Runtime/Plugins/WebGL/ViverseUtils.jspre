Module['ViverseUtils'] = {
    $deps: ['ViverseReturnCodes', 'ViverseAsyncHelper'],
    // Reference shared return codes
    get ReturnCode() {
        return Module['ViverseReturnCodes'];
    },

    // Cookie Management
    Cookie_SetItem: function(name, value, days) {
        try {
            if (!name || !value) return this.ReturnCode.ERROR_INVALID_PARAMETER;

            var expires = "";
            if (days) {
                var date = new Date();
                date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
                expires = "; expires=" + date.toUTCString();
            }
            document.cookie = UTF8ToString(name) + "=" + UTF8ToString(value) + expires + "; path=/";
            return this.ReturnCode.SUCCESS;
        } catch (e) {
            console.error('Cookie_SetItem error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    Cookie_GetItem: function(name) {
        try {
            if (!name) return this.ReturnCode.ERROR_INVALID_PARAMETER;

            var nameStr = UTF8ToString(name) + "=";
            var decodedCookie = decodeURIComponent(document.cookie);
            var ca = decodedCookie.split(';');
            for(var i = 0; i < ca.length; i++) {
                var c = ca[i].trim();
                if (c.indexOf(nameStr) == 0) {
                    var value = c.substring(nameStr.length, c.length);
                    var bufferSize = lengthBytesUTF8(value) + 1;
                    var buffer = _malloc(bufferSize);
                    stringToUTF8(value, buffer, bufferSize);
                    return buffer;
                }
            }
            return this.ReturnCode.ERROR_NOT_FOUND;
        } catch (e) {
            console.error('Cookie_GetItem error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    Cookie_DeleteItem: function(name) {
        try {
            if (!name) return this.ReturnCode.ERROR_INVALID_PARAMETER;
            document.cookie = UTF8ToString(name) + "=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";
            return this.ReturnCode.SUCCESS;
        } catch (e) {
            console.error('Cookie_DeleteItem error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    // Session Storage
    SessionStorage_GetItem: function(key) {
        try {
            if (!key) return this.ReturnCode.ERROR_INVALID_PARAMETER;

            var value = sessionStorage.getItem(UTF8ToString(key));
            if (value === null) return this.ReturnCode.ERROR_NOT_FOUND;

            var bufferSize = lengthBytesUTF8(value) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(value, buffer, bufferSize);
            return buffer;
        } catch (e) {
            console.error('SessionStorage_GetItem error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    SessionStorage_SetItem: function(key, value) {
        try {
            if (!key || !value) return this.ReturnCode.ERROR_INVALID_PARAMETER;
            sessionStorage.setItem(UTF8ToString(key), UTF8ToString(value));
            return this.ReturnCode.SUCCESS;
        } catch (e) {
            console.error('SessionStorage_SetItem error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    SessionStorage_RemoveItem: function(key) {
        try {
            if (!key) return this.ReturnCode.ERROR_INVALID_PARAMETER;
            sessionStorage.removeItem(UTF8ToString(key));
            return this.ReturnCode.SUCCESS;
        } catch (e) {
            console.error('SessionStorage_RemoveItem error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    SessionStorage_Clear: function() {
        try {
            sessionStorage.clear();
            return this.ReturnCode.SUCCESS;
        } catch (e) {
            console.error('SessionStorage_Clear error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    // Device Detection
    GetUserAgent: function() {
        try {
            var ua = navigator.userAgent;
            var bufferSize = lengthBytesUTF8(ua) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(ua, buffer, bufferSize);
            return buffer;
        } catch (e) {
            console.error('GetUserAgent error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    IsMobile: function() {
        try {
            return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent) ?
                this.ReturnCode.SUCCESS : this.ReturnCode.ERROR_NOT_FOUND;
        } catch (e) {
            console.error('IsMobile error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    IsMobileIOS: function() {
        try {
            return /iPhone|iPad|iPod/i.test(navigator.userAgent) ?
                this.ReturnCode.SUCCESS : this.ReturnCode.ERROR_NOT_FOUND;
        } catch (e) {
            console.error('IsMobileIOS error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    IsHtcMobileVR: function() {
        try {
            return /Mobile VR/i.test(navigator.userAgent) ?
                this.ReturnCode.SUCCESS : this.ReturnCode.ERROR_NOT_FOUND;
        } catch (e) {
            console.error('IsHtcMobileVR error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    // WebXR Functions
    IsVRSupported: function() {
        try {
            if (window.xrManager === undefined) {
                return this.ReturnCode.ERROR_NOT_SUPPORTED;
            }
            return window.xrManager.isVRSupported ?
                this.ReturnCode.SUCCESS : this.ReturnCode.ERROR_NOT_SUPPORTED;
        } catch (e) {
            console.error('IsVRSupported error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    IsInXR: function() {
        try {
            //FIXME: find a reliable way to get unityinstance without chainging the core page template
            //For webxr exporter this happens automatically so i think for most of these functions this is actually ok other than i may have to use globalThis.unityInstance or window.unityInstance
            // First check if unityInstance exists
            if (typeof unityInstance === 'undefined' || !unityInstance) {
                console.warn('Unity instance not initialized');
                return this.ReturnCode.ERROR_UNITY_NOT_INITIALIZED;
            }

            // Then check for Module and WebXR
            if (!unityInstance.Module?.WebXR?.isInXR) {
                return this.ReturnCode.ERROR_NOT_SUPPORTED;
            }

            return unityInstance.Module.WebXR.isInXR ?
                this.ReturnCode.SUCCESS :
                this.ReturnCode.ERROR_NOT_FOUND;
        } catch (e) {
            console.error('IsInXR error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    ToggleVR: function() {
        try {
            //FIXME: find a reliable way to get unityinstance without chainging the core page template - might be fine for this specific usecase though
            if (!unityInstance?.Module?.WebXR?.toggleVR) {
                return this.ReturnCode.ERROR_NOT_SUPPORTED;
            }
            unityInstance.Module.WebXR.toggleVR();
            return this.ReturnCode.SUCCESS;
        } catch (e) {
            console.error('ToggleVR error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    },

    Free_String: function(ptr) {
        try {
            if (!ptr) return this.ReturnCode.ERROR_INVALID_PARAMETER;
            _free(ptr);
            return this.ReturnCode.SUCCESS;
        } catch (e) {
            console.error('Free_String error:', e);
            return this.ReturnCode.ERROR_EXCEPTION;
        }
    }
};