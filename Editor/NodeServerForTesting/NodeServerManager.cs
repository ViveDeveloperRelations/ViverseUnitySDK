using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;


public class NodeServerManager
{
	public static string ToolsPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "tools"));
	public static string LibsPath => Path.Combine(ToolsPath, "libs");
	public static string NodeModulesPath => Path.Combine(LibsPath, "node_modules");

	public static string ServerScriptName = "server.js";
	public static string CertPath => Path.Combine(ToolsPath, "create.viverse.com.pem");
	public static string KeyPath => Path.Combine(ToolsPath, "create.viverse.com-key.pem");

	public static string NodePath => NodePathHelper.NodePath;
	public static string NpmPath => NodePathHelper.NpmPath;
	public static bool ServerScriptExists => File.Exists(Path.Combine(NodeServerManager.ToolsPath, NodeServerManager.ServerScriptName));
	public static bool ServerScriptIsTheSameAsOneInEditor()
	{
		string scriptInEditorPath = ServerScriptPath; // Path to server.js in Assets
		string scriptInToolsPath = Path.Combine(ToolsPath, ServerScriptName); // Path to server.js in Tools

		// Check if either file doesn't exist. If so, they are not the same.
		if (!File.Exists(scriptInEditorPath))
		{
			// Debug.LogWarning($"Server script not found in Editor: {scriptInEditorPath}"); // Optional: uncomment for debugging
			return false;
		}

		if (!File.Exists(scriptInToolsPath))
		{
			// Debug.LogWarning($"Server script not found in Tools path: {scriptInToolsPath}"); // Optional: uncomment for debugging
			return false;
		}

		try
		{
			// Compare file contents byte by byte
			byte[] editorFileBytes = File.ReadAllBytes(scriptInEditorPath);
			byte[] toolsFileBytes = File.ReadAllBytes(scriptInToolsPath);

			if (editorFileBytes.Length != toolsFileBytes.Length)
			{
				return false; // Different sizes mean they are different
			}

			for (int i = 0; i < editorFileBytes.Length; i++)
			{
				if (editorFileBytes[i] != toolsFileBytes[i])
				{
					return false; // Found a byte difference
				}
			}
			return true; // Files are identical
		}
		catch (IOException ex)
		{
			Debug.LogError($"Error comparing server scripts: {ex.Message}");
			return false; // If an error occurs, assume they are not the same
		}
	}

	private static string ServerScriptPath
	{
		get
		{
			string scriptSource = FindScriptPath(ServerScriptName);
			if (string.IsNullOrEmpty(scriptSource))
			{
				Debug.LogError($"server.js not found in Unity project.");
				return default;
			}
			return scriptSource;
		}
	}

	//[MenuItem("Tools/Copy Server Script")]
	public static void CopyServerScript()
	{
		string scriptSource = ServerScriptPath;

		if (!Directory.Exists(ToolsPath))
		{
			Directory.CreateDirectory(ToolsPath);
		}

		string destination = Path.Combine(ToolsPath, ServerScriptName);
		File.Copy(scriptSource, destination, true);
		Debug.Log($"Copied {ServerScriptName} to {ToolsPath}");
	}

	private static readonly NodeInstaller.NodeModuleVersionInfo[] NodeModulesNeeded = new[]
	{
		new NodeInstaller.NodeModuleVersionInfo(){NodeModuleName="express", NodeModuleVersion = "4.21.2"},
		new NodeInstaller.NodeModuleVersionInfo(){NodeModuleName="https", NodeModuleVersion = "1.0.0"},
		new NodeInstaller.NodeModuleVersionInfo(){NodeModuleName="morgan", NodeModuleVersion = "1.10.0"},
		new NodeInstaller.NodeModuleVersionInfo(){NodeModuleName="cors", NodeModuleVersion = "2.8.5"},
		new NodeInstaller.NodeModuleVersionInfo(){NodeModuleName="express-static-gzip", NodeModuleVersion = "2.2.0"},
	};

    public static bool AllNodeModulesAreInstalled()
    {
	    List<NodeInstaller.NodeModuleVersionInfo> installedModules = NodeInstaller.GetNodeModulesFromDirectory(NodeModulesPath);
	    //NOTE: this does not remove anything at the moment, extra packages will be ignored
	    foreach (NodeInstaller.NodeModuleVersionInfo requiredModule in NodeModulesNeeded)
	    {
		    bool found = false;
		    foreach (NodeInstaller.NodeModuleVersionInfo installedModule in installedModules)
		    {
			    if (installedModule.NodeModuleName == requiredModule.NodeModuleName &&
			        installedModule.NodeModuleVersion == requiredModule.NodeModuleVersion)
			    {
				    found = true;
				    break;
			    }
		    }

		    if (!found)
		    {
			    //Debug.Log($"Did not find package {requiredModule.NodeModuleName} with version {requiredModule.NodeModuleVersion} ");
			    return false;
		    }
	    }

	    return true;
    }

	//[MenuItem("Tools/Install Node Modules")]
	public static void InstallNodeModules()
	{
		if (!Directory.Exists(LibsPath))
		{
			Directory.CreateDirectory(LibsPath);
		}

		StringBuilder nodeModulesStringBuilder = new StringBuilder();
		foreach (NodeInstaller.NodeModuleVersionInfo nodeModuleVersionInfo in NodeModulesNeeded)
		{
			nodeModulesStringBuilder.Append(
				$"{nodeModuleVersionInfo.NodeModuleName}@{nodeModuleVersionInfo.NodeModuleVersion} ");
		}
		string npmInstallCommand = $"install {nodeModulesStringBuilder} --prefix \"{LibsPath}\" --loglevel=error";
		//Debug.Log("Running npm "+npmInstallCommand);
		NodeInstaller.RunNpmCommand(npmInstallCommand);
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
