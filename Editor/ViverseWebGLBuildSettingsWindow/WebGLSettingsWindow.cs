using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using ViverseWebGLAPI;

/// <summary>
/// Editor window for configuring WebGL build settings and server setup
/// </summary>
public class WebGLSettingsWindow : EditorWindow
{
    // UI Elements
    private Toggle _decompressionFallbackToggle;
    private Label _currentPlatformLabel;
    private VisualElement _container;

    // Shader UI Elements
    private VisualElement _shaderContainer;
    private Label _shaderStatusLabel;
    private Button _addShadersButton;

    // VRM Package UI Elements
    private VisualElement _vrmPackageContainer;
    private Label _vrmPackageStatusLabel;
    private Button _installVRMPackagesButton;
    private VisualElement _packageStatusList;
    private bool _isInstallingPackages = false;
    private bool _refreshShaderStatusAfterInstall = false;
    private bool _refreshPackageStatusAfterInstall = false;

    // Server setup UI elements
    private VisualElement _serverSetupContainer;
    private Label _mkcertStatusLabel;
    private Button _checkMkcertButton;
    private Label _certStatusLabel;
    private Button _generateCertButton;
    private Label _nodeModulesStatusLabel;
    private Button _installNodeModulesButton;
    private Label _serverScriptStatusLabel;
    private Button _copyServerScriptButton;
    private Toggle _serverRunningToggle;

    // Manager instances
    private WebGLShaderManager _shaderManager;
    private readonly WebGLServerManager _serverManager = new WebGLServerManager();

    // Update tracking
    private const float UPDATE_INTERVAL_IN_SECONDS = 1.0f;
    private double _lastUpdateTime;

    // Track if we need to refresh UI after package installation
    private bool _pendingUiRefresh = false;

    // Track if the initial package status has been checked
    private bool _initialPackageStatusChecked = false;

    [MenuItem("Tools/WebGL Build Settings")]
    public static void ShowWindow()
    {
        WebGLSettingsWindow window = GetWindow<WebGLSettingsWindow>();
        window.titleContent = new GUIContent("WebGL Build Settings");
        window.minSize = new Vector2(500, 600);
    }

    private void OnEnable()
    {
        try
        {
            // Always create a WebGLShaderManager instance - it is compatible with both
            // scenarios (with or without VRM packages installed)
            _shaderManager = new WebGLShaderManager();
            InitializeWindow();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize WebGL Settings Window: {e}");
        }
    }

    private void InitializeWindow()
    {
        // Get the path to the UXML and USS files
        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
        if (string.IsNullOrEmpty(scriptPath))
        {
            Debug.LogError("Could not find script path");
            return;
        }

        string directory = System.IO.Path.GetDirectoryName(scriptPath);
        string uxmlPath = System.IO.Path.Combine(directory, "WebGLSettingsWindow.uxml");
        string ussPath = System.IO.Path.Combine(directory, "WebGLSettingsWindow.uss");

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);

        if (visualTree == null)
        {
            Debug.LogError($"Failed to load UXML file at path: {uxmlPath}");
            return;
        }

        // Clear and setup root
        rootVisualElement.Clear();
        if (styleSheet != null) rootVisualElement.styleSheets.Add(styleSheet);
        visualTree.CloneTree(rootVisualElement);

        // Get references to UI elements
        _container = rootVisualElement.Q<VisualElement>("container");
        _decompressionFallbackToggle = rootVisualElement.Q<Toggle>("decompressionFallbackToggle");
		_decompressionFallbackToggle.tooltip = "Disable decompression fallback and set compression format to None to ensure smooth running in some browsers (recommended)";
        _currentPlatformLabel = rootVisualElement.Q<Label>("currentPlatformLabel");
        Button setAllButton = rootVisualElement.Q<Button>("setAllButton");

        // Initialize VRM Package UI elements
        InitializeVRMPackageUI();

        // Initialize shader-specific UI elements
        InitializeShaderUI();

        // Initialize server setup UI elements
        InitializeServerSetupUI();

        // Setup event handlers
        setAllButton.clicked += OnSetAllButtonClicked;
        _decompressionFallbackToggle.RegisterValueChangedCallback(OnDecompressionFallbackChanged);

        // Register for editor updates
        EditorApplication.update += OnEditorUpdate;
        EditorUserBuildSettings.activeBuildTargetChanged += OnBuildTargetChanged;

        // Register for script reloads to update UI after packages are installed and imported
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

        // Immediately check package status on window open
        CheckInitialPackageStatus();

        // Update UI
        UpdateUI();
    }

    private void CheckInitialPackageStatus()
    {
        // Use a delayed call to ensure UI is properly set up first
        EditorApplication.delayCall += () => {
            // Check VRM packages status immediately on window open
            _refreshPackageStatusAfterInstall = true;
            _refreshShaderStatusAfterInstall = true;
            _initialPackageStatusChecked = true;

            // This will trigger the refresh in the next UpdateUI call
            _pendingUiRefresh = true;
        };
    }

    private void OnAfterAssemblyReload()
    {
        // After a domain reload (which happens after package installation),
        // make sure we refresh our UI and check for VRM packages
        EditorApplication.delayCall += () => {
            _shaderManager = new WebGLShaderManager();

            // Set both refresh flags to ensure both sections update
            _refreshShaderStatusAfterInstall = true;
            _refreshPackageStatusAfterInstall = true;
            _pendingUiRefresh = true;

            UpdateUI();
        };
    }

    private void InitializeShaderUI()
    {
        // Create shader container
        _shaderContainer = new VisualElement { name = "shader-container" };
        _shaderContainer.AddToClassList("settings-group");

        // Add section header
        var shaderHeader = new Label("Shader Settings");
        shaderHeader.AddToClassList("section-header");
        _shaderContainer.Add(shaderHeader);

        // Add shader status label
        _shaderStatusLabel = new Label { name = "shader-status-label" };
        _shaderStatusLabel.AddToClassList("status-label");
        _shaderContainer.Add(_shaderStatusLabel);

        // The shader manager is now always available
        _addShadersButton = new Button(() => {
            _shaderManager.AddMissingShaders();
            UpdateShaderStatus();
        })
        {
            text = "Add Missing Shaders",
            name = "add-shaders-button"
        };
        _addShadersButton.AddToClassList("action-button");
        _shaderContainer.Add(_addShadersButton);

        // Info message about VRM packages if not installed
        // Note: We'll only display this when we've checked package status and confirmed packages aren't installed
        var missingPackagesMessage = new Label("VRM packages are not installed. Basic shaders will be added; install VRM packages for enhanced avatar rendering support.");
        missingPackagesMessage.AddToClassList("info-message");
        missingPackagesMessage.style.display = DisplayStyle.None; // Hide by default until we know package status
        _shaderContainer.Add(missingPackagesMessage);

        // Add to main container
        _container.Add(_shaderContainer);
    }

    private void InitializeVRMPackageUI()
    {
        _vrmPackageContainer = new VisualElement { name = "vrm-package-container" };
        _vrmPackageContainer.AddToClassList("settings-group");

        // Add section header
        var vrmHeader = new Label("VRM Package Installation");
        vrmHeader.AddToClassList("section-header");
        _vrmPackageContainer.Add(vrmHeader);

        // Add description
        var vrmDescription = new Label("Install UniVRM packages (v0.128.2) required for Viverse avatar rendering support.");
        vrmDescription.AddToClassList("package-description");
        _vrmPackageContainer.Add(vrmDescription);

        // Add package status label
        _vrmPackageStatusLabel = new Label { name = "vrm-package-status-label" };
        _vrmPackageStatusLabel.AddToClassList("status-label");
        _vrmPackageContainer.Add(_vrmPackageStatusLabel);

        // Set initial status message
        _vrmPackageStatusLabel.text = "Checking package status...";
        _vrmPackageStatusLabel.style.color = new Color(0.5f, 0.5f, 0.5f);

        // Add package status list
        _packageStatusList = new VisualElement { name = "package-status-list" };
        _packageStatusList.AddToClassList("package-list");
        _vrmPackageContainer.Add(_packageStatusList);

        // Add install button
        _installVRMPackagesButton = new Button(InstallVRMPackages)
        {
            text = "Install VRM Packages",
            name = "install-vrm-packages-button"
        };
        _installVRMPackagesButton.AddToClassList("action-button");
        _installVRMPackagesButton.style.display = DisplayStyle.None; // Hide by default until we've checked
        _vrmPackageContainer.Add(_installVRMPackagesButton);

        // Add to main container
        _container.Add(_vrmPackageContainer);
    }

    private IEnumerator m_CurrentInstallCoroutine = null;
    private void InstallVRMPackages()
    {
        if (_isInstallingPackages) return;

        _isInstallingPackages = true;
        _installVRMPackagesButton.SetEnabled(false);
        _installVRMPackagesButton.text = "Installing...";

        IEnumerator InstallCoroutine()
        {
	        var installVRMPackagesReturn = new VRMPackageInstaller.VRMPackageInstallCoroutineReturn();
			yield return VRMPackageInstaller.InstallVRMPackagesCoroutine(installVRMPackagesReturn);
			_isInstallingPackages = false;
			m_CurrentInstallCoroutine = null;
			_installVRMPackagesButton.SetEnabled(true);
			_installVRMPackagesButton.text = "Install VRM Packages";
			bool success = installVRMPackagesReturn.success;
			if (success)
			{
				EditorUtility.DisplayDialog("Installation Complete",
					"UniVRM packages installed successfully. ", "OK");

				// Set flag to refresh shader and package status after packages are installed
				_pendingUiRefresh = true;
				_refreshShaderStatusAfterInstall = true;
				_refreshPackageStatusAfterInstall = true;

				// Force a domain reload to apply the new packages
				AssetDatabase.Refresh();
			}
			else
			{
				EditorUtility.DisplayDialog("Installation Failed",
					$"Failed to install UniVRM packages {installVRMPackagesReturn.message}", "OK");
				UpdateVRMPackageStatus();
			}
        }

        if (m_CurrentInstallCoroutine != null)
        {
	        Debug.LogWarning("Install packages already running, ignoring extra request");
	        return;
        }
        m_CurrentInstallCoroutine = InstallCoroutine();
        // Use helper class to install packages
        EditorCoroutineUtility.StartCoroutine(m_CurrentInstallCoroutine);
    }

    private EditorCoroutine GetPackageStatusCoroutine;
    private void UpdateVRMPackageStatus()
    {
	    if (GetPackageStatusCoroutine != null)
	    {
		    EditorCoroutineUtility.StopCoroutine(GetPackageStatusCoroutine);
		    GetPackageStatusCoroutine = null;
	    }

	    IEnumerator UpdateVRMPackagesList()
	    {
		    var statusResult = new VRMPackageInstaller.PackageStatusCoroutineReturn();
		    yield return VRMPackageInstaller.GetPackageStatusCoroutine(statusResult);
			Dictionary<string,bool> packageStatus = statusResult.packageStatus;
			bool allInstalled = packageStatus.Values.All(installed => installed);
			if (allInstalled)
            {
                _vrmPackageStatusLabel.text = "✓ All VRM packages are installed";
                _vrmPackageStatusLabel.style.color = new Color(0, 0.7f, 0);
                _installVRMPackagesButton.style.display = DisplayStyle.None;
            }
            else
            {
                _vrmPackageStatusLabel.text = "⚠ Some VRM packages are missing";
                _vrmPackageStatusLabel.style.color = new Color(0.7f, 0.5f, 0);
                _installVRMPackagesButton.style.display = DisplayStyle.Flex;
            }

            // Update individual package status
            _packageStatusList.Clear();

            foreach (var package in packageStatus)
            {
                var packageContainer = new VisualElement();
                packageContainer.AddToClassList("package-item");

                var statusIcon = new Label(package.Value ? "✓" : "⚠");
                statusIcon.AddToClassList("status-icon");
                statusIcon.style.color = package.Value ? new Color(0, 0.7f, 0) : new Color(0.7f, 0.5f, 0);

                var packageName = new Label(package.Key);
                packageName.AddToClassList("package-name");

                packageContainer.Add(statusIcon);
                packageContainer.Add(packageName);
                _packageStatusList.Add(packageContainer);
            }

            // Show/hide additional info message based on installation status
            bool vrmPackagesInstalled = allInstalled;
            var infoMessage = _packageStatusList.Q<Label>(className: "package-info");

            // Add extra information if packages aren't installed
            if (!vrmPackagesInstalled && infoMessage == null)
            {
                infoMessage = new Label("Installing these packages will enable avatar rendering and shader management features.");
                infoMessage.AddToClassList("package-info");
                _packageStatusList.Add(infoMessage);
            }
            else if (vrmPackagesInstalled && infoMessage != null)
            {
                _packageStatusList.Remove(infoMessage);
            }

            // Update shader info message visibility based on package status
            var shaderInfoMessage = _shaderContainer.Q<Label>(className: "info-message");
            if (shaderInfoMessage != null)
            {
                shaderInfoMessage.style.display = vrmPackagesInstalled ? DisplayStyle.None : DisplayStyle.Flex;
            }

            // When package status changes, we should update shader status too
            UpdateShaderStatus();
	    }
	    GetPackageStatusCoroutine = EditorCoroutineUtility.StartCoroutine(UpdateVRMPackagesList());
    }

    private void InitializeServerSetupUI()
    {
        _serverSetupContainer = new VisualElement { name = "server-setup-container" };
        _serverSetupContainer.AddToClassList("settings-group");

        // Add server setup header
        var serverSetupHeader = new Label("HTTPS Server Setup");
        serverSetupHeader.AddToClassList("section-header");
        _serverSetupContainer.Add(serverSetupHeader);

        // Step 1: Check mkcert installation
        var step1Container = CreateStepContainer("1");
        _mkcertStatusLabel = CreateStatusLabel("Checking mkcert installation...");
        _checkMkcertButton = CreateButton("Check mkcert Installation", () => {
            bool installed = _serverManager.IsMkcertInstalled();
            if (!installed)
            {
                EditorUtility.DisplayDialog("mkcert Not Found",
                    "mkcert is not installed or not in your PATH. Please install mkcert globally and try again.\n\n" +
                    "Windows: Use Chocolatey or Scoop.\n" +
                    "macOS: Use Homebrew.\n" +
                    "Linux: Follow instructions at https://github.com/FiloSottile/mkcert",
                    "OK");
            }
            UpdateServerSetupUI();
        });
        step1Container.Add(_mkcertStatusLabel);
        step1Container.Add(_checkMkcertButton);
        _serverSetupContainer.Add(step1Container);

        // Step 2: Generate SSL certificate
        var step2Container = CreateStepContainer("2");
        _certStatusLabel = CreateStatusLabel("SSL certificates not generated");
        _generateCertButton = CreateButton("Generate SSL Certificate", () => {
            _serverManager.GenerateSSLCertificates();
            UpdateServerSetupUI();
        });
        step2Container.Add(_certStatusLabel);
        step2Container.Add(_generateCertButton);
        _serverSetupContainer.Add(step2Container);

        // Step 3: Install Node modules
        var step3Container = CreateStepContainer("3");
        _nodeModulesStatusLabel = CreateStatusLabel("Node modules not installed");
        _installNodeModulesButton = CreateButton("Install Node Modules", () => {
            _serverManager.InstallNodeModules();
            UpdateServerSetupUI();
        });
        step3Container.Add(_nodeModulesStatusLabel);
        step3Container.Add(_installNodeModulesButton);
        _serverSetupContainer.Add(step3Container);

        // Step 4: Copy server script
        var step4Container = CreateStepContainer("4");
        _serverScriptStatusLabel = CreateStatusLabel("Server script not copied");
        _copyServerScriptButton = CreateButton("Copy Server Script", () => {
            _serverManager.CopyServerScript();
            UpdateServerSetupUI();
        });
        step4Container.Add(_serverScriptStatusLabel);
        step4Container.Add(_copyServerScriptButton);
        _serverSetupContainer.Add(step4Container);

        // Step 5: Start/Stop server
        var step5Container = CreateStepContainer("5");
        _serverRunningToggle = new Toggle("HTTPS Server Running");
        _serverRunningToggle.AddToClassList("server-toggle");
        _serverRunningToggle.RegisterValueChangedCallback(evt => {
            if (evt.newValue)
                _serverManager.StartServer();
            else
                _serverManager.StopServer();

            UpdateServerSetupUI();
        });
        step5Container.Add(_serverRunningToggle);
        _serverSetupContainer.Add(step5Container);

        // Add container to main UI
        _container.Add(_serverSetupContainer);
    }

    private VisualElement CreateStepContainer(string stepNumber)
    {
        var container = new VisualElement();
        container.AddToClassList("step-container");

        var stepLabel = new Label($"Step {stepNumber}:");
        stepLabel.AddToClassList("step-label");
        container.Add(stepLabel);

        return container;
    }

    private Label CreateStatusLabel(string initialText)
    {
        var label = new Label(initialText);
        label.AddToClassList("status-label");
        return label;
    }

    private Button CreateButton(string text, System.Action clickHandler)
    {
        var button = new Button(clickHandler) { text = text };
        button.AddToClassList("setup-button");
        return button;
    }

    private void UpdateShaderStatus()
	{
	    // Get missing shaders count using the WebGLShaderManager
	    int numMissingShaders = _shaderManager.GetMissingShadersCount();

	    // Check if URP shader variants are missing
	    bool missingURPVariants = _shaderManager.IsMissingPreloadedVariants();

	    // Determine if all shader resources are included
	    bool hasAllShaders = numMissingShaders == 0 && !missingURPVariants;

	    // Check if VRM packages are installed using preprocessor directives
	    #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
	    bool vrmPackagesInstalled = true;
	    bool isURP = UniVRMEssentialShadersForPlatformHelper.IsUsingUniversalRenderPipeline();
	    #else
	    bool vrmPackagesInstalled = false;
	    bool isURP = false;
	    #endif

	    if (hasAllShaders)
	    {
	        _shaderStatusLabel.text = "✓ All required shaders are included";
	        _shaderStatusLabel.style.color = new Color(0, 0.7f, 0);
	        _addShadersButton.style.display = DisplayStyle.None;
	    }
	    else
	    {
	        int issueCount = numMissingShaders;
	        if (missingURPVariants) issueCount++; // Count missing URP variant collection as an issue

	        _shaderStatusLabel.text = $"⚠ Missing {issueCount} required shader resource(s)";
	        _shaderStatusLabel.style.color = new Color(0.7f, 0.5f, 0);
	        _addShadersButton.style.display = DisplayStyle.Flex;
	    }

	    // Hide/show the information message based on VRM installation status
	    var infoMessage = _shaderContainer.Q<Label>(className: "info-message");
	    if (infoMessage != null)
	    {
	        infoMessage.style.display = vrmPackagesInstalled ? DisplayStyle.None : DisplayStyle.Flex;
	    }

	    // Add URP variant info if using URP
	    if (isURP)
	    {
	        // Check if the URP info message already exists
	        var urpInfoMessage = _shaderContainer.Q<Label>("urp-variant-info");
	        bool hasURPShaderVariants = !missingURPVariants;

	        if (urpInfoMessage == null)
	        {
	            // Create URP variant info message
	            urpInfoMessage = new Label(hasURPShaderVariants ?
	                "✓ URP Shader Variant Collection is included" :
	                "⚠ URP Shader Variant Collection needs to be added to preloaded assets");
	            urpInfoMessage.name = "urp-variant-info";
	            urpInfoMessage.AddToClassList("status-label");
	            urpInfoMessage.style.color = hasURPShaderVariants ? new Color(0, 0.7f, 0) : new Color(0.7f, 0.5f, 0);
	            urpInfoMessage.style.marginTop = 8;
	            _shaderContainer.Add(urpInfoMessage);
	        }
	        else
	        {
	            // Update existing URP variant info message
	            urpInfoMessage.text = hasURPShaderVariants ?
	                "✓ URP Shader Variant Collection is included" :
	                "⚠ URP Shader Variant Collection needs to be added to preloaded assets";
	            urpInfoMessage.style.color = hasURPShaderVariants ? new Color(0, 0.7f, 0) : new Color(0.7f, 0.5f, 0);
	        }
	    }
	    else
	    {
	        // Remove URP variant info if not using URP
	        var urpInfoMessage = _shaderContainer.Q<Label>("urp-variant-info");
	        if (urpInfoMessage != null)
	        {
	            _shaderContainer.Remove(urpInfoMessage);
	        }
	    }
	}
    
	// Check if the URP shader variant collection is in preloaded assets
	private bool CheckURPShaderVariantPreloaded()
	{
		// GUID of the URP shader variant collection to check
		const string URP_SHADER_VARIANT_GUID = "179d48094d5ea464da56bf6fe34974ae";

		// Get preloaded assets
		var preloadedAssets = PlayerSettings.GetPreloadedAssets();
		if (preloadedAssets == null)
			return false;

		// Get the asset path from GUID
		string assetPath = AssetDatabase.GUIDToAssetPath(URP_SHADER_VARIANT_GUID);
		if (string.IsNullOrEmpty(assetPath))
			return false; // Asset not found in project

		// Load the asset from path
		var shaderVariantCollection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(assetPath);
		if (shaderVariantCollection == null)
			return false; // Asset couldn't be loaded

		// Check if this collection is in preloaded assets
		foreach (var asset in preloadedAssets)
		{
			if (asset == shaderVariantCollection)
				return true;
		}

		return false;
	}

    private void UpdateServerSetupUI()
    {
        WebGLServerManager.ServerSetupStatus status = _serverManager.GetServerSetupStatus();

        // Update Step 1: mkcert installation
        if (status.MkcertInstalled)
        {
            _mkcertStatusLabel.text = "✓ mkcert is installed";
            _mkcertStatusLabel.style.color = new Color(0, 0.7f, 0);
            _checkMkcertButton.style.display = DisplayStyle.None;
        }
        else
        {
            _mkcertStatusLabel.text = "⚠ mkcert needs to be installed globally";
            _mkcertStatusLabel.style.color = new Color(0.7f, 0.5f, 0);
            _checkMkcertButton.style.display = DisplayStyle.Flex;
        }

        // Update Step 2: SSL certificates
        _generateCertButton.SetEnabled(status.MkcertInstalled);
        if (status.CertificatesGenerated)
        {
            _certStatusLabel.text = "✓ SSL certificates generated";
            _certStatusLabel.style.color = new Color(0, 0.7f, 0);
            _generateCertButton.style.display = DisplayStyle.None;
        }
        else
        {
            _certStatusLabel.text = "⚠ SSL certificates not generated";
            _certStatusLabel.style.color = new Color(0.7f, 0.5f, 0);
            _generateCertButton.style.display = DisplayStyle.Flex;
        }

        // Update Step 3: Node modules
        _installNodeModulesButton.SetEnabled(status.CertificatesGenerated);
        if (status.NodeModulesInstalled)
        {
            _nodeModulesStatusLabel.text = "✓ Node modules installed";
            _nodeModulesStatusLabel.style.color = new Color(0, 0.7f, 0);
            _installNodeModulesButton.style.display = DisplayStyle.None;
        }
        else
        {
            _nodeModulesStatusLabel.text = "⚠ Node modules not installed";
            _nodeModulesStatusLabel.style.color = new Color(0.7f, 0.5f, 0);
            _installNodeModulesButton.style.display = DisplayStyle.Flex;
        }

        // Update Step 4: Server script
        _copyServerScriptButton.SetEnabled(status.NodeModulesInstalled);
        if (status.ServerScriptCopied)
        {
            _serverScriptStatusLabel.text = "✓ Server script copied";
            _serverScriptStatusLabel.style.color = new Color(0, 0.7f, 0);
            _copyServerScriptButton.style.display = DisplayStyle.None;
        }
        else
        {
            _serverScriptStatusLabel.text = "⚠ Server script not copied";
            _serverScriptStatusLabel.style.color = new Color(0.7f, 0.5f, 0);
            _copyServerScriptButton.style.display = DisplayStyle.Flex;
        }

        // Update Step 5: Server toggle
        _serverRunningToggle.SetEnabled(status.ServerScriptCopied);
        _serverRunningToggle.SetValueWithoutNotify(status.ServerRunning);
    }

    private void UpdateUI()
    {
        if (_container == null || _currentPlatformLabel == null || _decompressionFallbackToggle == null)
            return;

        bool isWebGLPlatform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;

        _currentPlatformLabel.text = $"Current Platform: {EditorUserBuildSettings.activeBuildTarget}";
        _decompressionFallbackToggle.SetValueWithoutNotify(!PlayerSettings.WebGL.decompressionFallback);
        _container.style.opacity = isWebGLPlatform ? 1f : 0.5f;
        _decompressionFallbackToggle.SetEnabled(isWebGLPlatform);

        if (isWebGLPlatform)
        {
            // Check if we need to refresh shader status
            if (_refreshShaderStatusAfterInstall)
            {
                EditorApplication.delayCall += () => {
                    if (_shaderManager == null)
                    {
                        _shaderManager = new WebGLShaderManager();
                    }
                    UpdateShaderStatus();
                    _refreshShaderStatusAfterInstall = false;
                };
            }
            else
            {
                UpdateShaderStatus();
            }

            // Check if we need to refresh package status
            if (_refreshPackageStatusAfterInstall)
            {
                EditorApplication.delayCall += () => {
                    UpdateVRMPackageStatus();
                    _refreshPackageStatusAfterInstall = false;
                };
            }
            else if (!_initialPackageStatusChecked)
            {
                // Check package status on first load
                UpdateVRMPackageStatus();
                _initialPackageStatusChecked = true;
            }
        }

        // Update server setup UI
        UpdateServerSetupUI();

        // Reset pending UI refresh flag if needed
        if (_pendingUiRefresh)
        {
            EditorApplication.delayCall += () => {
                UpdateUI();
                _pendingUiRefresh = false;
            };
        }
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorUserBuildSettings.activeBuildTargetChanged -= OnBuildTargetChanged;
        AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;

        if (_decompressionFallbackToggle != null)
        {
            _decompressionFallbackToggle.UnregisterValueChangedCallback(OnDecompressionFallbackChanged);
        }
    }

    private void OnEditorUpdate()
    {
        if (EditorApplication.timeSinceStartup - _lastUpdateTime >= UPDATE_INTERVAL_IN_SECONDS)
        {
            UpdateUI();
            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }
    }

    private void OnBuildTargetChanged()
    {
        UpdateUI();
    }

    private void OnDecompressionFallbackChanged(ChangeEvent<bool> evt)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            _decompressionFallbackToggle.SetValueWithoutNotify(!evt.newValue);
            Debug.LogWarning("Please switch to WebGL platform before changing WebGL settings.");
            EditorUtility.DisplayDialog("Invalid Platform",
                "Please switch to WebGL platform before changing WebGL settings.", "OK");
            return;
        }

        PlayerSettings.WebGL.decompressionFallback = !evt.newValue;
		
		// Also set the compression format to None when decompression fallback is disabled
		if (evt.newValue) // If toggle is checked (disable decompression fallback)
		{
			PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
		}
		
        AssetDatabase.SaveAssets();
    }

    private void OnSetAllButtonClicked()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUtility.DisplayDialog("Invalid Platform",
                "Please switch to WebGL platform before applying WebGL settings.", "OK");
            return;
        }

        PlayerSettings.WebGL.decompressionFallback = !_decompressionFallbackToggle.value;
		
		// Also set the compression format to None when decompression fallback is disabled
		if (_decompressionFallbackToggle.value) // If toggle is checked (disable decompression fallback)
		{
			PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
		}

        if (_shaderManager != null)
        {
            bool shadersAdded = _shaderManager.AddMissingShaders();
            if (shadersAdded)
            {
                Debug.Log("Added missing shaders to the Always Included Shaders list");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("WebGL Build Settings Applied");

        // Update UI to reflect changes
        UpdateShaderStatus();
        UpdateVRMPackageStatus();

        #if !UNI_VRM_INSTALLED || !UNI_GLTF_INSTALLED
        if (EditorUtility.DisplayDialog("VRM Packages Not Installed",
            "VRM packages are not installed. Installing these packages will provide enhanced shader support for avatar rendering. Would you like to install them now?",
            "Install VRM Packages", "Skip for now"))
        {
            InstallVRMPackages();
        }
        #endif
    }
}