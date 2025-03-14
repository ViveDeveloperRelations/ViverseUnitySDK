using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class NodeInstaller
{
	private static readonly string NodeVersion = "v12.18.1";
	//private static readonly string NpmVersion = "6.14.5";

	private static readonly string ToolsFolder = "tools";
	private static readonly string NodeInstallFolder = "nodeinstall";

	private static readonly string WindowsNodeUrl =
		$"https://nodejs.org/dist/{NodeVersion}/node-{NodeVersion}-win-x64.zip";

	private static readonly string MacNodeUrl =
		$"https://nodejs.org/dist/{NodeVersion}/node-{NodeVersion}-darwin-x64.tar.gz";

	private static readonly string LinuxNodeUrl =
		$"https://nodejs.org/dist/{NodeVersion}/node-{NodeVersion}-linux-x64.tar.xz";

	private enum MessageType
	{
		Info,
		Warning,
		Error
	}

	// Display a message with appropriate logging based on type and batch mode
	private static void DisplayMessage(string title, string message, MessageType type)
	{
		// Log message based on type
		switch (type)
		{
			case MessageType.Error:
				Debug.LogError($"{title}: {message}");
				break;
			case MessageType.Warning:
				Debug.LogWarning($"{title}: {message}");
				break;
			default:
				Debug.Log($"{title}: {message}");
				break;
		}

		// Only show dialog if not in batch mode
		if (!Application.isBatchMode)
		{
			EditorUtility.DisplayDialog(title, message, "OK");
		}
	}

	// Ask a question and get yes/no response, with fallback for batch mode
	private static bool DisplayQuestion(string title, string message, string yesText = "Yes", string noText = "No")
	{
		// In batch mode, we can't ask questions, so return a default value
		if (Application.isBatchMode)
		{
			Debug.Log($"{title}: {message} (Automatically choosing No in batch mode)");
			return false;
		}

		return EditorUtility.DisplayDialog(title, message, yesText, noText);
	}

	private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
	private static string ToolsPath => Path.Combine(ProjectRoot, ToolsFolder);
	private static string NodeInstallPath => Application.platform == RuntimePlatform.WindowsEditor ? Path.GetFullPath( Path.Combine(EditorApplication.applicationPath, "..","..", "Editor","Data","PlaybackEngines","WebGLSupport","BuildTools","Emscripten","node"))
		: Path.Combine(ToolsPath, NodeInstallFolder);

	private static string NodeExecutablePath
	{
		get
		{
			if (Application.platform == RuntimePlatform.WindowsEditor)
			{
				return Path.Combine(NodeInstallPath, "node.exe");
			}
			string binFolder = Path.Combine(NodeInstallPath, "bin");
			return Path.Combine(binFolder, "node");
		}
	}

	private static string NpmExecutablePath
	{
		get
		{
			if (Application.platform == RuntimePlatform.WindowsEditor)
			{
				return Path.Combine(NodeInstallPath, "npm.cmd");
			}

			return Path.Combine(NodeInstallPath, "bin", "npm");
		}
	}

	private static string NpxExecutablePath
	{
		get
		{
			if (Application.platform == RuntimePlatform.WindowsEditor)
			{
				return Path.Combine(NodeInstallPath, "npx.cmd");
			}

			return Path.Combine(NodeInstallPath, "bin", "npx");
		}
	}

	//[MenuItem("Tools/Node.js/Install Node.js v12.18.1")]
	public static async void InstallNode()
	{
		try
		{
			// Create directories if they don't exist
			if (!Directory.Exists(ToolsPath))
			{
				Directory.CreateDirectory(ToolsPath);
			}

			if (Directory.Exists(NodeInstallPath))
			{
				if (DisplayQuestion("Node.js Installation",
					    "Node.js installation folder already exists. Do you want to remove it and reinstall?"))
				{
					Directory.Delete(NodeInstallPath, true);
				}
				else
				{
					Debug.Log("Node.js installation canceled by user.");
					return;
				}
			}

			Directory.CreateDirectory(NodeInstallPath);

			// Download and install Node.js based on platform
			bool success = await DownloadAndInstallNode();

			if (success)
			{
				// Wait a moment to ensure file system operations are complete
				await Task.Delay(500);

				// Verify installation once more
				if (IsNodeInstalled())
				{
					/*
				    DisplayMessage("Node.js Installation",
				        $"Node.js {NodeVersion} with npm {NpmVersion} has been successfully installed.",
				        MessageType.Info);
				        */

					// Verify installation details
					VerifyNodeInstallation();
				}
				else
				{
					string errorMsg = "Node.js installation seems incomplete. Please check the log for details.";
					Debug.LogError(errorMsg);
					DisplayMessage("Installation Warning", errorMsg, MessageType.Warning);
				}
			}
			else
			{
				DisplayMessage("Installation Failed",
					"Failed to install Node.js. Please check the log for details.",
					MessageType.Error);
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error installing Node.js: {ex.Message}\n{ex.StackTrace}");
			DisplayMessage("Installation Error",
				$"Failed to install Node.js: {ex.Message}",
				MessageType.Error);
		}
	}

	private static async Task<bool> DownloadAndInstallNode()
	{
		string downloadUrl = "";
		string archivePath = "";

		// Determine platform-specific URL and archive path
		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			downloadUrl = WindowsNodeUrl;
			archivePath = Path.Combine(NodeInstallPath, "node.zip");
		}
		else if (Application.platform == RuntimePlatform.OSXEditor)
		{
			downloadUrl = MacNodeUrl;
			archivePath = Path.Combine(NodeInstallPath, "node.tar.gz");

			// Create the bin directory to ensure it exists for symbolic links
			Directory.CreateDirectory(Path.Combine(NodeInstallPath, "bin"));
		}
		else if (Application.platform == RuntimePlatform.LinuxEditor)
		{
			downloadUrl = LinuxNodeUrl;
			archivePath = Path.Combine(NodeInstallPath, "node.tar.xz");
		}
		else
		{
			Debug.LogError("Unsupported platform for Node.js installation");
			return false;
		}

		try
		{
			// Download Node.js archive
			Debug.Log($"Downloading Node.js from {downloadUrl}...");
			EditorUtility.DisplayProgressBar("Node.js Installation", "Downloading Node.js...", 0.3f);

			using (WebClient client = new WebClient())
			{
				await client.DownloadFileTaskAsync(new Uri(downloadUrl), archivePath);
			}

			// Extract archive
			Debug.Log("Extracting Node.js archive...");
			EditorUtility.DisplayProgressBar("Node.js Installation", "Extracting Node.js...", 0.6f);

			await Task.Run(() => ExtractArchive(archivePath));

			// Validate files were properly extracted
			if (Application.platform == RuntimePlatform.OSXEditor)
			{
				string nodeBin = Path.Combine(NodeInstallPath, "bin", "node");
				string npmScript = Path.Combine(NodeInstallPath, "bin", "npm");
				string npmJsPath = Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin", "npm-cli.js");

				Debug.Log($"Validating extracted files on macOS:");
				Debug.Log($"- Node binary exists: {File.Exists(nodeBin)}");
				Debug.Log($"- npm script exists: {File.Exists(npmScript)}");
				Debug.Log($"- npm-cli.js exists: {File.Exists(npmJsPath)}");

				// If files don't exist, extraction might have created a nested directory
				if (!File.Exists(nodeBin) || !File.Exists(npmScript))
				{
					string[] dirs = Directory.GetDirectories(NodeInstallPath);
					if (dirs.Length > 0)
					{
						Debug.Log("Nested directory detected, fixing structure...");
						string nestedDir = dirs[0];
						string nestedBinDir = Path.Combine(nestedDir, "bin");

						if (Directory.Exists(nestedBinDir))
						{
							Debug.Log($"Moving files from {nestedBinDir} to {Path.Combine(NodeInstallPath, "bin")}");
							CopyDirectory(nestedBinDir, Path.Combine(NodeInstallPath, "bin"));
						}

						string nestedLibDir = Path.Combine(nestedDir, "lib");
						if (Directory.Exists(nestedLibDir))
						{
							Debug.Log($"Moving files from {nestedLibDir} to {Path.Combine(NodeInstallPath, "lib")}");
							CopyDirectory(nestedLibDir, Path.Combine(NodeInstallPath, "lib"));
						}

						// Delete the nested directory after copying
						Directory.Delete(nestedDir, true);
					}
				}
			}

			// Cleanup archive file
			if (File.Exists(archivePath))
			{
				File.Delete(archivePath);
			}

			EditorUtility.ClearProgressBar();

			// Verify installation
			bool isInstalled = IsNodeInstalled();
			Debug.Log(
				$"Installation verification: Node.js is {(isInstalled ? "successfully installed" : "not installed properly")}");

			return isInstalled;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error during Node.js installation: {ex.Message}");
			EditorUtility.ClearProgressBar();
			return false;
		}
	}

	private static void ExtractArchive(string archivePath)
	{
		Debug.Log($"Extracting archive: {archivePath} to {NodeInstallPath}");

		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			// Extract ZIP archive
			Debug.Log("Extracting ZIP on Windows...");
			ZipFile.ExtractToDirectory(archivePath, NodeInstallPath);

			// Move files from the nested directory to the root
			string[] nestedDirs = Directory.GetDirectories(NodeInstallPath);
			if (nestedDirs.Length > 0)
			{
				string nestedNodeDir = nestedDirs[0];
				Debug.Log($"Found nested directory: {nestedNodeDir}, moving contents to root...");
				CopyDirectory(nestedNodeDir, NodeInstallPath);
				Directory.Delete(nestedNodeDir, true);
			}
			else
			{
				Debug.Log("No nested directories found after extraction.");
			}
		}
		else
		{
			// Create a temporary extraction directory
			string tempExtractDir = Path.Combine(NodeInstallPath, "temp_extract");
			Directory.CreateDirectory(tempExtractDir);

			// Determine the right tar command based on platform and archive type
			string tarArgs;
			if (Application.platform == RuntimePlatform.OSXEditor)
			{
				tarArgs = $"-xzf \"{archivePath}\" -C \"{tempExtractDir}\"";
				Debug.Log($"Using tar command on macOS: tar {tarArgs}");
			}
			else // Linux
			{
				tarArgs = $"-xf \"{archivePath}\" -C \"{tempExtractDir}\"";
				Debug.Log($"Using tar command on Linux: tar {tarArgs}");
			}

			// Extract with tar command
			Debug.Log($"Running tar command: tar {tarArgs}");
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "tar",
				Arguments = tarArgs,
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			using (Process process = Process.Start(psi))
			{
				string output = process.StandardOutput.ReadToEnd();
				string error = process.StandardError.ReadToEnd();
				process.WaitForExit();

				if (process.ExitCode != 0)
				{
					throw new Exception($"Failed to extract archive: {error}");
				}

				Debug.Log($"Tar command output: {output}");
				if (!string.IsNullOrEmpty(error))
				{
					Debug.LogWarning($"Tar command error output: {error}");
				}
			}

			// Find the extracted node directory
			string[] extractedDirs = Directory.GetDirectories(tempExtractDir);
			if (extractedDirs.Length == 0)
			{
				Debug.LogError("No directories found in extraction folder");
				throw new Exception("Failed to extract Node.js archive correctly");
			}

			string extractedNodeDir = extractedDirs[0];
			Debug.Log($"Found extracted Node.js directory: {extractedNodeDir}");

			// Create proper directory structure
			if (!Directory.Exists(Path.Combine(NodeInstallPath, "bin")))
			{
				Directory.CreateDirectory(Path.Combine(NodeInstallPath, "bin"));
			}

			if (!Directory.Exists(Path.Combine(NodeInstallPath, "lib")))
			{
				Directory.CreateDirectory(Path.Combine(NodeInstallPath, "lib"));
			}

			// Move contents to final destination
			CopyDirectory(extractedNodeDir, NodeInstallPath);

			// Clean up temp directory
			Directory.Delete(tempExtractDir, true);

			// Ensure executable permissions on macOS/Linux
			if (Application.platform != RuntimePlatform.WindowsEditor)
			{
				string nodeBin = Path.Combine(NodeInstallPath, "bin", "node");
				string npmBin = Path.Combine(NodeInstallPath, "bin", "npm");
				string npxBin = Path.Combine(NodeInstallPath, "bin", "npx");

				SetExecutablePermission(nodeBin);
				SetExecutablePermission(npmBin);
				SetExecutablePermission(npxBin);
			}
		}

		// Log the extracted files for debugging
		LogExtractedFiles();
	}

	private static void SetExecutablePermission(string filePath)
	{
		if (File.Exists(filePath))
		{
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = "chmod",
					Arguments = $"+x \"{filePath}\"",
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};

				using (Process process = Process.Start(psi))
				{
					process.WaitForExit();
					if (process.ExitCode != 0)
					{
						Debug.LogWarning($"Failed to set executable permission on {filePath}");
					}
					else
					{
						Debug.Log($"Set executable permission on {filePath}");
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error setting executable permission: {ex.Message}");
			}
		}
		else
		{
			Debug.LogWarning($"Cannot set permission on non-existent file: {filePath}");
		}
	}

	private static void LogExtractedFiles()
	{
		Debug.Log("Listing extracted files for verification:");

		// Check main directories
		if (Directory.Exists(Path.Combine(NodeInstallPath, "bin")))
		{
			string[] binFiles = Directory.GetFiles(Path.Combine(NodeInstallPath, "bin"));
			Debug.Log($"Files in bin directory ({binFiles.Length}):");
			foreach (string file in binFiles)
			{
				Debug.Log($" - {Path.GetFileName(file)}");
			}
		}
		else
		{
			Debug.LogWarning("bin directory not found");
		}

		if (Application.platform != RuntimePlatform.WindowsEditor)
		{
			if (Directory.Exists(Path.Combine(NodeInstallPath, "lib", "node_modules", "npm")))
			{
				Debug.Log("npm module directory found");

				string npmCliPath = Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin", "npm-cli.js");
				Debug.Log($"npm-cli.js exists: {File.Exists(npmCliPath)}");
			}
			else
			{
				Debug.LogWarning("npm module directory not found");
			}
		}
		else
		{
			Debug.Log($"node.exe exists: {File.Exists(Path.Combine(NodeInstallPath, "node.exe"))}");
			Debug.Log($"npm.cmd exists: {File.Exists(Path.Combine(NodeInstallPath, "npm.cmd"))}");
		}
	}

	private static void CopyDirectory(string sourceDir, string targetDir)
	{
		foreach (string file in Directory.GetFiles(sourceDir))
		{
			string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
			File.Copy(file, targetFile, true);
		}

		foreach (string dir in Directory.GetDirectories(sourceDir))
		{
			string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
			Directory.CreateDirectory(targetSubDir);
			CopyDirectory(dir, targetSubDir);
		}
	}

	//[MenuItem("Tools/Node.js/Verify Installation")]
	public static void VerifyNodeInstallation()
	{
		if (!IsNodeInstalled())
		{
			DisplayMessage("Node.js Installation",
				"Node.js is not installed. Please install it first.",
				MessageType.Warning);
			return;
		}

		try
		{
			// Set up environment paths before running commands
			SetupEnvironmentPaths();

			// Check node version
			string nodeVersion = RunCommand(NodeExecutablePath, "--version");
			Debug.Log($"Node.js version: {nodeVersion}");

			// Check npm version - on macOS, we need a special handling
			string npmVersion;
			if (Application.platform == RuntimePlatform.OSXEditor)
			{
				// On macOS, directly use node to run the npm-cli.js file
				string npmJsPath = Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin", "npm-cli.js");
				if (File.Exists(npmJsPath))
				{
					npmVersion = RunCommand(NodeExecutablePath, $"\"{npmJsPath}\" --version");
				}
				else
				{
					// Fallback to the regular approach
					npmVersion = RunCommand(NpmExecutablePath, "--version");
				}
			}
			else
			{
				npmVersion = RunCommand(NpmExecutablePath, "--version");
			}

			Debug.Log($"NPM version: {npmVersion}");

			// Check if npx exists
			bool npxExists = File.Exists(NpxExecutablePath);
			string npxVersion = "Not Found";

			if (npxExists)
			{
				try
				{
					if (Application.platform == RuntimePlatform.OSXEditor)
					{
						// On macOS, directly use node to run the npx-cli.js file
						string npxJsPath = Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin",
							"npx-cli.js");
						if (File.Exists(npxJsPath))
						{
							npxVersion = RunCommand(NodeExecutablePath, $"\"{npxJsPath}\" --version");
						}
						else
						{
							// Fallback to the regular approach
							npxVersion = RunCommand(NpxExecutablePath, "--version");
						}
					}
					else
					{
						npxVersion = RunCommand(NpxExecutablePath, "--version");
					}
				}
				catch (Exception)
				{
					npxExists = false;
				}
			}

			Debug.Log($"NPX version: {npxVersion}");
/*
			DisplayMessage("Node.js Installation Verification",
				$"Node.js: {nodeVersion}\nNPM: {npmVersion}\nNPX: {(npxExists ? npxVersion : "Not Found")}",
				MessageType.Info);
*/
			Debug.Log(
				$"Node.js installation verified:\nNode version: {nodeVersion}\nNPM version: {npmVersion}\nNPX: {(npxExists ? npxVersion : "Not Found")}");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error verifying Node.js installation: {ex.Message}");
			DisplayMessage("Verification Error",
				$"Failed to verify Node.js installation: {ex.Message}",
				MessageType.Error);
		}
	}

	/// <summary>
	/// Sets up required environment variables for Node.js
	/// </summary>
	private static void SetupEnvironmentPaths()
	{
		// Add the Node.js bin directory to the PATH variable
		string binPath = Application.platform == RuntimePlatform.WindowsEditor
			? NodeInstallPath
			: Path.Combine(NodeInstallPath, "bin");

		Environment.SetEnvironmentVariable("PATH",
			$"{binPath}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}");

		// For macOS/Linux, set NODE_PATH to find modules
		if (Application.platform != RuntimePlatform.WindowsEditor)
		{
			string nodeModulesPath = Path.Combine(NodeInstallPath, "lib", "node_modules");
			Environment.SetEnvironmentVariable("NODE_PATH", nodeModulesPath);
		}

		//Debug.Log($"Environment PATH set to: {Environment.GetEnvironmentVariable("PATH")}");
	}

	private static string RunCommand(string command, string arguments)
	{
		using Process process = StartCommandAndReturnProcess(command,arguments);
		string output = process.StandardOutput.ReadToEnd().Trim();
		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			string error = process.StandardError.ReadToEnd();
			throw new Exception($"Command failed with error: {error}");
		}

		return output;
	}

	private static Process StartCommandAndReturnProcess(string command, string arguments, DataReceivedEventHandler onOutputReceived=null, DataReceivedEventHandler onErrorReceived=null)
	{
		ProcessStartInfo psi = new ProcessStartInfo
		{
			FileName = command,
			Arguments = arguments,
			CreateNoWindow = true,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};

		// Setup environment variables specifically for Node.js
		psi.EnvironmentVariables["PATH"] = NodeInstallPath + Path.PathSeparator +
		                                   (Application.platform == RuntimePlatform.WindowsEditor ? "" : "/bin" + Path.PathSeparator) +
		                                   psi.EnvironmentVariables["PATH"];

		// For macOS/Linux, set NODE_PATH to the lib/node_modules
		if (Application.platform != RuntimePlatform.WindowsEditor)
		{
			string nodeModulesPath = Path.Combine(NodeInstallPath, "lib", "node_modules");
			psi.EnvironmentVariables["NODE_PATH"] = nodeModulesPath;

			// On macOS, npm and npx are shell scripts that rely on /usr/bin/env to find node
			// So if we're executing npm/npx directly, we need to handle it differently
			if ((command.EndsWith("/npm") || command.EndsWith("/npx")) &&
			    Application.platform == RuntimePlatform.OSXEditor)
			{
				// For npm/npx on macOS, we'll directly call node with the npm/npx JS file
				string jsFile = command.EndsWith("/npm")
					? Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin", "npm-cli.js")
					: Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin", "npx-cli.js");

				if (File.Exists(jsFile))
				{
					psi.FileName = Path.Combine(NodeInstallPath, "bin", "node");
					psi.Arguments = $"\"{jsFile}\" {arguments}";
				}
			}
		}

		Process process = new Process();
		process.StartInfo = psi;
		
		// Attach event handlers before starting the process
		if (onOutputReceived != null)
		{
			process.OutputDataReceived += onOutputReceived;
		}
		
		if (onErrorReceived != null)
		{
			process.ErrorDataReceived += onErrorReceived;
		}
		
		process.Start();
		
		// Begin asynchronous reading if event handlers are provided
		if (onOutputReceived != null)
		{
			process.BeginOutputReadLine();
		}
		
		if (onErrorReceived != null)
		{
			process.BeginErrorReadLine();
		}

		return process;
	}

	/// <summary>
	/// Checks if Node.js is installed in the expected location
	/// </summary>
	public static bool IsNodeInstalled()
	{
		if (Application.platform != RuntimePlatform.OSXEditor)
		{
			return true; //windows has a working npm install
		}
		bool nodeExists = File.Exists(NodeExecutablePath);
		bool npmExists = false;

		// For macOS, we need to check the npm JS file
		if (Application.platform == RuntimePlatform.OSXEditor)
		{
			string npmJsPath = Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin", "npm-cli.js");
			npmExists = File.Exists(npmJsPath);
		}
		else
		{
			npmExists = File.Exists(NpmExecutablePath);
		}

		bool isInstalled = nodeExists && npmExists;

		//Debug.Log($"Node.js installation check: Node exists: {nodeExists}, NPM exists: {npmExists}, Overall: {isInstalled}");

		return isInstalled;
	}

	/// <summary>
	/// Gets the path to the Node.js executable
	/// </summary>
	public static string GetNodePath()
	{
		return IsNodeInstalled() ? NodeExecutablePath : null;
	}

	/// <summary>
	/// Gets the path to the npm executable
	/// </summary>
	public static string GetNpmPath()
	{
		return IsNodeInstalled() ? NpmExecutablePath : null;
	}

	/// <summary>
	/// Gets the path to the npx executable
	/// </summary>
	public static string GetNpxPath()
	{
		return IsNodeInstalled() && File.Exists(NpxExecutablePath) ? NpxExecutablePath : null;
	}

	/// <summary>
	/// Runs a Node.js script
	/// </summary>
	public static string RunNodeScript(string scriptPath, string arguments = "")
	{
		if (!IsNodeInstalled())
		{
			throw new Exception("Node.js is not installed. Please install it first.");
		}

		// Set up environment paths
		SetupEnvironmentPaths();

		return RunCommand(NodeExecutablePath, $"\"{scriptPath}\" {arguments}");
	}
	/// <summary>
	/// Runs a Node.js script and return the raw process
	/// </summary>
	public static Process RunNodeScriptAndReturnProcess(string scriptPath, string arguments = "",DataReceivedEventHandler onOutputReceived=null, DataReceivedEventHandler onErrorReceived=null)
	{
		if (!IsNodeInstalled())
		{
			throw new Exception("Node.js is not installed. Please install it first.");
		}

		// Set up environment paths
		SetupEnvironmentPaths();

		return StartCommandAndReturnProcess(NodeExecutablePath, $"\"{scriptPath}\" {arguments}",onOutputReceived,onErrorReceived);
	}

	/// <summary>
	/// Runs an npm command
	/// </summary>
	public static string RunNpmCommand(string command, string arguments = "")
	{
		if (!IsNodeInstalled())
		{
			throw new Exception("Node.js is not installed. Please install it first.");
		}

		// Set up environment paths
		SetupEnvironmentPaths();

		if (Application.platform == RuntimePlatform.OSXEditor)
		{
			// On macOS, directly use node to run the npm-cli.js file
			string npmJsPath = Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin", "npm-cli.js");
			if (File.Exists(npmJsPath))
			{
				return RunCommand(NodeExecutablePath, $"\"{npmJsPath}\" {command} {arguments}");
			}
		}

		return RunCommand(NpmExecutablePath, $"{command} {arguments}");
	}

	/// <summary>
	/// Runs an npx command
	/// </summary>
	public static string RunNpxCommand(string command, string arguments = "")
	{
		if (!IsNodeInstalled())
		{
			throw new Exception("Node.js is not installed. Please install it first.");
		}

		if (!File.Exists(NpxExecutablePath))
		{
			throw new Exception("npx is not installed. Please verify your Node.js installation.");
		}

		// Set up environment paths
		SetupEnvironmentPaths();

		if (Application.platform == RuntimePlatform.OSXEditor)
		{
			// On macOS, directly use node to run the npx-cli.js file
			string npxJsPath = Path.Combine(NodeInstallPath, "lib", "node_modules", "npm", "bin", "npx-cli.js");
			if (File.Exists(npxJsPath))
			{
				return RunCommand(NodeExecutablePath, $"\"{npxJsPath}\" {command} {arguments}");
			}
		}

		return RunCommand(NpxExecutablePath, $"{command} {arguments}");
	}

}