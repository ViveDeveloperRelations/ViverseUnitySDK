using System;
using System.Runtime.InteropServices;

namespace ViverseWebGLAPI
{
	public class UnmanagedString : IDisposable
	{
		public static string GetValueAndDispose(IntPtr intPtr)
		{
			if (intPtr == IntPtr.Zero)
				return null;
			using var unmanagedString = new UnmanagedString(intPtr);
			return unmanagedString.Value;
		}

		private IntPtr _ptr;
		private bool _disposed;

		public UnmanagedString(IntPtr ptr)
		{
			_ptr = ptr;
		}

		public string Value
		{
			get
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(UnmanagedString));

				if (_ptr == IntPtr.Zero)
					return null;

				return Marshal.PtrToStringUTF8(_ptr);
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				if (_ptr != IntPtr.Zero)
				{
					ViverseCore.FreeString(_ptr);
					_ptr = IntPtr.Zero;
				}

				_disposed = true;
			}
		}

		~UnmanagedString()
		{
			Dispose();
		}
	}
}
