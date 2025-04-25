using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class MkCertManager
{
	// Define the base path one level above the Unity project's Assets folder
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
	private static (string, bool) RunCommand(string exePath, string arguments, bool printErrors)
	{
		(string output, string errorOutput, bool success, int exitCode) =
			RunCommandButReturnOutputAndErrorStreamSuccessAndReturnCode(exePath, arguments, printErrors);
		return (output, string.IsNullOrEmpty(errorOutput));
	}

	/// <summary>
	/// Runs a command synchronously.
	/// </summary>
	private static (string, string, bool, int) RunCommandButReturnOutputAndErrorStreamSuccessAndReturnCode(
		string exePath, string arguments, bool printErrors)
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

		return (output, error, string.IsNullOrEmpty(error), process.ExitCode);
	}

	/// <summary>
	/// Runs a command synchronously.
	/// </summary>
	private static (string, bool) RunCommandButReturnErrorStream(string exePath, string arguments, bool printErrors)
	{
		(string output, string errorOutput, bool success, int exitCode) =
			RunCommandButReturnOutputAndErrorStreamSuccessAndReturnCode(exePath, arguments, printErrors);
		return (errorOutput, string.IsNullOrEmpty(errorOutput));
	}

	/// <summary>
	/// Shows a dialog to inform the user that admin privileges are needed and runs mkcert with these privileges on macOS
	/// </summary>
	/// <returns>True if installation was successful, false otherwise</returns>
	public static bool RunMkcertInstallWithDialog()
	{
		if (Application.platform == RuntimePlatform.OSXEditor)
		{
			/*
			if (VerifyMkcertCaRootDirectory())
			{
				//mkcert -install was already run at some point
				return true;
			}
			//this can fail if the user is re-installing
			*/
			string mkcertPath = MkcertPath();

			// First show a dialog to inform the user about Terminal opening
			bool proceed = EditorUtility.DisplayDialog(
				"Certificate Installation Required",
				"A Terminal window will open to install the local certificate authority. You will need to enter your admin password when prompted.\n\nThis is required for local HTTPS development.",
				"Proceed", "Cancel");

			if (!proceed)
			{
				Debug.Log("User cancelled mkcert installation.");
				return false;
			}

			// Use a more reliable AppleScript to open Terminal and run the sudo command
			string appleScript =
				"tell application \"Terminal\"\n" +
				"    activate\n" +
				$"    do script \"sudo '{mkcertPath}' -install && echo 'Certificate installation completed.' && exit\"\n" +
				"end tell";

			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "/usr/bin/osascript",
				Arguments = $"-e \"{appleScript.Replace("\"", "\\\"")}\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			using Process process = new Process();
			process.StartInfo = psi;
			process.Start();

			string output = process.StandardOutput.ReadToEnd();
			string errorOutput = process.StandardError.ReadToEnd();
			process.WaitForExit();

			bool success = process.ExitCode == 0;

			if (success)
			{
				Debug.Log("mkcert installed successfully:\n" + output);
				//TODO: on mac figure out how to verify that install happened correctly, not just terminal launch
				return true;
			}
			else
			{
				Debug.LogError("mkcert installation failed: " + errorOutput);
				EditorUtility.DisplayDialog(
					"mkcert Installation Failed",
					$"mkcert installation failed. Error: {errorOutput}",
					"OK");
				return false;
			}
		}
		else
		{
			return SimpleMkcertInstallSimpleRanSuccessfully();
		}
	}

	//Trivial way to run mkcert, which is intended to succeed or fail in the most simple way. This does not run any command elevation for mac/etc
	//in addition, there's som quirks about how mkcert returns success/failure in the errstream that this manages
	private static bool SimpleMkcertInstallSimpleRanSuccessfully()
	{
		// For other platforms, just run the command directly
		(string output, bool success) =
			RunCommandButReturnErrorStream(MkcertPath(), "-install", printErrors: false);

		if (success || output.Contains("The local CA is already installed"))
		{
			Debug.Log("mkcert ran successfully.");
			return true;
		}
		else
		{
			Debug.LogError($"mkcert -install failed: {output}");
			return false;
		}
	}

	/// <summary>
	/// Runs mkcert to generate SSL certificates.
	/// </summary>
	//[MenuItem("Test/Generate Viverse Create Pem Files")]
	public static void GenerateSSLCertificate()
	{
		GenerateSSLCertificate("create.viverse.com");
	}

	public static bool GenerateSSLCertificate(string domainToInstallFor)
	{
		EnsureToolsDirectoryExists();
		(string output, bool success) = RunCommand(MkcertPath(), "-version", printErrors: true);
		if (!success)
		{
			Debug.LogError("mkcert is not installed. Please install it globally and restart Unity.");
			EditorUtility.DisplayDialog(
				"mkcert Not Found",
				"mkcert is not installed on your system. Please install it and restart Unity.",
				"OK");
			return false;
		}

		Debug.Log("Running mkcert -install...");

		// Use the dialog-based approach to install mkcert CA
		bool installSuccess = RunMkcertInstallWithDialog();
		if (!installSuccess)
		{
			Debug.LogError("mkcert -install failed.");
			return false;
		}

		Debug.Log($"Generating SSL certificate for {domainToInstallFor}...");
		string keyPath = Path.Combine(ToolsPath, $"{domainToInstallFor}-key.pem");
		string certPath = Path.Combine(ToolsPath, $"{domainToInstallFor}.pem");

		(output, success) = RunCommandButReturnErrorStream(MkcertPath(),
			$"-key-file \"{keyPath}\" -cert-file \"{certPath}\" {domainToInstallFor}", printErrors: false);

		string normalizedOutput = string.IsNullOrWhiteSpace(output) ? "" : output.Normalize(NormalizationForm.FormC);

		// **Change: Ignore `success` if the output confirms success**
		if (success || normalizedOutput.Contains("Created a new certificate valid for the following names"))
		{
			Debug.Log("mkcert certificate generated successfully:\n" + output);
			return VerifyMkcertCaRootDirectory();
		}
		else
		{
			Debug.LogError("mkcert certificate generation failed.");
			EditorUtility.DisplayDialog(
				"Certificate Generation Failed",
				$"Failed to generate SSL certificate for {domainToInstallFor}.",
				"OK");
			return false;
		}
	}

	//[MenuItem("Test/PrintMkcertPath")]
	public static void PrintPathOfMkcert()
	{
		Debug.Log(UseWhichToGetPath("mkcert"));
	}
	public static string UseWhichToGetPath(string exeName)
	{
	    // Get the current user's home directory
	    string homeDir = Environment.GetEnvironmentVariable("HOME");
	    //Debug.Log($"Home directory: {homeDir}");

	    try
	    {
	        // Approach 1: Try with login shell and PATH export
	        var startInfo = new System.Diagnostics.ProcessStartInfo
	        {
	            FileName = "/bin/bash",
	            Arguments = $"-l -c \"echo $PATH; which {exeName}\"",
	            RedirectStandardOutput = true,
	            RedirectStandardError = true,
	            UseShellExecute = false,
	            CreateNoWindow = true
	        };

	        using (var process = System.Diagnostics.Process.Start(startInfo))
	        {
	            string output = process.StandardOutput.ReadToEnd().Trim();
	            string error = process.StandardError.ReadToEnd().Trim();
	            process.WaitForExit();

	            string[] lines = output.Split('\n');
	            if (lines.Length > 1)
	            {
	                Debug.Log($"PATH from login shell: {lines[0]}");
	                string potentialPath = lines[lines.Length - 1];
	                if (File.Exists(potentialPath))
	                {
	                    Debug.Log($"Found {exeName} using login shell at: {potentialPath}");
	                    return potentialPath;
	                }
	            }

	            if (!string.IsNullOrEmpty(error))
	            {
	                Debug.LogWarning($"Error in login shell approach: {error}");
	            }
	        }

	        // Approach 2: Try with common profile files that might contain PATH settings
	        string[] profileFiles = {
	            Path.Combine(homeDir, ".zshrc"),
	            Path.Combine(homeDir, ".bash_profile"),
	            Path.Combine(homeDir, ".profile"),
	            Path.Combine(homeDir, ".bashrc")
	        };

	        foreach (string profileFile in profileFiles)
	        {
	            if (File.Exists(profileFile))
	            {
	                Debug.Log($"Trying to source: {profileFile}");
	                startInfo.Arguments = $"-c \"source {profileFile} 2>/dev/null; which {exeName}\"";

	                using (var process = System.Diagnostics.Process.Start(startInfo))
	                {
	                    string output = process.StandardOutput.ReadToEnd().Trim();
	                    process.WaitForExit();

	                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
	                    {
	                        Debug.Log($"Found {exeName} using {profileFile} at: {output}");
	                        return output;
	                    }
	                }
	            }
	        }

	        // Approach 3: Check common binary locations directly
	        string[] commonPaths = {
	            "/usr/local/bin",
	            "/opt/homebrew/bin", // Apple Silicon Macs using Homebrew
	            "/usr/bin",
	            "/bin",
	            Path.Combine(homeDir, ".local/bin"),
	            Path.Combine(homeDir, "bin")
	        };

	        foreach (string path in commonPaths)
	        {
	            string fullPath = Path.Combine(path, exeName);
	            if (File.Exists(fullPath))
	            {
	                //Debug.Log($"Found {exeName} in common path: {fullPath}");
	                return fullPath;
	            }
	        }
	    }
	    catch (System.Exception e)
	    {
	        Debug.LogError($"Failed to find {exeName}: {e.Message}");
	    }

	    Debug.LogWarning($"Could not find {exeName} in PATH");
	    return string.Empty;
	}
	public static string GetExePath(string exeName)
	{
	    // Get PATH environment variable
	    string path = Environment.GetEnvironmentVariable("PATH") ?? "";
	    string delimiter = Path.PathSeparator.ToString();

	    //hack to try to guess common paths (since the user's path doesn't get inherited by unity)
	    if (Application.platform == RuntimePlatform.OSXEditor)
	    {
	        // On macOS, also check the common Homebrew paths that might not be in PATH
	        var additionalPaths = new[]
	        {
	            "/opt/homebrew/bin", // Apple Silicon Homebrew
	            "/usr/local/bin", // Intel Homebrew
	            "/opt/local/bin", // MacPorts
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

	        if (Application.platform == RuntimePlatform.WindowsEditor && File.Exists(fullPath + ".exe"))
	        {
	            return fullPath + ".exe";
	        }
	    }

	    return exeName;
	}

	public static string MkcertPath()
	{
		if (Application.platform == RuntimePlatform.OSXEditor)
		{
			string mkcertFromWhich = UseWhichToGetPath("mkcert");
			if (!string.IsNullOrEmpty(mkcertFromWhich))return mkcertFromWhich;
			string[] brewPaths = {
				"/opt/homebrew/bin/mkcert",  // Apple Silicon Macs
				"/usr/local/bin/mkcert"      // Intel Macs
			};

			foreach (string path in brewPaths)
			{
				if (File.Exists(path))
				{
					Debug.Log($"Found mkcert via direct Homebrew path check: {path}");
					return path;
				}
			}

			// Last resort - try to find in tools directory
			string toolsPath = Path.Combine(ToolsPath, "mkcert");
			if (File.Exists(toolsPath))
			{
				Debug.Log($"Found mkcert in tools directory: {toolsPath}");
				return toolsPath;
			}
		}
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


    /// <summary>
    /// Runs 'mkcert -CAROOT' and returns the output path.
    /// </summary>
    /// <returns>The CA root directory path, or null if an error occurred.</returns>
    public static string GetMkcertCaRootDir()
    {
        string mkcertExecutable = MkcertPath();
        if (string.IsNullOrEmpty(mkcertExecutable))
        {
	        Debug.LogError("Cannot run mkcert -CAROOT: mkcert path not found.");
            return null;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = mkcertExecutable,
            Arguments = "-CAROOT",
            UseShellExecute = false, // Required for redirection
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true // Don't show a console window
        };

        //Debug.Log($"Running: \"{startInfo.FileName}\" {startInfo.Arguments}");

        try
        {
            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();

                // Read output and error streams
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit(); // Wait for the process to complete

                if (process.ExitCode == 0)
                {
                    string caRootDir = output.Trim(); // Remove leading/trailing whitespace/newlines
                    if (!string.IsNullOrEmpty(caRootDir))
                    {
	                    //Debug.Log($"mkcert -CAROOT returned: {caRootDir}");
                        return caRootDir;
                    }
                    else
                    {
	                    Debug.LogError("mkcert -CAROOT executed successfully but returned an empty path.");
                        if (!string.IsNullOrEmpty(error)) Debug.LogError($"mkcert stderr: {error}");
                        return null;
                    }
                }
                else
                {
	                Debug.LogError($"mkcert -CAROOT failed with exit code {process.ExitCode}.");
                    if (!string.IsNullOrEmpty(output)) Debug.Log($"mkcert stdout: {output}");
                    if (!string.IsNullOrEmpty(error)) Debug.LogError($"mkcert stderr: {error}");
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
	        Debug.LogError($"Error executing mkcert: {ex.Message}");
            // Log details like stack trace if needed for debugging
            // LogError($"Stack Trace: {ex.StackTrace}");
            return null;
        }
    }

    //[MenuItem("Test/runmkcertinstall")]
	public static void RunMkcertInstall()
	{
		Debug.Log("Ran successfully: "+RunMkcertInstallWithDialog());
	}
	//[MenuItem("Test/verify mkcert install ran")]
	public static void VerifyMkCertinstallRanPreviously()
	{
		Debug.Log("Ran successfully: "+VerifyMkcertCaRootDirectory());
	}
    /// <summary>
    /// Verifies the mkcert CA root directory and the presence/size of key files.
    /// </summary>
    /// <returns>True if the directory and non-empty key files exist, false otherwise.</returns>
    public static bool VerifyMkcertCaRootDirectory()
    {
        string caRootDir = GetMkcertCaRootDir();

        if (string.IsNullOrEmpty(caRootDir))
        {
            Debug.LogError("Verification failed: Could not determine mkcert CA root directory.");
            return false;
        }

        // Check if the directory exists
        if (!Directory.Exists(caRootDir))
        {
            Debug.LogError($"Verification failed: CA root directory does not exist: {caRootDir}");
            return false;
        }

        //Debug.Log($"CA root directory exists: {caRootDir}");

        // Define expected file names
        string rootCaPemFile = "rootCA.pem";
        string rootCaKeyFile = "rootCA-key.pem";

        string pemFilePath = Path.Combine(caRootDir, rootCaPemFile);
        string keyFilePath = Path.Combine(caRootDir, rootCaKeyFile);

        bool pemVerified = false;
        bool keyVerified = false;

        // Check rootCA.pem
        try
        {
            FileInfo pemInfo = new FileInfo(pemFilePath);
            if (pemInfo.Exists)
            {
                if (pemInfo.Length > 0)
                {
                    //Debug.Log($"Verified: {rootCaPemFile} exists and has size {pemInfo.Length} bytes.");
                    pemVerified = true;
                }
                else
                {
                    //Debug.LogError($"Verification failed: {rootCaPemFile} exists but is empty (0 bytes). Path: {pemFilePath}");
                }
            }
            else
            {
                //Debug.LogError($"Verification failed: {rootCaPemFile} does not exist. Path: {pemFilePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error accessing file info for {rootCaPemFile}: {ex.Message}");
            // This might happen due to permissions issues *on the directory itself*,
            // though FileInfo usually works even if file read permission is denied.
        }

        // Check rootCA-key.pem
        try
        {
            FileInfo keyInfo = new FileInfo(keyFilePath);
            if (keyInfo.Exists)
            {
                // FileInfo.Length reads metadata, not the file content, so it
                // should work even if read permissions are restricted.
                if (keyInfo.Length > 0)
                {
                    //Debug.Log($"Verified: {rootCaKeyFile} exists and has size {keyInfo.Length} bytes.");
                    keyVerified = true;
                }
                else
                {
                    Debug.LogError($"Verification failed: {rootCaKeyFile} exists but is empty (0 bytes). Path: {keyFilePath}");
                }
            }
            else
            {
                Debug.LogError($"Verification failed: {rootCaKeyFile} does not exist. Path: {keyFilePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error accessing file info for {rootCaKeyFile}: {ex.Message}");
        }

        return pemVerified && keyVerified;
    }
}
