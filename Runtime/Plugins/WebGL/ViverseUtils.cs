using System;
using System.Runtime.InteropServices;

namespace ViverseWebGLAPI
{
	public static class ViverseUtils
	{
		[DllImport("__Internal")]
		private static extern ViverseSDKReturnCode Utils_Free_String(IntPtr ptr);
		
		/// <summary>
		/// Converts an IntPtr to a string (for compatibility with legacy code)
		/// </summary>
		/// <param name="ptr">IntPtr to convert</param>
		/// <returns>String representation or null if invalid</returns>
		public static string IntPtrToString(IntPtr ptr)
		{
			if (ptr == IntPtr.Zero || ptr.ToInt32() < 0)
				return null;
				
			return Marshal.PtrToStringAnsi(ptr);
		}

		public static class Cookie
		{
			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_Cookie_SetItem(string name, string value, int days);

			[DllImport("__Internal")]
			private static extern IntPtr Utils_Cookie_GetItem(string name);

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_Cookie_DeleteItem(string name);

			public static ViverseResult<bool> Set(string name, string value, int days = 30)
			{
				ViverseSDKReturnCode code = Utils_Cookie_SetItem(name, value, days);
				ViverseSDKReturn result = new ViverseSDKReturn
				{
					ReturnCode = (int)code,
					Message = ReturnCodeHelper.GetErrorMessage(code)
				};
				return ViverseResult<bool>.Success(true, result);
			}

			public static ViverseResult<string> Get(string name)
			{
				IntPtr ptr = Utils_Cookie_GetItem(name);
				if (ptr.ToInt32() < 0)
				{
					ViverseSDKReturnCode code = (ViverseSDKReturnCode)ptr.ToInt32();
					ViverseSDKReturn result = new ViverseSDKReturn
					{
						ReturnCode = (int)code,
						Message = ReturnCodeHelper.GetErrorMessage(code)
					};
					return ViverseResult<string>.Failure(result);
				}

				try
				{
					string value = Marshal.PtrToStringAnsi(ptr);
					ViverseSDKReturn result = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.Success,
						Message = "Successfully retrieved cookie value"
					};
					return ViverseResult<string>.Success(value, result);
				}
				finally
				{
					Utils_Free_String(ptr);
				}
			}

			public static ViverseResult<bool> Delete(string name)
			{
				ViverseSDKReturnCode code = Utils_Cookie_DeleteItem(name);
				ViverseSDKReturn result = new ViverseSDKReturn
				{
					ReturnCode = (int)code,
					Message = ReturnCodeHelper.GetErrorMessage(code)
				};
				return code == ViverseSDKReturnCode.Success
					? ViverseResult<bool>.Success(true, result)
					: ViverseResult<bool>.Failure(result);
			}
		}

		public static class SessionStorage
		{
			[DllImport("__Internal")]
			private static extern IntPtr Utils_SessionStorage_GetItem(string key);

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_SessionStorage_SetItem(string key, string value);

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_SessionStorage_RemoveItem(string key);

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_SessionStorage_Clear();

			public static ViverseResult<bool> Set(string key, string value)
			{
				ViverseSDKReturnCode code = Utils_SessionStorage_SetItem(key, value);
				ViverseSDKReturn result = new ViverseSDKReturn
				{
					ReturnCode = (int)code,
					Message = ReturnCodeHelper.GetErrorMessage(code)
				};
				return code == ViverseSDKReturnCode.Success
					? ViverseResult<bool>.Success(true, result)
					: ViverseResult<bool>.Failure(result);
			}

			public static ViverseResult<string> Get(string key)
			{
				IntPtr ptr = Utils_SessionStorage_GetItem(key);
				if (ptr.ToInt32() < 0)
				{
					ViverseSDKReturnCode code = (ViverseSDKReturnCode)ptr.ToInt32();
					ViverseSDKReturn result = new ViverseSDKReturn
					{
						ReturnCode = (int)code,
						Message = ReturnCodeHelper.GetErrorMessage(code)
					};
					return ViverseResult<string>.Failure(result);
				}

				try
				{
					string value = Marshal.PtrToStringAnsi(ptr);
					ViverseSDKReturn result = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.Success,
						Message = "Successfully retrieved session storage value"
					};
					return ViverseResult<string>.Success(value, result);
				}
				finally
				{
					Utils_Free_String(ptr);
				}
			}

			public static ViverseResult<bool> Remove(string key)
			{
				ViverseSDKReturnCode code = Utils_SessionStorage_RemoveItem(key);
				ViverseSDKReturn result = new ViverseSDKReturn
				{
					ReturnCode = (int)code,
					Message = ReturnCodeHelper.GetErrorMessage(code)
				};
				return code == ViverseSDKReturnCode.Success
					? ViverseResult<bool>.Success(true, result)
					: ViverseResult<bool>.Failure(result);
			}

			public static ViverseResult<bool> Clear()
			{
				ViverseSDKReturnCode code = Utils_SessionStorage_Clear();
				ViverseSDKReturn result = new ViverseSDKReturn
				{
					ReturnCode = (int)code,
					Message = ReturnCodeHelper.GetErrorMessage(code)
				};
				return code == ViverseSDKReturnCode.Success
					? ViverseResult<bool>.Success(true, result)
					: ViverseResult<bool>.Failure(result);
			}
		}

		public static class UserAgentHelper
		{
			[DllImport("__Internal")]
			private static extern IntPtr Utils_GetUserAgent();

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_IsMobile();

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_IsMobileIOS();

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_IsHtcMobileVR();

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_IsVRSupported();

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_IsInXR();

			[DllImport("__Internal")]
			private static extern ViverseSDKReturnCode Utils_ToggleVR();

			/// <summary>
			/// Gets the user agent string with return code.
			/// </summary>
			public static ViverseResult<string> GetUserAgent()
			{
				IntPtr ptr = Utils_GetUserAgent();
				if (ptr.ToInt32() < 0)
				{
					ViverseSDKReturnCode code = (ViverseSDKReturnCode)ptr.ToInt32();
					ViverseSDKReturn result = new ViverseSDKReturn
					{
						ReturnCode = (int)code,
						Message = ReturnCodeHelper.GetErrorMessage(code)
					};
					return ViverseResult<string>.Failure(result);
				}

				try
				{
					string value = Marshal.PtrToStringAnsi(ptr);
					ViverseSDKReturn result = new ViverseSDKReturn
					{
						ReturnCode = (int)ViverseSDKReturnCode.Success,
						Message = "Successfully retrieved user agent"
					};
					return ViverseResult<string>.Success(value, result);
				}
				finally
				{
					Utils_Free_String(ptr);
				}
			}

			private static ViverseResult<bool> WrapDeviceCheck(ViverseSDKReturnCode code)
			{
				ViverseSDKReturn result = new ViverseSDKReturn
				{
					ReturnCode = (int)code,
					Message = ReturnCodeHelper.GetErrorMessage(code)
				};
				return code == ViverseSDKReturnCode.Success
					? ViverseResult<bool>.Success(true, result)
					: ViverseResult<bool>.Failure(result);
			}
			/// <summary>
			/// Checks if the current device is a mobile device.
			/// </summary>
			/// <returns>Success if mobile device, ErrorNotFound if not, or other error codes.</returns>
			public static ViverseResult<bool> IsMobile() => WrapDeviceCheck(Utils_IsMobile());

			/// <summary>
			/// Checks if the current device is an iOS device.
			/// </summary>
			/// <returns>Success if iOS device, ErrorNotFound if not, or other error codes.</returns>
			public static ViverseResult<bool> IsMobileIOS() => WrapDeviceCheck(Utils_IsMobileIOS());

			/// <summary>
			/// Checks if the current device is an HTC mobile VR device.
			/// </summary>
			/// <returns>Success if HTC mobile VR device, ErrorNotFound if not, or other error codes.</returns>
			public static ViverseResult<bool> IsHtcMobileVR() => WrapDeviceCheck(Utils_IsHtcMobileVR());

			/// <summary>
			/// Checks if VR is supported on the current device.
			/// </summary>
			/// <returns>Success if VR is supported, ErrorNotSupported if not, or other error codes.</returns>
			public static ViverseResult<bool> IsVRSupported() => WrapDeviceCheck(Utils_IsVRSupported());

			/// <summary>
			/// Checks if the application is currently in XR mode.
			/// </summary>
			/// <returns>Success if in XR mode, ErrorNotFound if not, or other error codes.</returns>
			public static ViverseResult<bool> IsInXR() => WrapDeviceCheck(Utils_IsInXR());

			/// <summary>
			/// Toggles VR mode.
			/// </summary>
			/// <returns>Success if toggle was successful, or appropriate error code if failed.</returns>
			public static ViverseResult<bool> ToggleVR() => WrapDeviceCheck(Utils_ToggleVR());

			public static class DeviceChecks
			{
				public static bool IsMobileDevice() => IsMobile().IsSuccess;
				public static bool IsIOSDevice() => IsMobileIOS().IsSuccess;
				public static bool IsHtcVRDevice() => IsHtcMobileVR().IsSuccess;
				public static bool SupportsVR() => IsVRSupported().IsSuccess;
				public static bool IsInXRMode() => IsInXR().IsSuccess;
			}
		}
	}
}
