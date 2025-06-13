using System;
using UnityEngine;

namespace ViverseWebGLAPI
{
    /// <summary>
    /// Centralized logging utility for the Viverse Unity SDK.
    /// Provides consistent logging patterns, categorization, and formatting across all SDK components.
    /// </summary>
    public static class ViverseLogger
    {
        /// <summary>
        /// Logging categories for different SDK components
        /// </summary>
        public static class Categories
        {
            public const string CORE = "VIVERSE_CORE";
            public const string ROOM = "VIVERSE_ROOM";
            public const string SSO = "VIVERSE_SSO";
            public const string AVATAR = "VIVERSE_AVATAR";
            public const string LEADERBOARD = "VIVERSE_LEADERBOARD";
            public const string MULTIPLAYER = "VIVERSE_MULTIPLAYER";
            public const string MATCHMAKING = "VIVERSE_MATCHMAKING";
            public const string PLAY_SERVICE = "VIVERSE_PLAY";
            public const string ASYNC_HELPER = "ViverseAsyncHelper";
            public const string EVENT_DISPATCHER = "VIVERSE_EVENTS";
            public const string SDK_STATE = "SDK_STATE_CHECK";
            public const string RAW_SDK_EVENT = "RAW_SDK_EVENT";
            public const string SMOKE_TEST = "SMOKE_TEST";
            public const string CONNECTION = "CONNECTION";
            public const string VALIDATION = "VALIDATION";
        }

        /// <summary>
        /// Log an informational message with category prefix
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="message">Message to log</param>
        public static void LogInfo(string category, string message)
        {
            Debug.Log($"[{category}] {message}");
        }

        /// <summary>
        /// Log an informational message with formatted arguments
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="messageFormat">Message format string</param>
        /// <param name="args">Arguments for formatting</param>
        public static void LogInfo(string category, string messageFormat, params object[] args)
        {
            Debug.Log($"[{category}] {string.Format(messageFormat, args)}");
        }

        /// <summary>
        /// Log a warning message with category prefix
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="message">Warning message to log</param>
        public static void LogWarning(string category, string message)
        {
            Debug.LogWarning($"[{category}] {message}");
        }

        /// <summary>
        /// Log a warning message with formatted arguments
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="messageFormat">Warning message format string</param>
        /// <param name="args">Arguments for formatting</param>
        public static void LogWarning(string category, string messageFormat, params object[] args)
        {
            Debug.LogWarning($"[{category}] {string.Format(messageFormat, args)}");
        }

        /// <summary>
        /// Log an error message with category prefix
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="message">Error message to log</param>
        public static void LogError(string category, string message)
        {
            Debug.LogError($"[{category}] {message}");
        }

        /// <summary>
        /// Log an error message with formatted arguments
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="messageFormat">Error message format string</param>
        /// <param name="args">Arguments for formatting</param>
        public static void LogError(string category, string messageFormat, params object[] args)
        {
            Debug.LogError($"[{category}] {string.Format(messageFormat, args)}");
        }

        /// <summary>
        /// Log an exception with category prefix and additional context
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="exception">Exception to log</param>
        /// <param name="contextMessage">Additional context about when/where the exception occurred</param>
        public static void LogException(string category, Exception exception, string contextMessage = null)
        {
            string message = string.IsNullOrEmpty(contextMessage)
                ? $"Exception occurred: {exception.Message}"
                : $"{contextMessage}: {exception.Message}";

            Debug.LogError($"[{category}] {message}");
            Debug.LogException(exception);
        }

        /// <summary>
        /// Log operation success with timing information
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="operation">Name of the successful operation</param>
        /// <param name="additionalInfo">Optional additional information</param>
        public static void LogSuccess(string category, string operation, string additionalInfo = null)
        {
            string message = string.IsNullOrEmpty(additionalInfo)
                ? $"✅ {operation} completed successfully"
                : $"✅ {operation} completed successfully - {additionalInfo}";

            Debug.Log($"[{category}] {message}");
        }

        /// <summary>
        /// Log operation failure with error details
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="operation">Name of the failed operation</param>
        /// <param name="errorMessage">Error message or reason for failure</param>
        public static void LogFailure(string category, string operation, string errorMessage)
        {
            Debug.LogError($"[{category}] ❌ {operation} failed: {errorMessage}");
        }

        /// <summary>
        /// Log state transition or status change
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="fromState">Previous state</param>
        /// <param name="toState">New state</param>
        /// <param name="context">Optional context about the transition</param>
        public static void LogStateChange(string category, string fromState, string toState, string context = null)
        {
            string message = string.IsNullOrEmpty(context)
                ? $"State change: {fromState} → {toState}"
                : $"State change: {fromState} → {toState} ({context})";

            Debug.Log($"[{category}] {message}");
        }

        /// <summary>
        /// Log network operation details (requests, responses, timeouts)
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="operation">Network operation name</param>
        /// <param name="details">Operation details (URL, status, etc.)</param>
        /// <param name="isSuccess">Whether the operation succeeded</param>
        public static void LogNetworkOperation(string category, string operation, string details, bool isSuccess = true)
        {
            string prefix = isSuccess ? "🌐" : "⚠️";
            Debug.Log($"[{category}] {prefix} {operation}: {details}");
        }

        /// <summary>
        /// Log timing information for performance monitoring
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="operation">Operation name</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="additionalInfo">Optional additional timing context</param>
        public static void LogTiming(string category, string operation, long durationMs, string additionalInfo = null)
        {
            string message = string.IsNullOrEmpty(additionalInfo)
                ? $"⏱️ {operation} took {durationMs}ms"
                : $"⏱️ {operation} took {durationMs}ms - {additionalInfo}";

            Debug.Log($"[{category}] {message}");
        }

        /// <summary>
        /// Log debug information (only in development builds)
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="message">Debug message</param>
        public static void LogDebug(string category, string message)
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[{category}] [DEBUG] {message}");
            #endif
        }

        /// <summary>
        /// Log ViverseResult operation with automatic success/failure handling
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="result">ViverseResult to log</param>
        /// <param name="operationName">Name of the operation</param>
        public static void LogViverseResult<T>(string category, ViverseResult<T> result, string operationName)
        {
            if (result.IsSuccess)
            {
                LogSuccess(category, operationName, $"Code: {result.RawResult.ReturnCode}");
            }
            else
            {
                LogFailure(category, operationName, result.ErrorMessage ?? "Unknown error");

                // Log additional error details if available
                LogDebug(category, $"Raw result code: {result.RawResult.ReturnCode}, Message: {result.RawResult.Message}");
            }
        }

        /// <summary>
        /// Log step-by-step process for complex operations
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="process">Process name</param>
        /// <param name="stepNumber">Current step number</param>
        /// <param name="stepDescription">Description of the current step</param>
        /// <param name="totalSteps">Total number of steps (optional)</param>
        public static void LogStep(string category, string process, int stepNumber, string stepDescription, int? totalSteps = null)
        {
            string stepInfo = totalSteps.HasValue
                ? $"Step {stepNumber}/{totalSteps.Value}"
                : $"Step {stepNumber}";

            Debug.Log($"[{category}] {process} - {stepInfo}: {stepDescription}");
        }

        /// <summary>
        /// Log important milestones or achievements
        /// </summary>
        /// <param name="category">Logging category (use Categories constants)</param>
        /// <param name="milestone">Milestone description</param>
        public static void LogMilestone(string category, string milestone)
        {
            Debug.Log($"[{category}] 🎉 MILESTONE: {milestone}");
        }
    }
}
