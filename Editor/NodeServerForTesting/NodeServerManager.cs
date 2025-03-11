using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class NodeServerManager
{
    private static string ToolsPath => Path.Combine(Application.dataPath, "..", "tools");
    private static string LibsPath => Path.Combine(ToolsPath, "libs");
    private static string NodeModulesPath => Path.Combine(LibsPath, "lib", "node_modules");
    private static string ServerScriptName = "server.js";
    private static string CertPath => Path.Combine(ToolsPath, "create.viverse.com.pem");
    private static string KeyPath => Path.Combine(ToolsPath, "create.viverse.com-key.pem");

    private static string NodePath => NodePathHelper.NodePath;
    private static string NpmPath => NodePathHelper.NpmPath;

    private static Process nodeProcess = null; // Store the process reference

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

	    RunCommand(NpmPath, npmInstallCommand, waitForExit: true);
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
	    StopOrphanedProcesses(); // Ensure no old processes are running

	    if (nodeProcessHandle != null && nodeProcessHandle.IsRunning)
	    {
		    Debug.LogWarning("Node.js server is already running.");
		    return;
	    }

	    string serverScriptPath = Path.Combine(ToolsPath, ServerScriptName);

	    if (!File.Exists(serverScriptPath))
	    {
		    Debug.LogError("Server script not found. Run 'Copy Server Script' first.");
		    return;
	    }

	    if (!File.Exists(CertPath) || !File.Exists(KeyPath))
	    {
		    Debug.LogError("SSL certificate files not found. Run MkCertManager first.");
		    return;
	    }

	    string args = $"\"{serverScriptPath}\" \"{CertPath}\" \"{KeyPath}\"";

	    nodeProcess = RunCommand(NodePath, args, waitForExit: false);
	    nodeProcessHandle = new NodeProcessSafeHandle();
	    nodeProcessHandle.Initialize(nodeProcess);

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
			    if (process.MainModule.FileName.Contains(ToolsPath))
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

	    if (doNotRestartLater)
	    {
		    Debug.Log("Clearing session state.");
		    // Clear session state
		    SessionState.SetBool(ServerStateKey, false);
	    }
    }


    private static Process RunCommand(string exePath, string arguments, bool waitForExit)
    {
	    ProcessStartInfo psi = new ProcessStartInfo
	    {
		    FileName = exePath,
		    Arguments = arguments,
		    RedirectStandardOutput = true,
		    RedirectStandardError = true,
		    UseShellExecute = false,  // Ensures the process is properly tracked
		    CreateNoWindow = true
	    };

	    psi.EnvironmentVariables["NODE_PATH"] = NodeModulesPath;

	    Process process = new Process { StartInfo = psi };

	    process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log(e.Data); };
	    process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogError(e.Data); };

	    process.Start();
	    process.BeginOutputReadLine();
	    process.BeginErrorReadLine();

	    if (waitForExit)
	    {
		    process.WaitForExit();
		    process.Dispose();
		    return null;
	    }

	    return process; // Return reference for background tasks
    }

    [InitializeOnLoad]
    public class UnityEditorDomainReloadListener
    {
	    static UnityEditorDomainReloadListener()
	    {
		    // Restore server if it was running before domain reload
		    bool shouldRunServer = SessionState.GetBool(NodeServerManager.ServerStateKey, false);
		    //Debug.Log($"Should run server: {shouldRunServer}");
		    if (shouldRunServer)
		    {
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
