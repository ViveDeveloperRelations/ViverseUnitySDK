# Viverse Unity SDK for WebGL

A Unity SDK for integrating Viverse functionality into WebGL applications, including avatar rendering, leaderboards, and SSO capabilities.

## Prerequisites

- Unity 6.x LTS (tested with 6000.0.34f1)
- Unity WebGL Build Support Module
- Importing samples and testing the ConfigurableDriver as your first scene is highly recommended, it allows you to test with sample credentials for your app
  - Some sample credentials are listed for clientid/appid on the creator studio page

## Installation
1. Import package by going to Window/Package Manager and selecting the Package manager
 - ![Import Package inside of package manager](SupportingDocuments/Import_From_TGZ.png)
2. Import samples: Select the package "Viverse API For WebGL export" and select the "Samples" tab, then import samples from pacakge in the samples tab
 - ![Import Samples](SupportingDocuments/Viverse_Import_Samples.png)

### Unity Project Setup

1. **Create/Open Unity Project**:
   - Create a new Unity project or open existing one
   - Import the Viverse SDK package (.tgz)
   - Import included samples for testing

2. **Configure Build Settings**:
   - Switch platform to WebGL
   - Open Tools > WebGL Settings window
   - Click "Apply All Settings" to configure default settings
   - The WebGL Settings window automatically applies the correct project settings for Viverse integration when you select apply all settings and have the checkbox above it checked that disables compression fallback

3. **Configure Build Output**:
   - Create a `Build` folder parallel to `Assets`:
   ```
   PROJECT_ROOT/
   ├── Assets/
   ├── Build/           <- Build output here (name is currently not configurable for early releases)
   ├── Library/
   └── ...
   ```

### VRM Package Setup (Needed for avatar rendering)

1. Open Tools > WebGL Settings
2. Select the "Install VRM Packages" button to install the relevant packages, wait for them to import
![WebGL Settings install packages](SupportingDocuments/Settings_VRM_Package_Installation.png)
  - If something goes wrong or you want to manage these yourself, install these dependencies
```
https://github.com/vrm-c/UniVRM.git?path=/Assets/UniGLTF#v0.128.2
https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM#v0.128.2
https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM10#v0.128.2
```

### 4. Shader setup
1. Open Tools > WebGL Settings
2. Select "Add Missing Shaders" to add shaders/shader variant collections to your build settings that ensure that viverse avatars will render as expected in a build (as they will get stripped by the webgl export otherwise, but will work in the editor)
![WebGL Settings install packages](SupportingDocuments/Settings_Shader_Settings_Add_Missing_Shaders.png)

### 7. Build configurable driver scene to Build directory previously made
 - Set the active scene to the ConfigurableDriver test scene from the package manager assets, and build to the "Build" directory we previously made that is parallel to the "Assets" directory of this project
 - ![HTTPS Server Setup](SupportingDocuments/Configurable_Driver_Test_Scene.png)
 - Note: the first build will likely take a little while longer than expected - 10-30 minutes is normal
 - Upload the a zip file of the build direcotry to the https://studio.viverse.com/ site

### 8. Leaderboard / Achievements Configuration (Optional)

1. Create account on [Viverse Stuiod](https://studio.viverse.com/)
2. Get your appid from the overview tab
3. Configure leaderboard names in the SDK Settings section
![Viveport ID Setup](SupportingDocuments/Viveport_id_setup.png)

## Known Issues

- Firefox browser not supported (mkcert limitation)

## Troubleshooting

### Testing Configuration
To test the configuration independently of unity project integration, there is a sample Viverse html only page to test that the functionality should work in your environment
1. Import "Viverse html only page" sample, which imports a ViverseAPIIntegrationPage.html page
2. Create a zip file containing that folder, and upload to the studio site

### Decompression settings warning shows up in build
1. if you see some popup like this when the app is loading
   ![Settings checkbox not checked before build](SupportingDocuments/Configuration_issue_Decompression_Settings_checkbox_not_checked_before_build.jpeg)
  - make sure the disable decompression fallback checkbox is checked and press apply webgl settings
  - If you still see this, un-check it, check it again and apply webgl settings, and then press save project before building again

#### Browser Console Debugging

Open browser developer tools (F12) and check for:

**JavaScript Console Errors:**
- `Viverse SDK not loaded` - SDK failed to load from CDN
- `multiplayerClient is undefined` - Multiplayer service not initialized
- `matchmakingClient is undefined` - Matchmaking service not initialized

**Network Tab Issues:**
- Failed requests to Viverse APIs
- CORS errors (may be normal for some operations)
- 401/403 errors (authentication issues)


## Known Issues

- **Firefox browser not supported** (mkcert limitation)

### Currently Non-Functional Features

- **Local development testing flow is currently non-functional**
  - The Node.js development server in `Editor/NodeServerForTesting/` is not operational
  - Local HTTPS testing with mkcert certificates is not working
  - **Workaround**: Upload builds to https://studio.viverse.com/ for testing

- **Multiplayer functionality is currently stubbed**
  - Networking flow is present in the SDK but non-functional at this time
  - Includes: Room management, matchmaking, real-time communication
  - **Status**: Updates are expected once related state management logic is finalized

### Service-Specific Issues

- **Leaderboard score uploads may fail when using the app ID directly**
  - Uploads may not return results when using the app ID directly
  - **Workaround**: Create leaderboard-specific app IDs in the Viveport Console
  - May be related to preview app status and will be addressed soon

- **Inconsistent handling of `appid` and `clientid`**
  - Due to legacy design and current backend inconsistencies, `appid` and `clientid` are treated as separate identifiers
  - This separation will be resolved once support is fully aligned with the new Viverse Studio flow


## Version History

See [CHANGELOG.md](CHANGELOG.md) for version history and updates.
