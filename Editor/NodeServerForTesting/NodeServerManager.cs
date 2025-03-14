using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Net.Sockets;


public class NodeServerManager
{
	private static string ToolsPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "tools"));
	private static string LibsPath => Path.Combine(ToolsPath, "libs");

	private static string ServerScriptName = "server.js";
	private static string CertPath => Path.Combine(ToolsPath, "create.viverse.com.pem");
	private static string KeyPath => Path.Combine(ToolsPath, "create.viverse.com-key.pem");

	private static string NodePath => NodePathHelper.NodePath;
	private static string NpmPath => NodePathHelper.NpmPath;


	//[MenuItem("Tools/Copy Server Script")]
	public static void CopyServerScript()
	{
		string scriptSource = FindScriptPath(ServerScriptName);

		if (string.IsNullOrEmpty(scriptSource))
		{
			Debug.LogError($"server.js not found in Unity project.");
			return;
		}

		if (!Directory.Exists(ToolsPath))
		{
			Directory.CreateDirectory(ToolsPath);
		}

		string destination = Path.Combine(ToolsPath, ServerScriptName);
		File.Copy(scriptSource, destination, true);
		Debug.Log($"Copied {ServerScriptName} to {ToolsPath}");
	}

	//[MenuItem("Tools/Install Node Modules")]
	public static void InstallNodeModules()
	{
		if (!Directory.Exists(LibsPath))
		{
			Directory.CreateDirectory(LibsPath);
		}

		// Install express, https, and morgan
		string npmInstallCommand = $"install express https morgan cors --prefix \"{LibsPath}\" --loglevel=error";
		NodeInstaller.RunNpmCommand(npmInstallCommand);
		//RunCommand(NpmPath, npmInstallCommand, waitForExit: true);
	}

	private static string FindScriptPath(string filename)
	{
		string[] guids = AssetDatabase.FindAssets("NodeServerManager t:Script");
		foreach (string guid in guids)
		{
			string assetPath = AssetDatabase.GUIDToAssetPath(guid);
			string dir = Path.GetDirectoryName(assetPath);
			string filePath = Path.Combine(dir, filename);

			if (File.Exists(filePath))
			{
				return filePath;
			}
		}

		return null;
	}

	public const string ServerStateKey = "NodeServerRunning";

	private static NodeProcessSafeHandle nodeProcessHandle;


	//[MenuItem("Tools/Start HTTPS Server")]
	public static void StartHttpsServer()
	{
		if (s_StartingHTTPServer || s_HTTPServerWasStarted)
		{
			//already trying to start server, ignoring
			return;
		}

		try
		{
			s_StartingHTTPServer = true;
			s_HTTPServerWasStarted = StartHTTPSServerLogic();
			s_StartingHTTPServer = false;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			s_StartingHTTPServer = false;
			s_HTTPServerWasStarted = false;
			StopOrphanedProcesses();
		}
	}
	private static bool s_StartingHTTPServer = false;
	private static bool s_HTTPServerWasStarted = false;
	private static bool StartHTTPSServerLogic()
	{
		StopOrphanedProcesses(); // Ensure no old processes are running

		if (nodeProcessHandle != null && nodeProcessHandle.IsRunning)
		{
			Debug.LogWarning("Node.js server is already running.");
			return false;
		}

		string serverScriptPath = Path.Combine(ToolsPath, ServerScriptName);

		if (!File.Exists(serverScriptPath))
		{
			Debug.LogError("Server script not found. Run 'Copy Server Script' first.");
			return false;
		}

		if (!File.Exists(CertPath) || !File.Exists(KeyPath))
		{
			Debug.LogError("SSL certificate files not found. Run MkCertManager first.");
			return false;
		}

		string args = $"\"{CertPath}\" \"{KeyPath}\"";

		List<string> errorsAtStart = new List<string>();
		bool isStarted = false;
		// Use the new NodePathHelper method to get a SafeHandle
		DataReceivedEventHandler outputDataReceived = (sender, e) =>
		{
			if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"Node output: {e.Data}");
		};
		DataReceivedEventHandler errorDataReceived = (sender, e) =>
		{
			if(!isStarted) errorsAtStart.Add(e.Data);
			if (!string.IsNullOrEmpty(e.Data)) Debug.LogError($"Node error: {e.Data}");
		};
		nodeProcessHandle = NodePathHelper.RunNodeWithSafeHandle(serverScriptPath, args,outputDataReceived,errorDataReceived);
		isStarted = true;
		//|| !nodeProcessHandle.IsRunning //may take a little while to run i guess?
		if (nodeProcessHandle == null || errorsAtStart.Count > 0)
		{
			nodeProcessHandle?.Dispose();
			nodeProcessHandle = null;
			Debug.LogError("Failed to start Node.js server.");
			return false;
		}

		// Store session state so it restarts after a domain reload
		SessionState.SetBool(ServerStateKey, true);

		// Ensure process stops when Unity shuts down
		AppDomain.CurrentDomain.ProcessExit -= StopProcessEventDelegate;
		AppDomain.CurrentDomain.ProcessExit += StopProcessEventDelegate;

		AppDomain.CurrentDomain.DomainUnload -= StopProcessDoNotClearSessionStateEventDelegate;
		AppDomain.CurrentDomain.DomainUnload += StopProcessDoNotClearSessionStateEventDelegate;

		EditorApplication.wantsToQuit -= WantsToQuit;
		EditorApplication.wantsToQuit += WantsToQuit;

		EditorApplication.quitting -= StopHttpsServer;
		EditorApplication.quitting += StopHttpsServer;
		s_HTTPServerWasStarted = true;
		return true;
	}

	private static void StopProcessEventDelegate(object sender, EventArgs e)
	{
		StopHttpsServer();
	}

	private static void StopProcessDoNotClearSessionStateEventDelegate(object sender, EventArgs e)
	{
		StopHttpsServerDoNotClearSessionStateVariable();
	}

	private static bool WantsToQuit()
	{
		StopHttpsServer();
		return true; // Allow Unity to quit
	}

	private static void StopOrphanedProcesses()
	{
		foreach (var process in Process.GetProcessesByName("node"))
		{
			try
			{
				if (process.MainModule == null) continue;
				string processFileNamePath = process.MainModule.FileName;
				if (processFileNamePath.Contains(ToolsPath) || processFileNamePath.Contains(NodePath))
				{
					process.Kill();
					process.WaitForExit();
					Debug.Log("Killed orphaned Node.js process.");
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"Failed to stop orphaned process: {e.Message}");
			}
		}
	}

	//[MenuItem("Tools/Stop HTTPS Server")]
	public static void StopHttpsServer()
	{
		StopHttpsServer(true);
	}

	public static void StopHttpsServerDoNotClearSessionStateVariable()
	{
		StopHttpsServer(false);
	}

	public static void StopHttpsServer(bool doNotRestartLater = true)
	{
		Debug.Log("in StopHttpsServer");
		if (nodeProcessHandle != null && nodeProcessHandle.IsRunning)
		{
			nodeProcessHandle.Dispose();
			nodeProcessHandle = null;
			Debug.Log("Node.js server stopped.");
		}
		else
		{
			Debug.LogWarning("No Node.js server is running.");
		}

		s_HTTPServerWasStarted = false;
		if (doNotRestartLater)
		{
			Debug.Log("Clearing session state.");
			// Clear session state
			SessionState.SetBool(ServerStateKey, false);
		}
	}


	[InitializeOnLoad]
	public class UnityEditorDomainReloadListener
	{
		private static bool shouldRunServer;
		private static float restartDelayInSeconds = 3f; // 3 second delay
		private static double startTime;

		static UnityEditorDomainReloadListener()
		{
			// Restore server if it was running before domain reload
			bool shouldRunServer = SessionState.GetBool(NodeServerManager.ServerStateKey, false);
			if (shouldRunServer)
			{
				NodeServerManager.StopHttpsServer();
				NodeServerManager.StartHttpsServer();
			}

			// Stop server gracefully when Unity quits
			UnityEditor.EditorApplication.wantsToQuit += () =>
			{
				NodeServerManager.StopHttpsServer();
				return true;
			};
			UnityEditor.EditorApplication.quitting += NodeServerManager.StopHttpsServer;
		}

	}
}
