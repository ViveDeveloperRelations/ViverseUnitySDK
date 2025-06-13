using UnityEngine;
using UnityEditor;

/// <summary>
/// Manager class for WebGL server setup operations
/// </summary>
public class WebGLServerManager
{
    /// <summary>
    /// Status of the server setup process
    /// </summary>
    public struct ServerSetupStatus
    {
        public bool MkcertInstalled;
        public bool CertificatesGenerated;
        public bool NodeModulesInstalled;
        public bool ServerScriptCopied;
        public bool ServerRunning;
    }

    /// <summary>
    /// Get the current status of the server setup
    /// </summary>
    public ServerSetupStatus GetServerSetupStatus()
    {
        bool mkcertInstalled = MkCertManager.IsMkcertInstalled();
        bool certsGenerated = CheckCertificatesGenerated();
        bool nodeModulesInstalled = NodeServerManager.AllNodeModulesAreInstalled();
        bool serverScriptCopied = NodeServerManager.ServerScriptIsTheSameAsOneInEditor();
        bool serverRunning = SessionState.GetBool(NodeServerManager.ServerStateKey, false);

        return new ServerSetupStatus
        {
            MkcertInstalled = mkcertInstalled,
            CertificatesGenerated = certsGenerated,
            NodeModulesInstalled = nodeModulesInstalled,
            ServerScriptCopied = serverScriptCopied,
            ServerRunning = serverRunning
        };
    }


    /// <summary>
    /// Check if SSL certificates are generated
    /// </summary>
    private bool CheckCertificatesGenerated()
    {
        string toolsPath = NodeServerManager.ToolsPath;
        string certPath = System.IO.Path.Combine(toolsPath, "create.viverse.com.pem");
        string keyPath = System.IO.Path.Combine(toolsPath, "create.viverse.com-key.pem");

        return System.IO.File.Exists(certPath) && System.IO.File.Exists(keyPath);
    }

    /// <summary>
    /// Generate SSL certificates using mkcert
    /// </summary>
    public void GenerateSSLCertificates()
    {
        MkCertManager.GenerateSSLCertificate();
    }

    /// <summary>
    /// Install Node.js modules required for the server
    /// </summary>
    public void InstallNodeModules()
    {
        NodeServerManager.InstallNodeModules();
    }

    /// <summary>
    /// Copy the server script from the editor to the tools folder
    /// </summary>
    public void CopyServerScript()
    {
        NodeServerManager.CopyServerScript();
    }

    /// <summary>
    /// Start the HTTPS server
    /// </summary>
    public void StartServer()
    {
        NodeServerManager.StartHttpsServer();
    }

    /// <summary>
    /// Stop the HTTPS server
    /// </summary>
    public void StopServer()
    {
        NodeServerManager.StopHttpsServer();
    }
}
