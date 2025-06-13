// Global variables for core functionality
let currentToken = null;
let sdkLoaded = false;
let sdkLoadAttempted = false;
let authClientInitialized = false;
let currentClientConfig = null;
let autoSaveTimeout = null;
let hasAttemptedLogin = false;

// LocalStorage key for settings
const SETTINGS_KEY = "viverse_sdk_settings";

// Viverse SDK configuration
const VIVERSE_SDK_URL =
  "https://www.viverse.com/static-assets/viverse-sdk/1.2.9/viverse-sdk.umd.js";

// Show loading indicator initially
document.addEventListener("DOMContentLoaded", () => {
  document.getElementById("loadingIndicator").style.display = "block";
});

// Function to dynamically load the Viverse SDK
function loadViverseSDK() {
  return new Promise((resolve, reject) => {
    if (sdkLoadAttempted) {
      if (sdkLoaded) {
        resolve();
      } else {
        reject(new Error("SDK load was already attempted and failed"));
      }
      return;
    }

    sdkLoadAttempted = true;

    // Check if SDK is already loaded (in case script tag exists)
    if (typeof globalThis.viverse !== "undefined") {
      sdkLoaded = true;
      resolve();
      return;
    }

    const script = document.createElement("script");
    script.src = VIVERSE_SDK_URL;
    script.async = true;

    script.onload = () => {
      // Double-check that the SDK is actually available
      if (typeof globalThis.viverse !== "undefined") {
        sdkLoaded = true;
        console.log("Viverse SDK loaded successfully from:", VIVERSE_SDK_URL);
        resolve();
      } else {
        const error = new Error(
          "SDK script loaded but viverse object not found"
        );
        console.error("SDK load error:", error);
        reject(error);
      }
    };

    script.onerror = (event) => {
      const error = new Error(
        `Failed to load Viverse SDK from ${VIVERSE_SDK_URL}`
      );
      console.error("SDK load error:", error, event);
      reject(error);
    };

    script.ontimeout = () => {
      const error = new Error("Viverse SDK load timed out");
      console.error("SDK load error:", error);
      reject(error);
    };

    // Set a timeout for the script load
    setTimeout(() => {
      if (!sdkLoaded) {
        script.remove();
        reject(new Error("Viverse SDK load timed out after 10 seconds"));
      }
    }, 10000);

    document.head.appendChild(script);
  });
}

// Function to check if SDK is loaded
function checkSDKLoaded() {
  return typeof globalThis.viverse !== "undefined" && sdkLoaded;
}

// Function to enable all buttons when SDK is ready
function enableAllButtons() {
  const buttons = document.querySelectorAll(
    "button:not(#saveAllSettings):not(#clearAllSettings):not(#loadSettings):not(#generateSessionId)"
  );
  buttons.forEach((button) => {
    button.disabled = false;
  });
}

// Function to wait for SDK to load with additional verification
function waitForSDK() {
  return new Promise((resolve, reject) => {
    if (checkSDKLoaded()) {
      resolve();
      return;
    }

    let attempts = 0;
    const maxAttempts = 100; // 10 seconds maximum wait time after script loads

    const checkInterval = setInterval(() => {
      attempts++;

      if (checkSDKLoaded()) {
        clearInterval(checkInterval);
        resolve();
      } else if (attempts >= maxAttempts) {
        clearInterval(checkInterval);
        reject(
          new Error(
            "SDK verification failed - viverse object not available after script load"
          )
        );
      }
    }, 100); // Check every 100ms
  });
}

// Initialize SDK when ready
async function initializeSDK() {
  try {
    // First, attempt to load the SDK
    await loadViverseSDK();

    // Then wait for it to be fully available
    await waitForSDK();

    // Hide loading indicator and show success
    document.getElementById("loadingIndicator").style.display = "none";
    const statusEl = document.getElementById("sdkStatus");
    statusEl.style.display = "block";
    statusEl.textContent = "Viverse SDK loaded successfully!";
    statusEl.classList.remove("error");

    enableAllButtons();
    console.log("Viverse SDK initialization complete");
  } catch (error) {
    // Hide loading indicator and show error
    document.getElementById("loadingIndicator").style.display = "none";
    const statusEl = document.getElementById("sdkStatus");
    statusEl.style.display = "block";

    let errorMessage = "Failed to load Viverse SDK";
    if (error.message.includes("timed out")) {
      errorMessage += " (connection timeout)";
    } else if (error.message.includes("Failed to load")) {
      errorMessage += " (network error)";
    } else {
      errorMessage += `: ${error.message}`;
    }

    statusEl.textContent = errorMessage;
    statusEl.classList.add("error");

    console.error("Failed to initialize Viverse SDK:", error);

    // Provide user guidance
    const additionalInfo = document.createElement("div");
    additionalInfo.style.marginTop = "10px";
    additionalInfo.style.fontSize = "14px";
    additionalInfo.innerHTML = `
            <strong>Troubleshooting:</strong><br>
            • Check your internet connection<br>
            • Ensure ${VIVERSE_SDK_URL} is accessible<br>
            • Try refreshing the page<br>
            • Check browser console for detailed error information
        `;
    statusEl.appendChild(additionalInfo);
  }
}

// Helper function to display results
function displayResult(elementId, data) {
  const resultEl = document.getElementById(elementId);

  if (typeof data === "object") {
    resultEl.textContent = JSON.stringify(data, null, 2);
  } else {
    resultEl.textContent = data;
  }
  console.log(resultEl.textContent);
}

// Helper function to update the UI with user info
function updateUserInfo(accountId, token) {
  const userInfoEl = document.getElementById("userInfo");
  const userIdEl = document.getElementById("userId");
  const tokenInfoEl = document.getElementById("tokenInfo");

  if (accountId && token) {
    userInfoEl.style.display = "block";
    userIdEl.textContent = `Account ID: ${accountId}`;
    tokenInfoEl.textContent = `Token: ${token}`;
  } else {
    userInfoEl.style.display = "none";
    userIdEl.textContent = "";
    tokenInfoEl.textContent = "";
  }
}

// Helper function to update client configuration status
function updateClientConfigStatus() {
  const configChanged = hasClientConfigChanged();
  const clientIdInput = document.getElementById("clientId");
  const domainInput = document.getElementById("domain");
  const cookieDomainInput = document.getElementById("cookieDomain");
  const initAuthButton = document.getElementById("initAuth");

  const currentClientId = clientIdInput.value;
  const previousClientId = currentClientConfig?.clientId;

  // Check if this is a valid client ID change (from one valid ID to another)
  const isValidClientIdChange =
    authClientInitialized &&
    isValidClientId(previousClientId) &&
    isValidClientId(currentClientId) &&
    previousClientId !== currentClientId;

  // Visual indicator for changed configuration
  const inputs = [clientIdInput, domainInput, cookieDomainInput];
  inputs.forEach((input) => {
    if (isValidClientIdChange) {
      // Red border for client ID changes that require reload
      input.style.borderColor = "#e74c3c"; // Red border
      input.style.backgroundColor = "#fdedec"; // Light red background
    } else if (configChanged && authClientInitialized) {
      input.style.borderColor = "#f39c12"; // Orange border for other changes
      input.style.backgroundColor = "#fef9e7"; // Light yellow background
    } else {
      input.style.borderColor = "";
      input.style.backgroundColor = "";
    }
  });

  // Update button text based on status
  if (!authClientInitialized) {
    initAuthButton.textContent = "Initialize Auth Client";
    initAuthButton.style.backgroundColor = "#3498db"; // Blue
  } else if (isValidClientIdChange) {
    initAuthButton.textContent = "Auto-Reload Pending...";
    initAuthButton.style.backgroundColor = "#e74c3c"; // Red
    initAuthButton.disabled = true; // Disable during pending reload
  } else if (configChanged) {
    initAuthButton.textContent = "Re-initialize Auth Client";
    initAuthButton.style.backgroundColor = "#f39c12"; // Orange
    initAuthButton.disabled = false;
  } else {
    initAuthButton.textContent = "Auth Client Initialized ✓";
    initAuthButton.style.backgroundColor = "#27ae60"; // Green
    initAuthButton.disabled = false;
  }
}

// Load saved settings from localStorage
function loadSavedSettings() {
  try {
    const savedSettings = localStorage.getItem(SETTINGS_KEY);

    if (savedSettings) {
      const settings = JSON.parse(savedSettings);
      console.log("Loading saved settings:", settings);

      // Set all form values from saved settings
      if (settings.clientId)
        document.getElementById("clientId").value = settings.clientId;
      if (settings.domain)
        document.getElementById("domain").value = settings.domain;
      if (settings.cookieDomain)
        document.getElementById("cookieDomain").value = settings.cookieDomain;
      if (settings.loginState)
        document.getElementById("loginState").value = settings.loginState;
      if (settings.appId)
        document.getElementById("appId").value = settings.appId;
      if (settings.leaderboardName)
        document.getElementById("leaderboardName").value =
          settings.leaderboardName;
      if (settings.leaderboardRegion)
        document.getElementById("leaderboardRegion").value =
          settings.leaderboardRegion;
      if (settings.leaderboardTimeRange)
        document.getElementById("leaderboardTimeRange").value =
          settings.leaderboardTimeRange;
      if (settings.leaderboardRangeStart !== undefined)
        document.getElementById("leaderboardRangeStart").value =
          settings.leaderboardRangeStart;
      if (settings.leaderboardRangeEnd !== undefined)
        document.getElementById("leaderboardRangeEnd").value =
          settings.leaderboardRangeEnd;
      if (settings.leaderboardAroundUser !== undefined)
        document.getElementById("leaderboardAroundUser").checked =
          settings.leaderboardAroundUser;
      if (settings.scoreValue)
        document.getElementById("scoreValue").value = settings.scoreValue;
      if (settings.achievementName)
        document.getElementById("achievementName").value =
          settings.achievementName;
      if (settings.achievementStatus)
        document.getElementById("achievementStatus").value =
          settings.achievementStatus;
      if (settings.avatarId)
        document.getElementById("avatarId").value = settings.avatarId;
      // Set multiplayer defaults with saved values or fallbacks
      if (settings.playerName) {
        document.getElementById("playerName").value = settings.playerName;
      } else {
        // Auto-generate player name if not saved
        document.getElementById("playerName").value =
          "Player_" + Math.random().toString(36).substr(2, 6);
      }
      if (settings.playerLevel) {
        document.getElementById("playerLevel").value = settings.playerLevel;
      }
      if (settings.playerSkill) {
        document.getElementById("playerSkill").value = settings.playerSkill;
      }
      if (settings.roomName)
        document.getElementById("roomName").value = settings.roomName;
      if (settings.roomMode)
        document.getElementById("roomMode").value = settings.roomMode;
      if (settings.maxPlayers)
        document.getElementById("maxPlayers").value = settings.maxPlayers;
      if (settings.minPlayers)
        document.getElementById("minPlayers").value = settings.minPlayers;
      if (settings.joinRoomId)
        document.getElementById("joinRoomId").value = settings.joinRoomId;
      if (settings.entityId)
        document.getElementById("entityId").value = settings.entityId;
      if (settings.actionName)
        document.getElementById("actionName").value = settings.actionName;
      if (settings.actionMessage)
        document.getElementById("actionMessage").value = settings.actionMessage;
      if (settings.actionId)
        document.getElementById("actionId").value = settings.actionId;

      // Load login attempt flag
      if (settings.hasAttemptedLogin !== undefined)
        hasAttemptedLogin = settings.hasAttemptedLogin;

      displayResult(
        "settingsResult",
        `Settings loaded successfully:\n${JSON.stringify(settings, null, 2)}`
      );

      // Update status after loading settings
      setTimeout(updateClientConfigStatus, 100);
    } else {
      // Set default values if no saved settings
      const defaults = {
        clientId: "YOUR_CLIENT_ID",
        domain: "account.htcvive.com",
        leaderboardRegion: "global",
        leaderboardTimeRange: "alltime",
        leaderboardRangeStart: 0,
        leaderboardRangeEnd: 100,
        achievementStatus: "true",
      };

      document.getElementById("clientId").value = defaults.clientId;
      document.getElementById("domain").value = defaults.domain;
      document.getElementById("leaderboardRegion").value =
        defaults.leaderboardRegion;
      document.getElementById("leaderboardTimeRange").value =
        defaults.leaderboardTimeRange;
      document.getElementById("leaderboardRangeStart").value =
        defaults.leaderboardRangeStart;
      document.getElementById("leaderboardRangeEnd").value =
        defaults.leaderboardRangeEnd;
      document.getElementById("achievementStatus").value =
        defaults.achievementStatus;

      console.log("No saved settings found, using defaults");
    }
  } catch (error) {
    console.error("Error loading saved settings:", error);
    displayResult("settingsResult", `Error loading settings: ${error.message}`);
  }
}

// Save current settings to localStorage
function saveSettings() {
  try {
    const settings = {
      clientId: document.getElementById("clientId").value,
      domain: document.getElementById("domain").value,
      cookieDomain: document.getElementById("cookieDomain").value,
      loginState: document.getElementById("loginState").value,
      appId: document.getElementById("appId").value,
      leaderboardName: document.getElementById("leaderboardName").value,
      leaderboardRegion: document.getElementById("leaderboardRegion").value,
      leaderboardTimeRange: document.getElementById("leaderboardTimeRange")
        .value,
      leaderboardRangeStart:
        parseInt(document.getElementById("leaderboardRangeStart").value) || 0,
      leaderboardRangeEnd:
        parseInt(document.getElementById("leaderboardRangeEnd").value) || 100,
      leaderboardAroundUser: document.getElementById("leaderboardAroundUser")
        .checked,
      scoreValue: document.getElementById("scoreValue").value,
      achievementName: document.getElementById("achievementName").value,
      achievementStatus: document.getElementById("achievementStatus").value,
      avatarId: document.getElementById("avatarId").value,
      playerName: document.getElementById("playerName").value,
      playerLevel: parseInt(document.getElementById("playerLevel").value) || 1,
      playerSkill: parseInt(document.getElementById("playerSkill").value) || 1,
      roomName: document.getElementById("roomName").value,
      roomMode: document.getElementById("roomMode").value,
      maxPlayers: parseInt(document.getElementById("maxPlayers").value) || 4,
      minPlayers: parseInt(document.getElementById("minPlayers").value) || 2,
      joinRoomId: document.getElementById("joinRoomId").value,
      entityId: document.getElementById("entityId").value,
      actionName: document.getElementById("actionName").value,
      actionMessage: document.getElementById("actionMessage").value,
      actionId: document.getElementById("actionId").value,
      savedAt: new Date().toISOString(),
    };

    localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
    console.log("Settings saved:", settings);
    displayResult(
      "settingsResult",
      `Settings saved successfully at ${new Date().toLocaleString()}:\n${JSON.stringify(
        settings,
        null,
        2
      )}`
    );

    return true;
  } catch (error) {
    console.error("Error saving settings:", error);
    displayResult("settingsResult", `Error saving settings: ${error.message}`);
    return false;
  }
}

// Clear saved settings from localStorage
function clearSettings() {
  try {
    localStorage.removeItem(SETTINGS_KEY);
    console.log("Settings cleared from localStorage");
    displayResult(
      "settingsResult",
      "All saved settings have been cleared from localStorage."
    );

    // Reset to defaults
    loadSavedSettings();

    return true;
  } catch (error) {
    console.error("Error clearing settings:", error);
    displayResult(
      "settingsResult",
      `Error clearing settings: ${error.message}`
    );
    return false;
  }
}

// Display current settings
function displayCurrentSettings() {
  try {
    const savedSettings = localStorage.getItem(SETTINGS_KEY);

    if (savedSettings) {
      const settings = JSON.parse(savedSettings);
      displayResult(
        "settingsResult",
        `Current saved settings:\n${JSON.stringify(settings, null, 2)}`
      );
    } else {
      displayResult(
        "settingsResult",
        "No settings currently saved in localStorage."
      );
    }
  } catch (error) {
    console.error("Error displaying settings:", error);
    displayResult("settingsResult", `Error reading settings: ${error.message}`);
  }
}

// Helper function to check if client configuration has changed
function hasClientConfigChanged() {
  const clientId = document.getElementById("clientId").value;
  const domain = document.getElementById("domain").value;
  const cookieDomain = document.getElementById("cookieDomain").value;

  const newConfig = {
    clientId: clientId,
    domain: domain || "account.htcvive.com",
    cookieDomain: cookieDomain || null,
  };

  if (!currentClientConfig) {
    return true; // No previous config, so it's changed
  }

  return (
    currentClientConfig.clientId !== newConfig.clientId ||
    currentClientConfig.domain !== newConfig.domain ||
    currentClientConfig.cookieDomain !== newConfig.cookieDomain
  );
}

// Helper function to validate client configuration
function validateClientConfig() {
  const clientId = document.getElementById("clientId").value;

  if (!clientId || clientId.trim() === "" || clientId === "YOUR_CLIENT_ID") {
    throw new Error(
      "Please enter a valid Client ID. You can get one from studio.viverse.com"
    );
  }

  return true;
}

// Helper function to check if client ID is valid (not default)
function isValidClientId(clientId) {
  return clientId && clientId.trim() !== "" && clientId !== "YOUR_CLIENT_ID";
}

// Helper function to safely call checkAuth with timeout and Promise handling
async function safeCheckAuth(timeoutMs = 10000) {
  const timeoutPromise = new Promise((_, reject) =>
    setTimeout(
      () =>
        reject(
          new Error(`checkAuth() timed out after ${timeoutMs / 1000} seconds`)
        ),
      timeoutMs
    )
  );

  // Handle the case where checkAuth() might return undefined instead of a Promise
  let checkAuthCall;
  try {
    checkAuthCall = globalThis.viverseClient.checkAuth();

    // If checkAuth returns undefined or non-Promise, handle it
    if (!checkAuthCall || typeof checkAuthCall.then !== "function") {
      console.warn("checkAuth() returned non-Promise value:", checkAuthCall);
      checkAuthCall = Promise.resolve(checkAuthCall);
    }
  } catch (syncError) {
    // If checkAuth throws synchronously, wrap in rejected Promise
    checkAuthCall = Promise.reject(syncError);
  }

  return await Promise.race([checkAuthCall, timeoutPromise]);
}

// Consolidated auth result processing function to eliminate duplication
function processAuthResult(result, contextInfo = 'manual') {
  const context = typeof contextInfo === 'string' ? contextInfo : contextInfo.context || 'manual';
  const subContext = typeof contextInfo === 'object' ? contextInfo.subContext : null;

  if (result) {
    const accessToken = result.access_token;
    const accountId = result.account_id;

    if (accessToken) {
      currentToken = accessToken;
      updateUserInfo(accountId, accessToken);

      const baseMessage = context === 'initialization'
        ? (subContext === 'existing'
          ? 'Auth client already initialized - User authenticated'
          : 'Auth client initialized successfully - User already authenticated')
        : 'User is authenticated';

      return {
        success: true,
        hasToken: true,
        result: result,
        displayData: {
          message: baseMessage,
          accountId: accountId || "Unknown",
          tokenExpiry: result.expires_in || "Unknown",
          token: `${accessToken}`,
          state: result.state || "No state provided",
          clientId: currentClientConfig?.clientId || "Unknown",
        }
      };
    } else {
      updateUserInfo(null, null);
      return {
        success: true,
        hasToken: false,
        result: result,
        displayData: {
          message: "Authentication result found but no access token available",
          accountId: accountId || "Unknown",
          tokenExpiry: result.expires_in || "Unknown",
          token: "No token available",
          state: result.state || "No state provided",
          clientId: currentClientConfig?.clientId || "Unknown",
        }
      };
    }
  } else {
    updateUserInfo(null, null);

    const baseMessage = context === 'initialization'
      ? (subContext === 'existing'
        ? 'Auth client already initialized - No existing authentication'
        : 'Auth client initialized successfully - No existing authentication')
      : 'User is not authenticated';

    return {
      success: false,
      hasToken: false,
      result: null,
      displayData: {
        message: baseMessage,
        clientId: currentClientConfig?.clientId || "Unknown",
      }
    };
  }
}

// Context-aware auth check with processing
async function performAuthCheckWithProcessing(context = 'manual', subContext = null) {
  try {
    const contextMessage = context === 'initialization'
      ? 'Checking existing authentication...'
      : 'Checking authentication status...';

    if (context === 'manual') {
      displayResult("authResult", contextMessage);
    }

    const result = await safeCheckAuth();
    const contextObj = typeof context === 'string' ? { context, subContext } : context;
    const processed = processAuthResult(result, contextObj);

    if (context === 'manual') {
      displayResult("authResult", processed.displayData);
    }

    return processed;

  } catch (error) {
    let errorMessage = `Error checking auth status: ${error.message || error}`;

    if (error.message && error.message.includes("timed out")) {
      errorMessage +=
        "\n\nThis usually indicates:\n• Authentication state corruption (try refreshing the page)\n• Network connectivity issues\n• CORS/cookie configuration problems\n• SDK may be in inconsistent state after failed login redirect";
    } else if (error.message && error.message.includes("fetch")) {
      errorMessage +=
        "\n\nNetwork error - check your internet connection and try again";
    }

    if (!error.message || !error.message.includes("refresh")) {
      errorMessage +=
        "\n\n💡 Try refreshing the page if checkAuth continues to fail";
    }

    if (context === 'manual') {
      displayResult("authResult", errorMessage);
      updateUserInfo(null, null);
    }

    console.error('Auth check error details:', error);
    return { success: false, hasToken: false, error: error };
  }
}

// Helper function to auto-save client configuration
function autoSaveClientConfig() {
  const clientId = document.getElementById("clientId").value;

  // Only auto-save if we have a valid client ID
  if (isValidClientId(clientId)) {
    console.log("Auto-saving client configuration...");
    saveSettings();
    return true;
  }
  return false;
}

// Debounced auto-save for client ID changes
function debouncedAutoSave() {
  const clientIdInput = document.getElementById("clientId");

  // Clear existing timeout
  if (autoSaveTimeout) {
    clearTimeout(autoSaveTimeout);
  }

  // Add visual indicator that auto-save is pending
  if (isValidClientId(clientIdInput.value)) {
    clientIdInput.style.borderRight = "3px solid #f39c12"; // Orange right border
    clientIdInput.title = "Auto-saving in 2 seconds...";
  } else {
    // Clear indicators for invalid client IDs
    clientIdInput.style.borderRight = "";
    clientIdInput.title = "";
  }

  // Set new timeout to auto-save after 2 seconds of no typing
  autoSaveTimeout = setTimeout(() => {
    const clientId = clientIdInput.value;
    if (isValidClientId(clientId)) {
      console.log("Auto-saving client ID after typing pause...");
      autoSaveClientConfig();

      // Update visual indicator
      clientIdInput.style.borderRight = "3px solid #27ae60"; // Green right border
      clientIdInput.title = "Client ID auto-saved ✓";

      // Clear the indicator after 2 seconds
      setTimeout(() => {
        clientIdInput.style.borderRight = "";
        clientIdInput.title = "";
      }, 2000);
    }
  }, 2000);
}

// Helper function to initialize auth client and check auth status
async function initializeAuthClient(forceReinit = false) {
  if (!sdkLoaded) {
    throw new Error("SDK not loaded yet");
  }

  // Validate client configuration
  validateClientConfig();

  // Check if we need to initialize or reinitialize
  const configChanged = hasClientConfigChanged();
  const currentClientId = document.getElementById("clientId").value;
  const previousClientId = currentClientConfig?.clientId;

  // Check if this is a valid client ID change (from one valid ID to another)
  const isValidClientIdChange =
    authClientInitialized &&
    isValidClientId(previousClientId) &&
    isValidClientId(currentClientId) &&
    previousClientId !== currentClientId;

  if (isValidClientIdChange) {
    // For client ID changes between valid IDs, automatically reload
    console.log(
      `Client ID changed from "${previousClientId}" to "${currentClientId}" - auto-saving and reloading...`
    );

    // Auto-save the new configuration
    autoSaveClientConfig();

    // Show brief message before reload
    displayResult(
      "authResult",
      `Client ID changed to "${currentClientId}" - reloading page to clear authentication state...`
    );

    // Reload after short delay to show the message
    setTimeout(() => {
      window.location.reload();
    }, 1000);

    return; // Exit early, page will reload
  }

  if (authClientInitialized && !configChanged && !forceReinit) {
    console.log(
      "Auth client already initialized with same configuration, skipping initialization"
    );
    return await safeCheckAuth();
  }

  if (authClientInitialized && configChanged && !isValidClientIdChange) {
    console.log("Client configuration changed, reinitializing auth client");
    // Clear current state when config changes
    currentToken = null;
    updateUserInfo(null, null);
  }

  const domain = document.getElementById("domain").value;
  const cookieDomain = document.getElementById("cookieDomain").value;

  const config = {
    clientId: currentClientId,
    domain: domain || "account.htcvive.com",
  };

  if (cookieDomain) {
    config.cookieDomain = cookieDomain;
  }

  // Auto-save configuration when initializing with valid client ID
  autoSaveClientConfig();

  // Store current config for future comparison
  currentClientConfig = {
    clientId: config.clientId,
    domain: config.domain,
    cookieDomain: config.cookieDomain || null,
  };

  // Initialize the auth client
  globalThis.viverseClient = new globalThis.viverse.client(config);
  authClientInitialized = true;

  console.log("Auth client initialized with config:", currentClientConfig);

  // Check existing auth status
  const authResult = await safeCheckAuth();
  return authResult;
}

// Setup Core Event Listeners (Authentication, Avatar, Dashboard, Settings)
function setupCoreEventListeners() {
  // Tab functionality
  document.querySelectorAll(".tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      document
        .querySelectorAll(".tab")
        .forEach((t) => t.classList.remove("active"));
      document
        .querySelectorAll(".tab-content")
        .forEach((c) => c.classList.remove("active"));

      tab.classList.add("active");
      const tabName = tab.getAttribute("data-tab");
      document.getElementById(tabName).classList.add("active");
    });
  });

  // ---- Settings Section Event Listeners ----
  document.getElementById("saveAllSettings").addEventListener("click", () => {
    saveSettings();
  });

  document.getElementById("clearAllSettings").addEventListener("click", () => {
    if (
      confirm(
        "Are you sure you want to clear all saved settings? This action cannot be undone."
      )
    ) {
      clearSettings();
    }
  });

  document.getElementById("loadSettings").addEventListener("click", () => {
    loadSavedSettings();
  });

  // Show current settings when settings tab is opened
  document
    .querySelector('[data-tab="settings"]')
    .addEventListener("click", displayCurrentSettings);

  // Add event listeners to monitor client configuration changes
  const clientIdInput = document.getElementById("clientId");
  const domainInput = document.getElementById("domain");
  const cookieDomainInput = document.getElementById("cookieDomain");

  // Special handling for client ID - auto-save and status update
  if (clientIdInput) {
    clientIdInput.addEventListener("input", () => {
      updateClientConfigStatus();
      debouncedAutoSave(); // Auto-save after typing pause
    });
    clientIdInput.addEventListener("change", updateClientConfigStatus);
  }

  // Regular status updates for other config inputs
  [domainInput, cookieDomainInput].forEach((input) => {
    if (input) {
      input.addEventListener("input", updateClientConfigStatus);
      input.addEventListener("change", updateClientConfigStatus);
    }
  });

  // Initial status update
  updateClientConfigStatus();

  // ---- Authentication Section ----
  document.getElementById("initAuth").addEventListener("click", async () => {
    const configChanged = hasClientConfigChanged();
    try {
      displayResult("authResult", "Initializing auth client...");

      // Add timeout protection for the entire initialization process
      const initTimeoutPromise = new Promise((_, reject) =>
        setTimeout(
          () =>
            reject(
              new Error("Auth client initialization timed out after 15 seconds")
            ),
          15000
        )
      );

      const authResult = await Promise.race([
        initializeAuthClient(),
        initTimeoutPromise,
      ]);

      // Handle the auth result using consolidated logic
      if (authResult !== undefined) {
        // initializeAuthClient returns the raw auth result, process it
        const wasConfigChanged = !authClientInitialized || configChanged;
        const subContext = wasConfigChanged ? 'new' : 'existing';
        const processed = processAuthResult(authResult, { context: 'initialization', subContext });

        // Add config status to the display data
        processed.displayData.configStatus = wasConfigChanged
          ? "New configuration applied"
          : "Using existing configuration";

        displayResult("authResult", processed.displayData);
      }

      // Update visual status after successful initialization
      updateClientConfigStatus();
    } catch (error) {
      let errorMessage = `Error initializing auth client: ${
        error.message || error
      }`;

      // Provide specific guidance for initialization failures
      if (error.message && error.message.includes("timed out")) {
        errorMessage +=
          "\n\nInitialization timeout usually indicates:\n• Authentication state corruption (try refreshing the page)\n• Network connectivity issues\n• Invalid Client ID configuration\n• SDK internal state problems";
      }

      // Always suggest page refresh for init failures
      if (!error.message || !error.message.includes("refresh")) {
        errorMessage +=
          "\n\n💡 Try refreshing the page if initialization continues to fail";
      }

      displayResult("authResult", errorMessage);
      console.error("Auth client initialization error details:", error);
    }
  });

  document.getElementById("checkAuth").addEventListener("click", async () => {
    if (!sdkLoaded) {
      displayResult("authResult", "SDK not loaded yet");
      return;
    }

    if (!authClientInitialized || !globalThis.viverseClient) {
      displayResult(
        "authResult",
        'Auth client not initialized. Please click "Initialize Auth Client" first.'
      );
      return;
    }

    // Use consolidated auth check with processing
    await performAuthCheckWithProcessing('manual');
  });

  document
    .getElementById("loginWithWorlds")
    .addEventListener("click", async () => {
      if (!sdkLoaded) {
        displayResult("authResult", "SDK not loaded yet");
        return;
      }

      if (!authClientInitialized || !globalThis.viverseClient) {
        displayResult(
          "authResult",
          'Auth client not initialized. Please click "Initialize Auth Client" first.'
        );
        return;
      }

      try {
        const state = document.getElementById("loginState").value;
        const config = {};

        if (state) {
          config.state = state;
        }

        displayResult("authResult", "Initiating login with VIVERSE Worlds...");
        await globalThis.viverseClient.loginWithWorlds(config);

        // Note: This line should rarely execute because loginWithWorlds() typically redirects the browser
        displayResult(
          "authResult",
          "Login with VIVERSE Worlds initiated successfully."
        );
      } catch (error) {
        let errorMessage = `Error starting login with Worlds: ${
          error.message || error
        }`;

        // Provide specific guidance for login failures
        errorMessage +=
          "\n\nThis usually indicates:\n• Invalid Client ID configuration\n• Network connectivity issues\n• CORS/domain configuration problems\n• Check browser console for detailed error information";

        displayResult("authResult", errorMessage);
        console.error("Login with Worlds error details:", error);
      }
    });

  document.getElementById("logout").addEventListener("click", async () => {
    if (!sdkLoaded) {
      displayResult("authResult", "SDK not loaded yet");
      return;
    }

    if (!authClientInitialized || !globalThis.viverseClient) {
      displayResult(
        "authResult",
        'Auth client not initialized. Please click "Initialize Auth Client" first.'
      );
      return;
    }

    try {
      await globalThis.viverseClient.logout({
        redirectionUrl: window.location.href,
      });
    } catch (error) {
      displayResult(
        "authResult",
        `Error logging out: ${error.message || error}`
      );
    }
  });

  // ---- Avatar Section ----
  document.getElementById("initAvatar").addEventListener("click", async () => {
    if (!sdkLoaded) {
      displayResult("avatarResult", "SDK not loaded yet");
      return;
    }

    if (!currentToken) {
      displayResult(
        "avatarResult",
        "No authentication token available. Please authenticate first."
      );
      return;
    }

    try {
      globalThis.avatarClient = new globalThis.viverse.avatar({
        baseURL: "https://sdk-api.viverse.com/",
        token: currentToken,
      });

      displayResult("avatarResult", "Avatar client initialized successfully");
    } catch (error) {
      displayResult(
        "avatarResult",
        `Error initializing avatar client: ${error.message || error}`
      );
    }
  });

  document
    .getElementById("getActiveAvatar")
    .addEventListener("click", async () => {
      if (!globalThis.avatarClient) {
        displayResult("avatarResult", "Avatar client not initialized");
        return;
      }

      try {
        const avatar = await globalThis.avatarClient.getActiveAvatar();
        displayResult("avatarResult", avatar);
      } catch (error) {
        displayResult(
          "avatarResult",
          `Error getting active avatar: ${error.message || error}`
        );
      }
    });

  document
    .getElementById("getAvatarList")
    .addEventListener("click", async () => {
      if (!globalThis.avatarClient) {
        displayResult("avatarResult", "Avatar client not initialized");
        return;
      }

      try {
        const avatars = await globalThis.avatarClient.getAvatarList();
        displayResult("avatarResult", avatars);
      } catch (error) {
        displayResult(
          "avatarResult",
          `Error getting avatar list: ${error.message || error}`
        );
      }
    });

  document
    .getElementById("getPublicAvatarList")
    .addEventListener("click", async () => {
      if (!globalThis.avatarClient) {
        displayResult("avatarResult", "Avatar client not initialized");
        return;
      }

      try {
        const avatars = await globalThis.avatarClient.getPublicAvatarList();
        displayResult("avatarResult", avatars);
      } catch (error) {
        displayResult(
          "avatarResult",
          `Error getting public avatars: ${error.message || error}`
        );
      }
    });

  document
    .getElementById("getPublicAvatarByID")
    .addEventListener("click", async () => {
      if (!globalThis.avatarClient) {
        displayResult("avatarResult", "Avatar client not initialized");
        return;
      }

      const avatarId = document.getElementById("avatarId").value;
      if (!avatarId) {
        displayResult("avatarResult", "Please enter an Avatar ID");
        return;
      }

      try {
        const avatar = await globalThis.avatarClient.getPublicAvatarByID(
          avatarId
        );
        displayResult("avatarResult", avatar);
      } catch (error) {
        displayResult(
          "avatarResult",
          `Error getting avatar details: ${error.message || error}`
        );
      }
    });

  // ---- Game Dashboard Section ----
  document
    .getElementById("initDashboard")
    .addEventListener("click", async () => {
      if (!sdkLoaded) {
        displayResult("dashboardResult", "SDK not loaded yet");
        return;
      }

      if (!currentToken) {
        displayResult(
          "dashboardResult",
          "No authentication token available. Please authenticate first."
        );
        return;
      }

      const appId = document.getElementById("appId").value;
      if (!appId) {
        displayResult("dashboardResult", "Please enter an App ID");
        return;
      }

      try {
        globalThis.gameDashboardClient = new globalThis.viverse.gameDashboard({
          baseURL: "https://www.viveport.com/",
          communityBaseURL: "https://www.viverse.com/",
          token: currentToken,
        });

        displayResult(
          "dashboardResult",
          "Game Dashboard client initialized successfully"
        );
      } catch (error) {
        displayResult(
          "dashboardResult",
          `Error initializing dashboard client: ${error.message || error}`
        );
      }
    });

  document
    .getElementById("getLeaderboard")
    .addEventListener("click", async () => {
      if (!globalThis.gameDashboardClient) {
        displayResult("dashboardResult", "Dashboard client not initialized");
        return;
      }

      const appId = document.getElementById("appId").value;
      if (!appId) {
        displayResult("dashboardResult", "Please enter an App ID");
        return;
      }

      const name = document.getElementById("leaderboardName").value;
      if (!name) {
        displayResult("dashboardResult", "Please enter a Leaderboard Name");
        return;
      }

      try {
        const config = {
          name: name,
          range_start:
            parseInt(document.getElementById("leaderboardRangeStart").value) ||
            0,
          range_end:
            parseInt(document.getElementById("leaderboardRangeEnd").value) ||
            100,
          region: document.getElementById("leaderboardRegion").value,
          time_range: document.getElementById("leaderboardTimeRange").value,
          around_user: document.getElementById("leaderboardAroundUser").checked,
        };

        const leaderboard = await globalThis.gameDashboardClient.getLeaderboard(
          appId,
          config
        );
        displayResult("dashboardResult", leaderboard);
      } catch (error) {
        displayResult(
          "dashboardResult",
          `Error getting leaderboard: ${error.message || error}`
        );
      }
    });

  document.getElementById("uploadScore").addEventListener("click", async () => {
    if (!globalThis.gameDashboardClient) {
      displayResult("dashboardResult", "Dashboard client not initialized");
      return;
    }

    const appId = document.getElementById("appId").value;
    if (!appId) {
      displayResult("dashboardResult", "Please enter an App ID");
      return;
    }

    const name = document.getElementById("leaderboardName").value;
    const value = document.getElementById("scoreValue").value;

    if (!name || !value) {
      displayResult(
        "dashboardResult",
        "Please enter a valid leaderboard name and score value"
      );
      return;
    }

    try {
      const scores = [
        {
          name: name,
          value: value,
        },
      ];
      const result =
        await globalThis.gameDashboardClient.uploadLeaderboardScore(
          appId,
          scores
        );
      displayResult("dashboardResult", result);
    } catch (error) {
      displayResult(
        "dashboardResult",
        `Error uploading score: ${error.message || error}`
      );
    }
  });

  document
    .getElementById("getUserAchievements")
    .addEventListener("click", async () => {
      if (!globalThis.gameDashboardClient) {
        displayResult("dashboardResult", "Dashboard client not initialized");
        return;
      }

      const appId = document.getElementById("appId").value;
      if (!appId) {
        displayResult("dashboardResult", "Please enter an App ID");
        return;
      }

      try {
        const achievements =
          await globalThis.gameDashboardClient.getUserAchievement(appId);
        displayResult("dashboardResult", achievements);
      } catch (error) {
        displayResult(
          "dashboardResult",
          `Error getting user achievements: ${error.message || error}`
        );
      }
    });

  document
    .getElementById("uploadAchievement")
    .addEventListener("click", async () => {
      if (!globalThis.gameDashboardClient) {
        displayResult("dashboardResult", "Dashboard client not initialized");
        return;
      }

      const appId = document.getElementById("appId").value;
      if (!appId) {
        displayResult("dashboardResult", "Please enter an App ID");
        return;
      }

      const name = document.getElementById("achievementName").value;
      const status =
        document.getElementById("achievementStatus").value === "true";

      if (!name) {
        displayResult(
          "dashboardResult",
          "Please enter an Achievement API Name"
        );
        return;
      }

      try {
        const achievements = [
          {
            name: name,
            unlocked: status,
          },
        ];
        const result =
          await globalThis.gameDashboardClient.uploadUserAchievement(
            appId,
            achievements
          );
        displayResult("dashboardResult", result);
      } catch (error) {
        displayResult(
          "dashboardResult",
          `Error updating achievement: ${error.message || error}`
        );
      }
    });
}

// Initialize core functionality when page loads
window.addEventListener("load", async () => {
  loadSavedSettings();
  setupCoreEventListeners();
  await initializeSDK();

  // Check if we have saved client credentials and auto-initialize
  const clientId = document.getElementById("clientId").value;
  if (
    sdkLoaded &&
    clientId &&
    clientId !== "YOUR_CLIENT_ID" &&
    clientId.trim() !== ""
  ) {
    try {
      console.log("Auto-initializing auth client with saved credentials...");
      await initializeAuthClient();
      console.log("Auth client auto-initialized on page load");
      updateClientConfigStatus();
    } catch (error) {
      console.log("Could not auto-initialize auth client:", error);
      updateClientConfigStatus();
    }
  } else {
    // Update status even if not auto-initializing
    updateClientConfigStatus();
  }
});
