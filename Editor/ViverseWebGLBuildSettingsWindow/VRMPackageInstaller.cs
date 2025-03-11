using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using ViverseWebGLAPI;
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

	public class PackagesInstalledCoroutineReturn
	{
		public bool packagesInstalled;
		public bool listRequestCompleted;
	}
	public static IEnumerator AreVRMPackagesInstalledCoroutine(PackagesInstalledCoroutineReturn packagesInstalledCoroutineReturn)
	{
		_listRequest = Client.List();
		while (!_listRequest.IsCompleted)
		{
			yield return null;
		}
		//i think if there's a domain reload, the list request times out... so maybe simplify the ifdefs to the most greedy item, which is com.vrmc.vrm
		packagesInstalledCoroutineReturn.listRequestCompleted = _listRequest.IsCompleted;

		if (_listRequest.Status != StatusCode.Success)
		{
			Debug.LogError($"Error checking VRM packages: {_listRequest.Error?.message}");
			yield break;
		}

		bool allInstalled = true;
		foreach (string package in VRMPackages.Keys)
		{
			bool found = false;
			foreach (PackageInfo installedPackage in _listRequest.Result)
			{
				if (installedPackage.name != package) continue;
				found = true;
				break;
			}

			if (found) continue;
			allInstalled = false;
			break;
		}

		packagesInstalledCoroutineReturn.packagesInstalled = allInstalled;
	}

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
                foreach (string package in VRMPackages.Keys)
                {
                    bool found = false;
                    foreach (PackageInfo installedPackage in _listRequest.Result)
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
    public class PackageStatusCoroutineReturn
    {
	    public bool requestCompleted = false;
	    public Dictionary<string, bool> packageStatus = new Dictionary<string, bool>();
	    public string errorMessage = null;
    }

    public static IEnumerator GetPackageStatusCoroutine(PackageStatusCoroutineReturn result)
    {
	    // Initialize result with all packages marked as false
	    foreach (string package in VRMPackages.Keys)
	    {
		    result.packageStatus[package] = false;
	    }

	    // Start the list request
	    ListRequest listRequest = Client.List();

	    // Wait for the request to complete
	    while (!listRequest.IsCompleted)
	    {
		    yield return null;
	    }

	    result.requestCompleted = true;

	    // Process the results
	    if (listRequest.Status == StatusCode.Success)
	    {
		    foreach (PackageInfo installedPackage in listRequest.Result)
		    {
			    foreach (string package in VRMPackages.Keys)
			    {
				    if (installedPackage.name == package)
				    {
					    result.packageStatus[package] = true;
					    break;
				    }
			    }
		    }
	    }
	    else
	    {
		    result.errorMessage = $"Error getting package status: {listRequest.Error?.message}";
		    Debug.LogError(result.errorMessage);
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

        //EditorUtility.DisplayProgressBar("Installing VRM Packages", "Starting installation...", 0f);
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
    public class VRMPackageInstallCoroutineReturn
    {
	    public bool success;
	    public string message;
	    public Dictionary<string, bool> installedPackages = new Dictionary<string, bool>();
    }

    public static IEnumerator InstallVRMPackagesCoroutine(VRMPackageInstallCoroutineReturn result)
    {
	    int packagesInstalled = 0;
	    List<string> packageNamesToInstall = new List<string>(VRMPackages.Keys);
	    var packageStatusReturn = new PackageStatusCoroutineReturn();
		yield return GetPackageStatusCoroutine(packageStatusReturn);
		if(packageStatusReturn.requestCompleted == false || packageStatusReturn.errorMessage != null)
		{
			result.success = false;
			result.message = packageStatusReturn.errorMessage;
			Debug.LogWarning("Package status request failed when trying to install packages");
			yield break;
		}
		foreach ((string alreadyInstalledPackageName, bool installed) in packageStatusReturn.packageStatus)
		{
			if (!installed) continue;
			if (packageNamesToInstall.Contains(alreadyInstalledPackageName))
			{
				packageNamesToInstall.Remove(alreadyInstalledPackageName);
			}
		}
		Request[] requests = new Request[packageNamesToInstall.Count];
		for (int i = 0; i < packageNamesToInstall.Count; i++)
		{
			string packageName = packageNamesToInstall[i];
			string packageUrl = VRMPackages[packageName];
			// Start the package installation
			requests[i] = Client.Add(packageUrl);
			Debug.Log("Requesting to install " + packageName);
		}

		for (int i = 0; i < requests.Length; i++)
		{
			Request request = requests[i];
			// Wait for the request to complete
			while (!request.IsCompleted)
			{
				yield return null;
			}
			string packageName = packageNamesToInstall[i];
			// Check the result
			if (request.Status == StatusCode.Success)
			{
				packagesInstalled++;
				result.installedPackages[packageName] = true;
				Debug.Log($"Successfully installed {packageName}");
			}
			else
			{
				string error = $"Failed to install {packageName}: {request.Error?.message}";
				Debug.LogError(error);
				result.success = false;
				result.message = error;
				yield break;
			}
		}

	    // All packages installed successfully
	    result.success = true;
	    result.message = "All packages installed successfully.";
    }

}
