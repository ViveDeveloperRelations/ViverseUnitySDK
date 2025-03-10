using System;
using System.Threading.Tasks;

namespace ViverseWebGLAPI
{
	public class ViverseResult<T>
	{
		public T Data { get; }
		public ViverseSDKReturn RawResult { get; }
		public bool IsSuccess => RawResult.ViverseSDKReturnCode == ViverseSDKReturnCode.Success;

		public string ErrorMessage =>
			!IsSuccess ? ReturnCodeHelper.GetErrorMessage(RawResult.ViverseSDKReturnCode) : null;

		public ViverseResult(T data, ViverseSDKReturn rawResult)
		{
			Data = data;
			RawResult = rawResult;
		}

		// Helper methods for creating results
		public static ViverseResult<T> Success(T data, ViverseSDKReturn rawResult)
			=> new ViverseResult<T>(data, rawResult);

		public static ViverseResult<T> Failure(ViverseSDKReturn rawResult)
			=> new ViverseResult<T>(default, rawResult);

		// Extension method to help with error handling patterns
		public ViverseResult<TResult> Map<TResult>(Func<T, TResult> mapper)
		{
			if (!IsSuccess) return ViverseResult<TResult>.Failure(RawResult);
			return ViverseResult<TResult>.Success(mapper(Data), RawResult);
		}
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

		// Generic errors (deep negative values)
		ErrorUnknown = -99,
		ErrorException = -100
	}
}
