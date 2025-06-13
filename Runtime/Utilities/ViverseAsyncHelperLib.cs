using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace ViverseWebGLAPI
{
    /// <summary>
    /// Centralized utility library for JavaScript Promise/Unity Task interop patterns.
    /// Consolidates all async helper methods and callback safety utilities used across SDK services.
    /// </summary>
    public static class ViverseAsyncHelperLib
    {
        private static Dictionary<int, TaskCompletionSource<ViverseSDKReturn>> s_pendingTasks = new();
        private static int s_taskId = 0;
        private static readonly object s_lockObject = new object();

        /// <summary>
        /// Generic async wrapper that eliminates need for individual wrapper functions.
        /// Converts JavaScript Promise-based native functions to Unity Task pattern.
        /// </summary>
        /// <param name="nativeFunction">JavaScript function that takes taskId and callback</param>
        /// <returns>Task that completes when JavaScript Promise resolves</returns>
        public static Task<ViverseSDKReturn> WrapAsyncWithPayload(Action<int, Action<string>> nativeFunction)
        {
            if (nativeFunction == null)
                throw new ArgumentNullException(nameof(nativeFunction));

            TaskCompletionSource<ViverseSDKReturn> tcs = new();
            
            lock (s_lockObject)
            {
                s_taskId++;
                s_pendingTasks.Add(s_taskId, tcs);
            }

            try
            {
                nativeFunction(s_taskId, SafeCallback);
                return tcs.Task;
            }
            catch (Exception e)
            {
                ViverseLogger.LogException(ViverseLogger.Categories.ASYNC_HELPER, e, "Exception calling native function");
                
                lock (s_lockObject)
                {
                    s_pendingTasks.Remove(s_taskId);
                }
                
                tcs.SetResult(new ViverseSDKReturn
                {
                    TaskId = s_taskId,
                    ReturnCode = (int)ViverseSDKReturnCode.ErrorException,
                    Message = e.Message
                });
                
                return tcs.Task;
            }
        }

        /// <summary>
        /// Generic async wrapper for functions with single parameter.
        /// </summary>
        public static Task<ViverseSDKReturn> WrapAsyncWithPayload<T>(Action<T, int, Action<string>> nativeFunction, T parameter)
        {
            return WrapAsyncWithPayload((taskId, callback) => nativeFunction(parameter, taskId, callback));
        }

        /// <summary>
        /// Generic async wrapper for functions with two parameters.
        /// </summary>
        public static Task<ViverseSDKReturn> WrapAsyncWithPayload<T1, T2>(Action<T1, T2, int, Action<string>> nativeFunction, T1 param1, T2 param2)
        {
            return WrapAsyncWithPayload((taskId, callback) => nativeFunction(param1, param2, taskId, callback));
        }

        /// <summary>
        /// Generic async wrapper for functions with three parameters.
        /// </summary>
        public static Task<ViverseSDKReturn> WrapAsyncWithPayload<T1, T2, T3>(Action<T1, T2, T3, int, Action<string>> nativeFunction, T1 param1, T2 param2, T3 param3)
        {
            return WrapAsyncWithPayload((taskId, callback) => nativeFunction(param1, param2, param3, taskId, callback));
        }

        /// <summary>
        /// Generic async wrapper for functions with four parameters.
        /// </summary>
        public static Task<ViverseSDKReturn> WrapAsyncWithPayload<T1, T2, T3, T4>(Action<T1, T2, T3, T4, int, Action<string>> nativeFunction, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            return WrapAsyncWithPayload((taskId, callback) => nativeFunction(param1, param2, param3, param4, taskId, callback));
        }

        /// <summary>
        /// Safe callback handler with comprehensive error handling and memory management.
        /// MonoPInvokeCallback for JavaScript->C# callback marshaling safety.
        /// </summary>
        /// <param name="result">JSON formatted ViverseSDKReturn from JavaScript</param>
        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void SafeCallback(string result)
        {
            if (string.IsNullOrEmpty(result))
            {
                ViverseLogger.LogError(ViverseLogger.Categories.ASYNC_HELPER, "Received null or empty result from JavaScript - this indicates a bug");
                return;
            }

            try
            {
                ViverseSDKReturn sdkReturn = JsonUtility.FromJson<ViverseSDKReturn>(result);
                int taskId = sdkReturn.TaskId;

                TaskCompletionSource<ViverseSDKReturn> tcs = null;
                
                lock (s_lockObject)
                {
                    if (!s_pendingTasks.TryGetValue(taskId, out tcs))
                    {
                        ViverseLogger.LogError(ViverseLogger.Categories.ASYNC_HELPER, "TaskId {0} not found in pending tasks - this indicates a bug. JSON: {1}", taskId, result);
                        return;
                    }
                    
                    s_pendingTasks.Remove(taskId);
                }

                tcs.SetResult(sdkReturn);
            }
            catch (Exception e)
            {
                ViverseLogger.LogError(ViverseLogger.Categories.ASYNC_HELPER, "Failed to parse JavaScript callback result. JSON: {0}", result);
                ViverseLogger.LogException(ViverseLogger.Categories.ASYNC_HELPER, e);
            }
        }

        /// <summary>
        /// Helper to create strongly-typed ViverseResult from raw SDK return.
        /// Includes comprehensive error logging and recovery guidance.
        /// </summary>
        public static async Task<ViverseResult<T>> ExecuteWithResult<T>(Func<Task<ViverseSDKReturn>> operation, Func<ViverseSDKReturn, T> parsePayload, string operationName)
        {
            try
            {
                var sdkReturn = await operation();
                
                if (sdkReturn.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
                {
                    var failureResult = ViverseResult<T>.Failure(sdkReturn);
                    failureResult.LogError(operationName);
                    return failureResult;
                }

                try
                {
                    T data = parsePayload(sdkReturn);
                    return ViverseResult<T>.Success(data, sdkReturn);
                }
                catch (Exception parseException)
                {
                    ViverseLogger.LogException(ViverseLogger.Categories.ASYNC_HELPER, parseException, $"Failed to parse {operationName} payload");
                    return ViverseResult<T>.Failure(sdkReturn);
                }
            }
            catch (Exception e)
            {
                ViverseLogger.LogException(ViverseLogger.Categories.ASYNC_HELPER, e, $"Exception during {operationName}");
                return ViverseResult<T>.Failure(ViverseSDKReturnCode.ErrorException, $"Exception: {e.Message}");
            }
        }

        /// <summary>
        /// Helper to create ViverseResult<bool> for simple success/failure operations.
        /// </summary>
        public static async Task<ViverseResult<bool>> ExecuteWithBoolResult(Func<Task<ViverseSDKReturn>> operation, string operationName)
        {
            return await ExecuteWithResult(
                operation,
                sdkReturn => true, // Success is indicated by reaching this point
                operationName
            );
        }

        /// <summary>
        /// Helper to parse JSON payload safely with error handling.
        /// </summary>
        public static T ParseJsonPayload<T>(ViverseSDKReturn sdkReturn) where T : class
        {
            if (string.IsNullOrEmpty(sdkReturn.Payload))
                return null;

            return JsonUtility.FromJson<T>(sdkReturn.Payload);
        }

        /// <summary>
        /// Helper to parse JSON payload with array wrapper handling for Unity JsonUtility limitations.
        /// </summary>
        public static T[] ParseJsonArrayPayload<T>(ViverseSDKReturn sdkReturn, string arrayFieldName = null) where T : class
        {
            if (string.IsNullOrEmpty(sdkReturn.Payload))
                return new T[0];

            string payload = sdkReturn.Payload;
            
            // Handle Unity JsonUtility limitation - wrap top-level arrays
            if (!payload.TrimStart().StartsWith("{"))
            {
                string fieldName = arrayFieldName ?? typeof(T).Name.ToLowerInvariant() + "s";
                payload = $"{{\"{fieldName}\":{payload}}}";
            }

            try
            {
                // Use generic wrapper class for parsing
                var wrapperType = typeof(JsonArrayWrapper<>).MakeGenericType(typeof(T));
                var wrapper = JsonUtility.FromJson(payload, wrapperType);
                var arrayField = wrapperType.GetField("items");
                return (T[])arrayField.GetValue(wrapper) ?? new T[0];
            }
            catch (Exception e)
            {
                ViverseLogger.LogException(ViverseLogger.Categories.ASYNC_HELPER, e, "Failed to parse array payload");
                return new T[0];
            }
        }

        /// <summary>
        /// Get current pending task count for debugging and monitoring.
        /// </summary>
        public static int GetPendingTaskCount()
        {
            lock (s_lockObject)
            {
                return s_pendingTasks.Count;
            }
        }

        /// <summary>
        /// Clear all pending tasks (emergency cleanup).
        /// Should only be used during shutdown or error recovery.
        /// </summary>
        public static void ClearPendingTasks()
        {
            lock (s_lockObject)
            {
                foreach (var tcs in s_pendingTasks.Values)
                {
                    try
                    {
                        tcs.SetCanceled();
                    }
                    catch (Exception e)
                    {
                        ViverseLogger.LogWarning(ViverseLogger.Categories.ASYNC_HELPER, "Exception while canceling pending task: {0}", e.Message);
                    }
                }
                
                s_pendingTasks.Clear();
                ViverseLogger.LogInfo(ViverseLogger.Categories.ASYNC_HELPER, "Cleared all pending tasks during cleanup");
            }
        }
    }

    /// <summary>
    /// Generic wrapper class for JSON array parsing with Unity JsonUtility.
    /// </summary>
    [Serializable]
    internal class JsonArrayWrapper<T>
    {
        public T[] items;
    }
}