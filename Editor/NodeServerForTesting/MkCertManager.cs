using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

public class MkCertManager
{
    // Define the base path one level above the Unity project’s Assets folder
    private static string GetProjectRootPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    // Define the tools directory path
    private static string ToolsPath => Path.Combine(GetProjectRootPath(), "tools");


    /// <summary>
    /// Ensures the tools directory exists.
    /// </summary>
    private static void EnsureToolsDirectoryExists()
    {
        if (!Directory.Exists(ToolsPath))
        {
            Directory.CreateDirectory(ToolsPath);
            Debug.Log("Created tools directory at: " + ToolsPath);
        }
    }

    /// <summary>
    /// Runs a command synchronously.
    /// </summary>
    private static (string,bool) RunCommand(string exePath, string arguments, bool printErrors)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new Process();
        process.StartInfo = psi;
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrEmpty(error) && printErrors)
		{
			Debug.LogError($"Error running {exePath} {arguments}: {error}");
		}

        return (output, string.IsNullOrEmpty(error));
    }
    /// <summary>
    /// Runs a command synchronously.
    /// </summary>
    private static (string,bool) RunCommandButReturnErrorStream(string exePath, string arguments, bool printErrors)
    {
	    ProcessStartInfo psi = new ProcessStartInfo
	    {
		    FileName = exePath,
		    Arguments = arguments,
		    RedirectStandardOutput = true,
		    RedirectStandardError = true,
		    UseShellExecute = false,
		    CreateNoWindow = true
	    };

	    using Process process = new Process();
	    process.StartInfo = psi;
	    process.Start();
	    string output = process.StandardOutput.ReadToEnd();
	    string error = process.StandardError.ReadToEnd();
	    process.WaitForExit();

	    if (!string.IsNullOrEmpty(error) && printErrors)
	    {
		    Debug.LogError($"Error running {exePath} {arguments}: {error}");
	    }

	    return (error, string.IsNullOrEmpty(error));
    }
    /// <summary>
    /// Runs mkcert to generate SSL certificates.
    /// </summary>
    //[MenuItem("Tools/Generate SSL Certificate")]
    public static void GenerateSSLCertificate()
    {
	    GenerateSSLCertificate("create.viverse.com");
    }
    public static bool GenerateSSLCertificate(string domainToInstallFor)
    {
	    EnsureToolsDirectoryExists();
	    (string output, bool success) = RunCommand("mkcert", "-version", printErrors: true);
	    if(!success)
	    {
		    Debug.LogError("mkcert is not installed. Please install it globally and restart Unity.");
		    return false;
	    }

	    Debug.Log("Running mkcert -install...");
	    (output, success) = RunCommand("mkcert", "-install", printErrors: false);
	    if(!success && (!string.IsNullOrWhiteSpace(output) && !output.Contains("The local CA is already installed in the system trust store")))
	    {
		    Debug.LogError("mkcert -install failed.");
		    return false;
	    }

	    Debug.Log($"Generating SSL certificate for {domainToInstallFor}...");
	    (output, success) = RunCommandButReturnErrorStream("mkcert", $"-key-file \"{ToolsPath}\\{domainToInstallFor}-key.pem\" -cert-file \"{ToolsPath}\\{domainToInstallFor}.pem\" {domainToInstallFor}", printErrors: false);

	    string normalizedOutput = string.IsNullOrWhiteSpace(output) ? "" : output.Normalize(NormalizationForm.FormC);

	    // **Change: Ignore `success` if the output confirms success**
	    if (success || normalizedOutput.Contains("Created a new certificate valid for the following names"))
	    {
		    Debug.Log("mkcert certificate generated successfully:\n" + output);
		    return true;
	    }
	    else
	    {
		    Debug.LogError("mkcert certificate generation failed.");
		    return false;
	    }
    }
}
