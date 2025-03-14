using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class NodeProcessSafeHandle : SafeHandle
{
	private Process nodeProcess;
	private CancellationTokenSource cancellationTokenSource;

	public NodeProcessSafeHandle() : base(IntPtr.Zero, true)
	{
		cancellationTokenSource = new CancellationTokenSource();
	}

	public override bool IsInvalid => handle == IntPtr.Zero;

	public void Initialize(Process process)
	{
		if (!IsInvalid)
			throw new InvalidOperationException("Handle already initialized");

		this.nodeProcess = process;
		handle = Marshal.AllocHGlobal(1);
	}

	public void Release()
	{
		if (!IsInvalid)
		{
			ReleaseHandle();
		}
	}

	protected override bool ReleaseHandle()
	{
		try
		{
			cancellationTokenSource?.Cancel();

			if (nodeProcess != null && !nodeProcess.HasExited)
			{
				nodeProcess.Kill();
				nodeProcess.WaitForExit();
				nodeProcess.Dispose();
				nodeProcess = null;
			}

			cancellationTokenSource?.Dispose();
			cancellationTokenSource = null;

			if (handle != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(handle);
				handle = IntPtr.Zero;
			}

			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error releasing Node.js process: {ex.Message}");
			return false;
		}
	}

	public CancellationToken Token => cancellationTokenSource.Token;
	public bool IsRunning => nodeProcess != null && !nodeProcess.HasExited;
	public Process Process => nodeProcess;
}
