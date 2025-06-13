Module['ViverseReturnCodes'] = {
    // Success codes (positive values)
    SUCCESS: 1,

    // Neutral codes
    NOT_SET: 0,

    // Error codes (negative values)
    ERROR_INVALID_PARAMETER: -1,
    ERROR_NOT_FOUND: -2,
    ERROR_UNAUTHORIZED: -3,
    ERROR_NOT_SUPPORTED: -4,
    ERROR_MODULE_NOT_LOADED: -5,
    ERROR_SDK_NOT_LOADED: -6,
    ERROR_SDK_NOT_INITIALIZED: -7,
    ERROR_UNITY_NOT_INITIALIZED: -8,
    ERROR_INVALID_STATE: -9,
    ERROR_PARSE_JSON: -10,

    // Specific SDK error codes (negative values, -11 to -50 range)
    ERROR_SDK_RETURNED_NULL: -11,          // SDK returned null/undefined - suggests client reinitialization
    ERROR_AUTHENTICATION_TIMEOUT: -12,     // Authentication operation timed out - suggests client reinitialization
    ERROR_CLIENT_CORRUPTED: -13,          // Client state appears corrupted - suggests client reinitialization
    ERROR_NETWORK_TIMEOUT: -14,           // Network timeout occurred - may be recoverable with retry
    ERROR_OAUTH_CALLBACK_FAILED: -15,     // OAuth callback processing failed - suggests retry OAuth flow

    // Generic errors (deep negative values)
    ERROR_UNKNOWN: -99,
    ERROR_EXCEPTION: -100
};