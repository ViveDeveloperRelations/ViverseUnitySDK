using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Manages the HTTPS server setup for WebGL development
/// </summary>
public class WebGLServerManager
{
    // Static paths reused from other classes to ensure consistency
    private static string ToolsPath => Path.Combine(Application.dataPath, "..", "tools");
    private static string CertPath => Path.Combine(ToolsPath, "create.viverse.com.pem");
    private static string KeyPath => Path.Combine(ToolsPath, "create.viverse.com-key.pem");
    private static string LibsPath => Path.Combine(ToolsPath, "libs");
    private static string ServerScriptPath => Path.Combine(ToolsPath, "server.js");

    // Session state keys for caching setup states
    public const string MkcertInstalledKey = "WebGLSettings_MkcertInstalled";
    public const string CertificatesGeneratedKey = "WebGLSettings_CertificatesGenerated";
    public const string NodeModulesInstalledKey = "WebGLSettings_NodeModulesInstalled";
    public const string ServerScriptCopiedKey = "WebGLSettings_ServerScriptCopied";

    /// <summary>
    /// Checks if mkcert is installed on the system
    /// </summary>
    public bool IsMkcertInstalled()
    {
        try
        {
            Process process = new Process();
            process.StartInfo.FileName = "mkcert";
            process.StartInfo.Arguments = "-version";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            process.WaitForExit();

            bool installed = process.ExitCode == 0;
            if (installed)
            {
                SessionState.SetBool(MkcertInstalledKey, true);
            }
            return installed;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates SSL certificates using MkCertManager
    /// </summary>
    public bool GenerateSSLCertificates()
    {
        // Use the existing MkCertManager
        bool createdCert = MkCertManager.GenerateSSLCertificate("create.viverse.com");

        // Verify certificates were created
        if (createdCert && File.Exists(CertPath) && File.Exists(KeyPath))
        {
            SessionState.SetBool(CertificatesGeneratedKey, true);
        }
        return SessionState.GetBool(CertificatesGeneratedKey, false);
    }

    /// <summary>
    /// Installs required Node.js modules
    /// </summary>
    public void InstallNodeModules()
    {
        // Use existing NodeServerManager
        NodeServerManager.InstallNodeModules();

        // Verify node_modules directory exists
        if (Directory.Exists(Path.Combine(LibsPath, "node_modules")))
        {
            SessionState.SetBool(NodeModulesInstalledKey, true);
        }
    }

    /// <summary>
    /// Copies the server.js script to the tools directory
    /// </summary>
    public void CopyServerScript()
    {
        // Use existing NodeServerManager
        NodeServerManager.CopyServerScript();

        // Verify server script exists
        if (File.Exists(ServerScriptPath))
        {
            SessionState.SetBool(ServerScriptCopiedKey, true);
        }
    }

    /// <summary>
    /// Checks if the server setup steps have been completed
    /// </summary>
    public ServerSetupStatus GetServerSetupStatus()
    {
        bool mkcertInstalled = SessionState.GetBool(MkcertInstalledKey, false);
        if (!mkcertInstalled && IsMkcertInstalled())
        {
            mkcertInstalled = true;
        }

        bool certificatesGenerated = SessionState.GetBool(CertificatesGeneratedKey, false);
        if (!certificatesGenerated && File.Exists(CertPath) && File.Exists(KeyPath))
        {
            certificatesGenerated = true;
            SessionState.SetBool(CertificatesGeneratedKey, true);
        }

        bool nodeModulesInstalled = SessionState.GetBool(NodeModulesInstalledKey, false);
        if (!nodeModulesInstalled && Directory.Exists(Path.Combine(LibsPath, "node_modules")))
        {
            nodeModulesInstalled = true;
            SessionState.SetBool(NodeModulesInstalledKey, true);
        }

        bool serverScriptCopied = SessionState.GetBool(ServerScriptCopiedKey, false);
        if (!serverScriptCopied && File.Exists(ServerScriptPath))
        {
            serverScriptCopied = true;
            SessionState.SetBool(ServerScriptCopiedKey, true);
        }

        return new ServerSetupStatus(
            mkcertInstalled,
            certificatesGenerated,
            nodeModulesInstalled,
            serverScriptCopied,
            IsServerRunning()
        );
    }

    /// <summary>
    /// Starts the HTTPS server
    /// </summary>
    public void StartServer()
    {
        if (!IsServerRunning())
        {
            NodeServerManager.StartHttpsServer();
        }
    }

    /// <summary>
    /// Stops the HTTPS server
    /// </summary>
    public void StopServer()
    {
        if (IsServerRunning())
        {
            NodeServerManager.StopHttpsServer();
        }
    }

    /// <summary>
    /// Checks if the server is currently running
    /// </summary>
    public bool IsServerRunning()
    {
        // We can't directly access NodeServerManager.nodeProcessHandle since it's private
        // Instead we'll use the ServerStateKey
        return SessionState.GetBool(NodeServerManager.ServerStateKey, false);
    }

    /// <summary>
    /// Represents the status of the server setup
    /// </summary>
    public struct ServerSetupStatus
    {
        public bool MkcertInstalled;
        public bool CertificatesGenerated;
        public bool NodeModulesInstalled;
        public bool ServerScriptCopied;
        public bool ServerRunning;

        public ServerSetupStatus(
            bool mkcertInstalled,
            bool certificatesGenerated,
            bool nodeModulesInstalled,
            bool serverScriptCopied,
            bool serverRunning)
        {
            MkcertInstalled = mkcertInstalled;
            CertificatesGenerated = certificatesGenerated;
            NodeModulesInstalled = nodeModulesInstalled;
            ServerScriptCopied = serverScriptCopied;
            ServerRunning = serverRunning;
        }
    }
}
