namespace ViverseWebGLAPI
{
	public static class ReturnCodeHelper
	{
		public static bool IsSuccess(int returnCode)
		{
			return returnCode > 0;
		}

		public static bool IsError(int returnCode)
		{
			return returnCode < 0;
		}

		public static ViverseSDKReturnCode ToReturnCode(int jsReturnCode)
		{
			if (System.Enum.IsDefined(typeof(ViverseSDKReturnCode), jsReturnCode))
			{
				return (ViverseSDKReturnCode)jsReturnCode;
			}

			return ViverseSDKReturnCode.ErrorUnknown;
		}

		public static string GetErrorMessage(ViverseSDKReturnCode code)
		{
			return code switch
			{
				ViverseSDKReturnCode.Success => "Operation completed successfully",
				ViverseSDKReturnCode.NotSet => "Value not set",
				ViverseSDKReturnCode.ErrorInvalidParameter => "Invalid parameter provided",
				ViverseSDKReturnCode.ErrorNotFound => "Requested resource not found",
				ViverseSDKReturnCode.ErrorUnauthorized => "Unauthorized access",
				ViverseSDKReturnCode.ErrorNotSupported => "Operation not supported",
				ViverseSDKReturnCode.ErrorModuleNotLoaded => "Module not loaded",
				ViverseSDKReturnCode.ErrorSdkNotLoaded => "SDK not loaded",
				ViverseSDKReturnCode.ErrorUnknown => "Unknown error occurred",
				ViverseSDKReturnCode.ErrorException => "Exception occurred",
				_ => $"Undefined error code: {code}"
			};
		}
	}
}
