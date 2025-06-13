using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ViverseWebGLAPI
{
	public class ViverseResult<T>
	{
		public T Data { get; }
		public T Value => Data; // Alias for compatibility
		public ViverseSDKReturn RawResult { get; private set; }
		public bool IsSuccess => RawResult.ViverseSDKReturnCode == ViverseSDKReturnCode.Success;

		// Convenience properties for easier access
		public string TaskId
		{
			get => RawResult.TaskId.ToString();
			set
			{
				var result = RawResult;
				result.TaskId = int.TryParse(value, out int id) ? id : 0;
				RawResult = result;
			}
		}

		public string Payload
		{
			get => RawResult.Payload;
			set
			{
				var result = RawResult;
				result.Payload = value;
				RawResult = result;
			}
		}

		public ViverseSDKReturnCode ViverseSDKReturnCode => RawResult.ViverseSDKReturnCode;
		public string Message => RawResult.Message ?? string.Empty;

		public string ErrorMessage =>
			!IsSuccess ? ReturnCodeHelper.GetErrorMessage(RawResult.ViverseSDKReturnCode) : null;

		/// <summary>
		/// Safe access to payload with null protection
		/// </summary>
		public string SafePayload => RawResult.Payload ?? string.Empty;

		/// <summary>
		/// Safe access to message with null protection
		/// </summary>
		public string SafeMessage => RawResult.Message ?? string.Empty;

		/// <summary>
		/// Check if payload contains specific text (null-safe)
		/// </summary>
		public bool PayloadContains(string text)
		{
			return !string.IsNullOrEmpty(RawResult.Payload) && RawResult.Payload.Contains(text);
		}

		/// <summary>
		/// Check if this error was caused by null/undefined SDK return (using proper error code)
		/// </summary>
		public bool IsNullUndefinedError => ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorSdkReturnedNull;

		/// <summary>
		/// Get error details from payload (null-safe)
		/// </summary>
		public string GetErrorDetails()
		{
			if (string.IsNullOrEmpty(RawResult.Payload))
				return "No error details available";

			try
			{
				// Try to parse as JSON and extract error message
				var payload = JsonUtility.FromJson<ErrorPayload>(RawResult.Payload);
				return payload?.error ?? RawResult.Payload;
			}
			catch
			{
				// If not JSON, return raw payload
				return RawResult.Payload;
			}
		}

		public ViverseResult(T data, ViverseSDKReturn rawResult)
		{
			Data = data;
			RawResult = rawResult;
		}

		// Helper methods for creating results
		public static ViverseResult<T> Success(T data, ViverseSDKReturn rawResult)
			=> new ViverseResult<T>(data, rawResult);

		public static ViverseResult<T> Success(T data)
		{
			var rawResult = new ViverseSDKReturn
			{
				TaskId = 0,
				ReturnCode = (int)ViverseSDKReturnCode.Success,
				Message = "Success",
				Payload = ""
			};
			return new ViverseResult<T>(data, rawResult);
		}

		public static ViverseResult<T> Failure(ViverseSDKReturn rawResult)
			=> new ViverseResult<T>(default, rawResult);

		public static ViverseResult<T> Failure(ViverseSDKReturnCode errorCode, string message)
		{
			var rawResult = new ViverseSDKReturn
			{
				TaskId = 0,
				ReturnCode = (int)errorCode,
				Message = message ?? "",
				Payload = ""
			};
			return new ViverseResult<T>(default, rawResult);
		}

		// Extension method to help with error handling patterns
		public ViverseResult<TResult> Map<TResult>(Func<T, TResult> mapper)
		{
			if (!IsSuccess) return ViverseResult<TResult>.Failure(RawResult);
			return ViverseResult<TResult>.Success(mapper(Data), RawResult);
		}
	}

	/// <summary>
	/// Helper class for parsing error payload JSON (null-safe)
	/// </summary>
	[Serializable]
	internal class ErrorPayload
	{
		public string error;
		public bool nullUndefinedDetected;
		public bool timeout;
	}

	//raw results from sdk, not to be used directly - maybe make internal or something
	[Serializable]
	public struct ViverseSDKReturn
	{
		public int TaskId;
		public int ReturnCode; // For example: 0 = success, non-zero = error.
		public ViverseSDKReturnCode ViverseSDKReturnCode => (ViverseSDKReturnCode)ReturnCode;
		public string Message;
		public string Payload;
	}

	public enum ViverseSDKReturnCode
	{
		// Success codes (positive values)
		Success = 1,

		// Neutral codes
		NotSet = 0,

		// Error codes (negative values)
		ErrorInvalidParameter = -1,
		ErrorNotFound = -2,
		ErrorUnauthorized = -3,
		ErrorNotSupported = -4,
		ErrorModuleNotLoaded = -5,
		ErrorSdkNotLoaded = -6,
		ErrorSdkNotInitialized = -7,
		ErrorUnityNotInitialized = -8,
		ErrorInvalidState = -9,
		ErrorParseJson = -10,

		// Specific SDK error codes (negative values, -11 to -50 range)
		ErrorSdkReturnedNull = -11,          // SDK returned null/undefined - suggests client reinitialization
		ErrorAuthenticationTimeout = -12,    // Authentication operation timed out - suggests client reinitialization
		ErrorClientCorrupted = -13,         // Client state appears corrupted - suggests client reinitialization
		ErrorNetworkTimeout = -14,          // Network timeout occurred - may be recoverable with retry
		ErrorOAuthCallbackFailed = -15,     // OAuth callback processing failed - suggests retry OAuth flow

		// Generic errors (deep negative values)
		ErrorUnknown = -99,
		ErrorException = -100
	}

	/// <summary>
	/// Extension methods for safe logging and error handling with comprehensive null protection
	/// </summary>
	public static class ViverseResultExtensions
	{
		/// <summary>
		/// Safe comprehensive error logging for raw ViverseSDKReturn with null protection and formatted output
		/// </summary>
		public static void LogError(this ViverseSDKReturn result, string operationName)
		{
			// Safe operation name
			string safeOperationName = operationName ?? "Unknown Operation";
			
			// Don't log if operation succeeded
			if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success) 
			{
				UnityEngine.Debug.Log($"✅ [{safeOperationName}] SUCCESS");
				return;
			}

			// Main error message with comprehensive formatting
			string errorMessage = GetSafeErrorMessageForRaw(result);
			UnityEngine.Debug.LogError($"❌ [{safeOperationName}] FAILED: {errorMessage}");
			
			// Return code information
			string returnCodeName = System.Enum.GetName(typeof(ViverseSDKReturnCode), result.ViverseSDKReturnCode) ?? "Unknown";
			UnityEngine.Debug.LogError($"   Return Code: {result.ReturnCode} ({returnCodeName})");
			
			// Message details (safe)
			if (!string.IsNullOrEmpty(result.Message))
			{
				UnityEngine.Debug.LogError($"   Message: {result.Message}");
			}
			
			// Operation context
			UnityEngine.Debug.LogError($"   Operation: {safeOperationName}");
			
			// Recoverable error detection with guidance
			bool isRecoverable = IsRecoverableError(result);
			UnityEngine.Debug.LogError($"   Recoverable: {isRecoverable}");
			
			if (isRecoverable)
			{
				string guidance = GetRecoveryGuidance(result.ViverseSDKReturnCode);
				if (!string.IsNullOrEmpty(guidance))
				{
					UnityEngine.Debug.LogWarning($"   Guidance: {guidance}");
				}
			}
			
			// Payload information (if available and relevant)
			if (!string.IsNullOrEmpty(result.Payload) && result.Payload.Length < 500)
			{
				try
				{
					string errorDetails = GetErrorDetails(result);
					if (!string.IsNullOrEmpty(errorDetails) && errorDetails != "No error details available")
					{
						UnityEngine.Debug.LogError($"   Details: {errorDetails}");
					}
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogWarning($"   Note: Could not parse error details ({e.GetType().Name})");
				}
			}
			
			// Raw result for debugging (truncated if too long)
			try
			{
				string rawResultJson = JsonUtility.ToJson(result);
				if (rawResultJson.Length > 200)
				{
					rawResultJson = rawResultJson.Substring(0, 200) + "...";
				}
				UnityEngine.Debug.LogError($"   Raw Result: {rawResultJson}");
			}
			catch
			{
				UnityEngine.Debug.LogError($"   Raw Result: TaskId={result.TaskId}, ReturnCode={result.ReturnCode}");
			}
		}

		/// <summary>
		/// Safe warning logging for raw ViverseSDKReturn for non-critical issues
		/// </summary>
		public static void LogWarning(this ViverseSDKReturn result, string operationName)
		{
			// Safe operation name
			string safeOperationName = operationName ?? "Unknown Operation";
			
			// Success case
			if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success) 
			{
				UnityEngine.Debug.Log($"✅ [{safeOperationName}] SUCCESS");
				return;
			}

			// Main warning message
			string errorMessage = GetSafeErrorMessageForRaw(result);
			UnityEngine.Debug.LogWarning($"⚠️ [{safeOperationName}] WARNING: {errorMessage}");
			
			// Basic error information
			string returnCodeName = System.Enum.GetName(typeof(ViverseSDKReturnCode), result.ViverseSDKReturnCode) ?? "Unknown";
			UnityEngine.Debug.LogWarning($"   Return Code: {result.ReturnCode} ({returnCodeName})");
			
			// Recovery guidance if available
			if (IsRecoverableError(result))
			{
				string guidance = GetRecoveryGuidance(result.ViverseSDKReturnCode);
				if (!string.IsNullOrEmpty(guidance))
				{
					UnityEngine.Debug.LogWarning($"   Suggestion: {guidance}");
				}
			}
		}

		/// <summary>
		/// Safe detailed logging for raw ViverseSDKReturn for debugging with comprehensive information
		/// </summary>
		public static void LogDetailed(this ViverseSDKReturn result, string operationName)
		{
			string safeOperationName = operationName ?? "Unknown Operation";
			var status = result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success ? "SUCCESS" : "FAILED";
			var statusIcon = result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success ? "✅" : "❌";
			
			UnityEngine.Debug.Log($"{statusIcon} [{safeOperationName}] {status}");
			
			// Return code details
			string returnCodeName = System.Enum.GetName(typeof(ViverseSDKReturnCode), result.ViverseSDKReturnCode) ?? "Unknown";
			UnityEngine.Debug.Log($"   Return Code: {result.ReturnCode} ({returnCodeName})");
			
			// Task ID
			UnityEngine.Debug.Log($"   Task ID: {result.TaskId}");
			
			// Message information (safe)
			if (!string.IsNullOrEmpty(result.Message))
			{
				UnityEngine.Debug.Log($"   Message: {result.Message}");
			}
			else
			{
				UnityEngine.Debug.Log($"   Message: (empty)");
			}

			// Payload information (safe, with size limits)
			if (!string.IsNullOrEmpty(result.Payload))
			{
				if (result.Payload.Length > 300)
				{
					UnityEngine.Debug.Log($"   Payload: {result.Payload.Substring(0, 300)}... ({result.Payload.Length} chars total)");
				}
				else
				{
					UnityEngine.Debug.Log($"   Payload: {result.Payload}");
				}
			}
			else
			{
				UnityEngine.Debug.Log($"   Payload: (empty)");
			}

			// Error-specific information
			if (result.ViverseSDKReturnCode != ViverseSDKReturnCode.Success)
			{
				UnityEngine.Debug.Log($"   Recoverable: {IsRecoverableError(result)}");
				
				if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorSdkReturnedNull)
				{
					UnityEngine.Debug.LogWarning($"   ⚠️ Null/undefined error detected - SDK state may be corrupted");
				}
				else if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorAuthenticationTimeout)
				{
					UnityEngine.Debug.LogWarning($"   ⚠️ Authentication timeout - client may need reinitialization");
				}
				else if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorClientCorrupted)
				{
					UnityEngine.Debug.LogWarning($"   ⚠️ Client corruption detected - reinitialization recommended");
				}
				else if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorNetworkTimeout)
				{
					UnityEngine.Debug.LogWarning($"   ⚠️ Network timeout - check connectivity and retry");
				}
			}
		}

		/// <summary>
		/// Check if this is a recoverable error for raw ViverseSDKReturn that might benefit from retry or reinitialization
		/// </summary>
		public static bool IsRecoverableError(this ViverseSDKReturn result)
		{
			if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.Success) return false;

			return result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorSdkReturnedNull ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorAuthenticationTimeout ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorClientCorrupted ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorNetworkTimeout ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorOAuthCallbackFailed ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorModuleNotLoaded ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorUnauthorized;
		}

		/// <summary>
		/// Get error details from payload for raw ViverseSDKReturn (null-safe)
		/// </summary>
		public static string GetErrorDetails(this ViverseSDKReturn result)
		{
			if (string.IsNullOrEmpty(result.Payload))
				return "No error details available";

			try
			{
				// Try to parse as JSON and extract error message
				var payload = JsonUtility.FromJson<ErrorPayload>(result.Payload);
				return payload?.error ?? result.Payload;
			}
			catch
			{
				// If not JSON, return raw payload
				return result.Payload;
			}
		}
		/// <summary>
		/// Safe comprehensive error logging with null protection and formatted output
		/// </summary>
		public static void LogError<T>(this ViverseResult<T> result, string operationName)
		{
			// Null safety check
			if (result == null)
			{
				UnityEngine.Debug.LogError($"[{operationName ?? "Unknown Operation"}] CRITICAL: ViverseResult is null");
				return;
			}

			// Don't log if operation succeeded
			if (result.IsSuccess) 
			{
				UnityEngine.Debug.Log($"✅ [{operationName ?? "Unknown Operation"}] SUCCESS");
				return;
			}

			// Safe operation name
			string safeOperationName = operationName ?? "Unknown Operation";
			
			// Main error message with comprehensive formatting
			string errorMessage = GetSafeErrorMessage(result);
			UnityEngine.Debug.LogError($"❌ [{safeOperationName}] FAILED: {errorMessage}");
			
			// Return code information
			string returnCodeName = System.Enum.GetName(typeof(ViverseSDKReturnCode), result.ViverseSDKReturnCode) ?? "Unknown";
			UnityEngine.Debug.LogError($"   Return Code: {result.RawResult.ReturnCode} ({returnCodeName})");
			
			// Message details (safe)
			if (!string.IsNullOrEmpty(result.SafeMessage))
			{
				UnityEngine.Debug.LogError($"   Message: {result.SafeMessage}");
			}
			
			// Operation context
			UnityEngine.Debug.LogError($"   Operation: {safeOperationName}");
			
			// Recoverable error detection with guidance
			bool isRecoverable = result.IsRecoverableError();
			UnityEngine.Debug.LogError($"   Recoverable: {isRecoverable}");
			
			if (isRecoverable)
			{
				string guidance = GetRecoveryGuidance(result.ViverseSDKReturnCode);
				if (!string.IsNullOrEmpty(guidance))
				{
					UnityEngine.Debug.LogWarning($"   Guidance: {guidance}");
				}
			}
			
			// Payload information (if available and relevant)
			if (!string.IsNullOrEmpty(result.SafePayload) && result.SafePayload.Length < 500)
			{
				try
				{
					string errorDetails = result.GetErrorDetails();
					if (!string.IsNullOrEmpty(errorDetails) && errorDetails != "No error details available")
					{
						UnityEngine.Debug.LogError($"   Details: {errorDetails}");
					}
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogWarning($"   Note: Could not parse error details ({e.GetType().Name})");
				}
			}
			
			// Raw result for debugging (truncated if too long)
			try
			{
				string rawResultJson = JsonUtility.ToJson(result.RawResult);
				if (rawResultJson.Length > 200)
				{
					rawResultJson = rawResultJson.Substring(0, 200) + "...";
				}
				UnityEngine.Debug.LogError($"   Raw Result: {rawResultJson}");
			}
			catch
			{
				UnityEngine.Debug.LogError($"   Raw Result: TaskId={result.RawResult.TaskId}, ReturnCode={result.RawResult.ReturnCode}");
			}
		}

		/// <summary>
		/// Safe warning logging for non-critical issues
		/// </summary>
		public static void LogWarning<T>(this ViverseResult<T> result, string operationName)
		{
			// Null safety check
			if (result == null)
			{
				UnityEngine.Debug.LogWarning($"[{operationName ?? "Unknown Operation"}] WARNING: ViverseResult is null");
				return;
			}

			// Success case
			if (result.IsSuccess) 
			{
				UnityEngine.Debug.Log($"✅ [{operationName ?? "Unknown Operation"}] SUCCESS");
				return;
			}

			// Safe operation name
			string safeOperationName = operationName ?? "Unknown Operation";
			
			// Main warning message
			string errorMessage = GetSafeErrorMessage(result);
			UnityEngine.Debug.LogWarning($"⚠️ [{safeOperationName}] WARNING: {errorMessage}");
			
			// Basic error information
			string returnCodeName = System.Enum.GetName(typeof(ViverseSDKReturnCode), result.ViverseSDKReturnCode) ?? "Unknown";
			UnityEngine.Debug.LogWarning($"   Return Code: {result.RawResult.ReturnCode} ({returnCodeName})");
			
			// Recovery guidance if available
			if (result.IsRecoverableError())
			{
				string guidance = GetRecoveryGuidance(result.ViverseSDKReturnCode);
				if (!string.IsNullOrEmpty(guidance))
				{
					UnityEngine.Debug.LogWarning($"   Suggestion: {guidance}");
				}
			}
		}

		/// <summary>
		/// Safe detailed logging for debugging with comprehensive information
		/// </summary>
		public static void LogDetailed<T>(this ViverseResult<T> result, string operationName)
		{
			// Null safety check
			if (result == null)
			{
				UnityEngine.Debug.LogError($"[{operationName ?? "Unknown Operation"}] DETAILED: ViverseResult is null");
				return;
			}

			string safeOperationName = operationName ?? "Unknown Operation";
			var status = result.IsSuccess ? "SUCCESS" : "FAILED";
			var statusIcon = result.IsSuccess ? "✅" : "❌";
			
			UnityEngine.Debug.Log($"{statusIcon} [{safeOperationName}] {status}");
			
			// Return code details
			string returnCodeName = System.Enum.GetName(typeof(ViverseSDKReturnCode), result.ViverseSDKReturnCode) ?? "Unknown";
			UnityEngine.Debug.Log($"   Return Code: {result.RawResult.ReturnCode} ({returnCodeName})");
			
			// Task ID
			UnityEngine.Debug.Log($"   Task ID: {result.TaskId}");
			
			// Message information (safe)
			if (!string.IsNullOrEmpty(result.SafeMessage))
			{
				UnityEngine.Debug.Log($"   Message: {result.SafeMessage}");
			}
			else
			{
				UnityEngine.Debug.Log($"   Message: (empty)");
			}

			// Payload information (safe, with size limits)
			if (!string.IsNullOrEmpty(result.SafePayload))
			{
				if (result.SafePayload.Length > 300)
				{
					UnityEngine.Debug.Log($"   Payload: {result.SafePayload.Substring(0, 300)}... ({result.SafePayload.Length} chars total)");
				}
				else
				{
					UnityEngine.Debug.Log($"   Payload: {result.SafePayload}");
				}
			}
			else
			{
				UnityEngine.Debug.Log($"   Payload: (empty)");
			}

			// Data information (if available)
			if (result.Data != null)
			{
				string dataType = result.Data.GetType().Name;
				UnityEngine.Debug.Log($"   Data Type: {dataType}");
				
				try
				{
					string dataJson = JsonUtility.ToJson(result.Data);
					if (dataJson.Length > 200)
					{
						dataJson = dataJson.Substring(0, 200) + "...";
					}
					UnityEngine.Debug.Log($"   Data: {dataJson}");
				}
				catch
				{
					UnityEngine.Debug.Log($"   Data: (not serializable)");
				}
			}
			else
			{
				UnityEngine.Debug.Log($"   Data: null");
			}

			// Error-specific information
			if (!result.IsSuccess)
			{
				UnityEngine.Debug.Log($"   Recoverable: {result.IsRecoverableError()}");
				
				if (result.IsNullUndefinedError)
				{
					UnityEngine.Debug.LogWarning($"   ⚠️ Null/undefined error detected - SDK state may be corrupted");
				}
				else if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorAuthenticationTimeout)
				{
					UnityEngine.Debug.LogWarning($"   ⚠️ Authentication timeout - client may need reinitialization");
				}
				else if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorClientCorrupted)
				{
					UnityEngine.Debug.LogWarning($"   ⚠️ Client corruption detected - reinitialization recommended");
				}
				else if (result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorNetworkTimeout)
				{
					UnityEngine.Debug.LogWarning($"   ⚠️ Network timeout - check connectivity and retry");
				}
			}
		}

		/// <summary>
		/// Check if this is a recoverable error that might benefit from retry or reinitialization
		/// </summary>
		public static bool IsRecoverableError<T>(this ViverseResult<T> result)
		{
			if (result?.IsSuccess != false) return false;

			return result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorSdkReturnedNull ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorAuthenticationTimeout ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorClientCorrupted ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorNetworkTimeout ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorOAuthCallbackFailed ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorModuleNotLoaded ||
			       result.ViverseSDKReturnCode == ViverseSDKReturnCode.ErrorUnauthorized;
		}

		/// <summary>
		/// Get a safe error message with null protection for raw ViverseSDKReturn
		/// </summary>
		private static string GetSafeErrorMessageForRaw(ViverseSDKReturn result)
		{
			// Try to get a meaningful error message
			if (!string.IsNullOrEmpty(result.Message))
			{
				return result.Message;
			}
			
			// Fallback to return code helper
			try
			{
				return ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode) ?? "Unknown error";
			}
			catch
			{
				return $"Error code {result.ReturnCode}";
			}
		}

		/// <summary>
		/// Get a safe error message with null protection for ViverseResult<T>
		/// </summary>
		private static string GetSafeErrorMessage<T>(ViverseResult<T> result)
		{
			if (result == null) return "ViverseResult is null";
			
			// Try to get a meaningful error message
			if (!string.IsNullOrEmpty(result.SafeMessage))
			{
				return result.SafeMessage;
			}
			
			// Fallback to return code helper
			try
			{
				return ReturnCodeHelper.GetErrorMessage(result.ViverseSDKReturnCode) ?? "Unknown error";
			}
			catch
			{
				return $"Error code {result.RawResult.ReturnCode}";
			}
		}

		/// <summary>
		/// Get recovery guidance for specific error codes
		/// </summary>
		private static string GetRecoveryGuidance(ViverseSDKReturnCode errorCode)
		{
			switch (errorCode)
			{
				case ViverseSDKReturnCode.ErrorSdkReturnedNull:
					return "Consider calling ForceReinitializeClient() to reset SDK state";
				case ViverseSDKReturnCode.ErrorAuthenticationTimeout:
					return "Try refreshing authentication or reinitializing the client";
				case ViverseSDKReturnCode.ErrorClientCorrupted:
					return "Client state corrupted - call ForceReinitializeClient()";
				case ViverseSDKReturnCode.ErrorNetworkTimeout:
					return "Check network connectivity and retry operation";
				case ViverseSDKReturnCode.ErrorOAuthCallbackFailed:
					return "Retry OAuth flow or check callback URL configuration";
				case ViverseSDKReturnCode.ErrorUnauthorized:
					return "Check authentication credentials and re-authenticate if needed";
				case ViverseSDKReturnCode.ErrorModuleNotLoaded:
					return "Ensure SDK modules are properly initialized";
				case ViverseSDKReturnCode.ErrorInvalidParameter:
					return "Check input parameters for null or invalid values";
				case ViverseSDKReturnCode.ErrorInvalidState:
					return "Check that required initialization steps have been completed";
				default:
					return string.Empty;
			}
		}
	}
}
