using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

public class NodePathHelper
{

	/// <summary>
	/// Gets the path to Node.js, prioritizing the version installed by NodeInstaller
	/// </summary>
	public static string NodePath
	{
		get
		{
			// First check if NodeInstaller has installed Node.js
			string installerNodePath = NodeInstaller.GetNodePath();
			if (!string.IsNullOrEmpty(installerNodePath) && File.Exists(installerNodePath))
			{
				return installerNodePath;
			}

			// Fall back to Unity's built-in Node.js
			return GetUnityNodePath();
		}
	}

	/// <summary>
	/// Gets the path to npm, prioritizing the version installed by NodeInstaller
	/// </summary>
	public static string NpmPath
	{
		get
		{
			// First check if NodeInstaller has installed npm
			string installerNpmPath = NodeInstaller.GetNpmPath();
			if (!string.IsNullOrEmpty(installerNpmPath) && File.Exists(installerNpmPath))
			{
				return installerNpmPath;
			}

			// Fall back to Unity's built-in npm
			return GetUnityNpmPath();
		}
	}

	/// <summary>
	/// Gets the path to npx, prioritizing the version installed by NodeInstaller
	/// </summary>
	public static string NpxPath
	{
		get
		{
			// First check if NodeInstaller has installed npx
			string installerNpxPath = NodeInstaller.GetNpxPath();
			if (!string.IsNullOrEmpty(installerNpxPath) && File.Exists(installerNpxPath))
			{
				return installerNpxPath;
			}

			// Fall back to Unity's built-in npx
			return GetUnityNpxPath();
		}
	}

	/// <summary>
	/// Gets the Unity Editor installation root directory.
	/// </summary>
	private static string GetUnityEditorRoot()
	{
		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			return Path.GetFullPath(Path.Combine(EditorApplication.applicationPath, "..", ".."));
		}
		else
		{
			return Path.GetFullPath(Path.Combine(EditorApplication.applicationPath));
		}
	}

	/// <summary>
	/// Gets the path to the WebGL Emscripten BuildTools directory.
	/// </summary>
	private static string GetEmscriptenBuildToolsPath()
	{
		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			return Path.GetFullPath(Path.Combine(GetUnityEditorRoot(), "Editor", "Data", "PlaybackEngines",
				"WebGLSupport", "BuildTools", "Emscripten"));
		}
		else
		{
			return Path.GetFullPath(Path.Combine(GetUnityEditorRoot(), "Contents", "Tools", "nodejs", "lib",
				"node_modules", "npm"));
		}
	}

	/// <summary>
	/// Gets the full path of a given executable in the Emscripten BuildTools directory.
	/// </summary>
	private static string NodeDir(string executableName)
	{
		string path = Path.Combine(GetEmscriptenBuildToolsPath(), "node", executableName);

		// Don't assert here as we're using this as a fallback
		if (!File.Exists(path))
		{
			Debug.LogWarning($"Unity Node.js executable not found: {path}");
			return null;
		}

		return path;
	}

	/// <summary>
	/// Gets the path to Node.js from Unity's built-in installation
	/// </summary>
	public static string GetUnityNodePath()
	{
		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			return NodeDir("node.exe");
		}
		else
		{
			return NodeDir("node");
		}
	}

	/// <summary>
	/// Gets the path to npm from Unity's built-in installation
	/// </summary>
	public static string GetUnityNpmPath()
	{
		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			return NodeDir("npm.cmd");
		}
		else
		{
			return NodeDir("npm");
		}
	}

	/// <summary>
	/// Gets the path to npx from Unity's built-in installation
	/// </summary>
	public static string GetUnityNpxPath()
	{
		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			return NodeDir("npm.cmd");
		}
		else
		{
			return NodeDir("npx");
		}
	}

	/// <summary>
	/// Runs a Node.js process and returns a SafeHandle to manage its lifecycle.
	/// </summary>
	/// <param name="scriptPath">Path to the Node.js script to run</param>
	/// <param name="arguments">Arguments to pass to the script</param>
	/// <returns>A NodeProcessSafeHandle that will clean up resources when disposed</returns>
	public static NodeProcessSafeHandle RunNodeWithSafeHandle(string scriptPath, string arguments = "",DataReceivedEventHandler onOutputReceived = null, DataReceivedEventHandler onErrorReceived = null)
	{
		if (!File.Exists(scriptPath))
		{
			Debug.LogError($"Script not found: {scriptPath}");
			return null;
		}

		try
		{
			// Set up output and error handling
			Process process = NodeInstaller.RunNodeScriptAndReturnProcess(scriptPath, arguments,onOutputReceived, onErrorReceived);

			// Create and initialize the safe handle
			var nodeSafeHandle = new NodeProcessSafeHandle();
			nodeSafeHandle.Initialize(process);

			return nodeSafeHandle;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to start Node.js process: {ex.Message}");
			return null;
		}
	}

}
