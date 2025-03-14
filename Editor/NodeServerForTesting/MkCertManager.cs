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
    public static string GetExePath(string exeName)
    {
	    // Get PATH environment variable
	    string path = Environment.GetEnvironmentVariable("PATH") ?? "";
	    string delimiter = Path.PathSeparator.ToString();

	    if (Application.platform == RuntimePlatform.OSXEditor)
	    {
		    // On macOS, also check the common Homebrew paths that might not be in PATH
		    var additionalPaths = new[]
		    {
			    "/opt/homebrew/bin",       // Apple Silicon Homebrew
			    "/usr/local/bin",          // Intel Homebrew
			    "/opt/local/bin",          // MacPorts
			    $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.local/bin" // User local bin
		    };
		    foreach (var additionalPath in additionalPaths)
		    {
			    if (!path.Contains(additionalPath))
			    {
				    path = $"{additionalPath}{delimiter}{path}";
			    }
		    }
	    }

	    // Split path by delimiter
	    string[] directories = path.Split(delimiter);

	    // Search for the executable in each directory
	    foreach (string directory in directories)
	    {
		    if (string.IsNullOrEmpty(directory)) continue;

		    string fullPath = Path.Combine(directory, exeName);
		    if (File.Exists(fullPath))
		    {
			    return fullPath;
		    }

		    if (Application.platform == RuntimePlatform.WindowsEditor && File.Exists(fullPath+".exe"))
		    {
			    return fullPath + ".exe";
		    }
	    }
	    // Look for the executable in each directory
	    foreach (string directory in directories)
	    {
		    if (string.IsNullOrEmpty(directory)) continue;

		    string fullPath = Path.Combine(directory, exeName);
		    if (File.Exists(fullPath))
		    {
			    return fullPath;
		    }
	    }

	    return exeName;
    }

    public static string MkcertPath()
    {
	    return GetExePath("mkcert");
    }
    /// <summary>
    /// Checks if mkcert is installed on the system
    /// </summary>
    public static bool IsMkcertInstalled()
    {
	    if (!File.Exists(MkCertManager.MkcertPath()))
	    {
		    return false;
	    }
	    try
	    {
		    Process process = new Process();

		    process.StartInfo.FileName = MkCertManager.MkcertPath();
		    process.StartInfo.Arguments = "-version";
		    process.StartInfo.UseShellExecute = false;
		    process.StartInfo.RedirectStandardOutput = true;
		    process.StartInfo.RedirectStandardError = true;
		    process.StartInfo.CreateNoWindow = true;

		    process.Start();
		    process.WaitForExit();

		    bool installed = process.ExitCode == 0;

		    return installed;
	    }
	    catch (Exception e)
	    {
		    Debug.LogException(e);
		    return false;
	    }
    }

    public static bool GenerateSSLCertificate(string domainToInstallFor)
    {
	    EnsureToolsDirectoryExists();
	    (string output, bool success) = RunCommand(MkcertPath(), "-version", printErrors: true);
	    if(!success)
	    {
		    Debug.LogError("mkcert is not installed. Please install it globally and restart Unity.");
		    return false;
	    }

	    if (Application.platform != RuntimePlatform.OSXEditor)
	    {
		    //osx needs elevated privileges for install, so skip it
		    Debug.Log("Running mkcert -install...");
		    (output, success) = RunCommand(MkcertPath(), "-install", printErrors: false);
		    if(!success && (!string.IsNullOrWhiteSpace(output) && !output.Contains("The local CA is already installed in the system trust store")))
		    {
			    Debug.LogError("mkcert -install failed.");
			    return false;
		    }
	    }


	    Debug.Log($"Generating SSL certificate for {domainToInstallFor}...");
	    string keyPath = Path.Combine(ToolsPath, $"{domainToInstallFor}-key.pem");
	    string certPath = Path.Combine(ToolsPath, $"{domainToInstallFor}.pem");

	    (output, success) = RunCommandButReturnErrorStream(MkcertPath(), $"-key-file \"{keyPath}\" -cert-file \"{certPath}\" {domainToInstallFor}", printErrors: false);

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
