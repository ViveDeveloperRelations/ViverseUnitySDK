using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

/// <summary>
/// Utility for installing UniVRM packages required for the VIVERSE SDK VRM extension.
/// This class works even when the packages are not yet installed.
/// </summary>
public class VRMPackageInstaller
{
    // UniVRM packages to install with their Git URLs
    private static readonly Dictionary<string, string> VRMPackages = new Dictionary<string, string>
    {
        { "com.vrmc.gltf", "https://github.com/vrm-c/UniVRM.git?path=/Assets/UniGLTF#v0.128.2" },
        { "com.vrmc.univrm", "https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM#v0.128.2" },
        { "com.vrmc.vrm", "https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM10#v0.128.2" }
    };

    private static AddRequest _currentRequest;
    private static int _packagesToInstall;
    private static int _packagesInstalled;
    private static string _currentPackage;
    private static Action<bool, string> _onComplete;
    private static ListRequest _listRequest;

    /// <summary>
    /// Checks if UniVRM packages are installed in the project with async callback.
    /// </summary>
    public static void AreVRMPackagesInstalledAsync(Action<bool> callback)
    {
        _listRequest = Client.List();
        EditorApplication.update += CheckVRMPackagesInstalled;

        void CheckVRMPackagesInstalled()
        {
            if (!_listRequest.IsCompleted) return;

            EditorApplication.update -= CheckVRMPackagesInstalled;

            if (_listRequest.Status == StatusCode.Success)
            {
                bool allInstalled = true;
                foreach (var package in VRMPackages.Keys)
                {
                    bool found = false;
                    foreach (var installedPackage in _listRequest.Result)
                    {
                        if (installedPackage.name == package)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        allInstalled = false;
                        break;
                    }
                }
                callback(allInstalled);
            }
            else
            {
                Debug.LogError($"Error checking VRM packages: {_listRequest.Error?.message}");
                callback(false);
            }
        }
    }

    /// <summary>
    /// Gets the installation status of UniVRM packages with async callback.
    /// </summary>
    public static void GetPackageStatusAsync(Action<Dictionary<string, bool>> callback)
    {
        var result = new Dictionary<string, bool>();
        foreach (string package in VRMPackages.Keys)
        {
            result[package] = false;
        }

        _listRequest = Client.List();
        EditorApplication.update += CheckPackageStatus;

        void CheckPackageStatus()
        {
            if (!_listRequest.IsCompleted) return;

            EditorApplication.update -= CheckPackageStatus;

            if (_listRequest.Status == StatusCode.Success)
            {
                foreach (PackageInfo installedPackage in _listRequest.Result)
                {
                    foreach (string package in VRMPackages.Keys)
                    {
                        if (installedPackage.name == package)
                        {
                            result[package] = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                Debug.LogError($"Error getting package status: {_listRequest.Error?.message}");
            }

            callback(result);
        }
    }

    /// <summary>
    /// Installs all required UniVRM packages.
    /// </summary>
    /// <param name="onComplete">Callback when installation is complete.</param>
    public static void InstallVRMPackages(Action<bool, string> onComplete = null)
    {
        _onComplete = onComplete;
        _packagesToInstall = VRMPackages.Count;
        _packagesInstalled = 0;

        EditorUtility.DisplayProgressBar("Installing VRM Packages", "Starting installation...", 0f);
        InstallNextPackage();
    }

    private static void InstallNextPackage()
    {
        if (_packagesInstalled >= _packagesToInstall)
        {
            EditorUtility.ClearProgressBar();
            _onComplete?.Invoke(true, "All packages installed successfully.");
            return;
        }

        string packageName = new List<string>(VRMPackages.Keys)[_packagesInstalled];
        string packageUrl = VRMPackages[packageName];
        _currentPackage = packageName;

        float progress = (float)_packagesInstalled / _packagesToInstall;
        EditorUtility.DisplayProgressBar("Installing VRM Packages", $"Installing {packageName}...", progress);

        _currentRequest = Client.Add(packageUrl);
        EditorApplication.update += PackageInstallProgress;
    }

    private static void PackageInstallProgress()
    {
        if (!_currentRequest.IsCompleted) return;
        EditorApplication.update -= PackageInstallProgress;

        switch (_currentRequest.Status)
        {
            case StatusCode.Success:
                Debug.Log($"Successfully installed {_currentPackage}");
                _packagesInstalled++;
                InstallNextPackage();
                break;
            case StatusCode.Failure:
            default:
                EditorUtility.ClearProgressBar();
                string error = $"Failed to install {_currentPackage}: {_currentRequest.Error?.message}";
                Debug.LogError(error);
                _onComplete?.Invoke(false, error);
                break;
        }
    }
}
