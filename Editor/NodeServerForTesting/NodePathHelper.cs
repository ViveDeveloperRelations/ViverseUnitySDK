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
    /// Gets the Unity Editor installation root directory.
    /// Example: C:/Program Files/Unity/Hub/Editor/6000.0.34f1/
    /// </summary>
    private static string GetUnityEditorRoot()
    {
        return Path.GetFullPath(Path.Combine(EditorApplication.applicationPath, "..\\..\\"));
    }

    /// <summary>
    /// Gets the path to the WebGL Emscripten BuildTools directory.
    /// Example: C:/Program Files/Unity/Hub/Editor/6000.0.34f1/Editor/Data/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/
    /// </summary>
    private static string GetEmscriptenBuildToolsPath()
    {
        return Path.Combine(GetUnityEditorRoot(), "Editor\\Data\\PlaybackEngines\\WebGLSupport\\BuildTools\\Emscripten");
    }

    /// <summary>
    /// Gets the full path of a given executable in the Emscripten BuildTools directory.
    /// </summary>
    private static string NodeDir(string executableName)
    {
        string path = Path.Combine(GetEmscriptenBuildToolsPath(), "node", executableName);
        Assert.IsTrue(File.Exists(path), $"File not found: {path}");
        return path;
    }

    public static string NodePath => NodeDir("node.exe");
    public static string NpxPath => NodeDir("npx.cmd");
    public static string NpmPath => NodeDir("npm.cmd");

    /// <summary>
    /// Runs a command synchronously and returns the output.
    /// </summary>
    /// <param name="exePath">The full path to the executable.</param>
    /// <param name="arguments">The arguments to pass.</param>
    /// <returns>The standard output of the process.</returns>
    public static string RunCommand(string exePath, string arguments)
    {
        if (!File.Exists(exePath))
        {
            Debug.LogError($"Executable not found: {exePath}");
            return null;
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process { StartInfo = psi })
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"Error running {exePath} {arguments}: {error}");
            }

            return output;
        }
    }

    /*
    [MenuItem("Tools/Run Node Version")]
    public static void RunNodeVersion()
    {
        string output = RunCommand(NodePath, "--version");
        Debug.Log("Node Version: " + output);
    }

    [MenuItem("Tools/Run NPM Version")]
    public static void RunNpmVersion()
    {
        string output = RunCommand(NpmPath, "--version");
        Debug.Log("NPM Version: " + output);
    }

    [MenuItem("Tools/Run NPX Version")]
    public static void RunNpxVersion()
    {
        string output = RunCommand(NpxPath, "--version");
        Debug.Log("NPX Version: " + output);
    }
    */
}
