# Viverse Unity SDK for WebGL

A Unity SDK for integrating Viverse functionality into WebGL applications, including avatar rendering, leaderboards, and SSO capabilities.

## Prerequisites

- Unity 6.x LTS (tested with 6000.0.34f1)
- Unity WebGL Build Support Module
- Windows Operating System ( mac support coming soon )
- [mkcert](https://github.com/FiloSottile/mkcert) installed globally
  - ensure that you can open a terminal and can run "mkcert -version" in it successfully
  - requires reboot for unity to pick up the new path after it's installed
- Administrator access (for hosts file modification)
- Importing samples and testing the ConfigurableDriver as your first scene is highly reccomended, it allows you to test with sample credentials for your app
  - Some sample credentials if you do not have any for testing are:
    - clientid: 3c3e8325-db8f-4a77-a66b-c189c500b0ad
    - appid for leaderboards:  64aa6613-4e6c-4db4-b270-67744e953ce0
    - leaderboard name: test_leaderboard - which was created with options: sort decending, numeric and append type

## Installation
1. Import package by going to Window/Package Manager and selecting the Package manager
 - ![Import Package inside of package manager](SupportingDocuments/Import_From_TGZ.png)
2. Import samples: Select the package "Viverse API For WebGL export" and select the "Samples" tab, then import samples from pacakge in the samples tab
 - ![Import Samples](SupportingDocuments/Viverse_Import_Samples.png)
### 1. System Configuration

1. **Configure Hosts File**:
   - Open `C:\Windows\System32\drivers\etc\hosts` as administrator
   - Add: `127.0.0.1 create.viverse.com`

2. **Verify Port Availability**:
   - Navigate to https://localhost
   - Ensure you see "site cannot be reached" (confirming no other HTTPS server is running)

3. **Install mkcert**:
   - Follow instructions at [mkcert Windows installation](https://github.com/FiloSottile/mkcert?tab=readme-ov-file#windows)
   - Verify installation by running `mkcert -version` in a new terminal
   -  ![MKCert version works](SupportingDocuments/MKCert_version_works.png)
   - Restart your computer if you just added mkcert to PATH

### 2. Unity Project Setup

1. **Create/Open Unity Project**:
   - Create a new Unity project or open existing one
   - Import the Viverse SDK package (.tgz)
   - Import included samples for testing

2. **Configure Build Settings**:
   - Switch platform to WebGL
   - Open Tools > WebGL Settings window
   - Click "Apply All Settings" to configure default settings

3. **Configure Build Output**:
   - Create a `Build` folder parallel to `Assets`:
   ```
   PROJECT_ROOT/
   ├── Assets/
   ├── Build/           <- Build output here (name is currently not configurable for early releases)
   ├── Library/
   └── ...
   ```

### 4. VRM Package Setup (Needed for avatar rendering)

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
2. Select "Add Missing Shaders" to add shaders/shader variant collecitons to your build settings that ensure that viverse avatars will render as expected in a build (as they will get stripped by the webgl export otherwise, but will work in the editor)
![WebGL Settings install packages](SupportingDocuments/Settings_Shader_Settings_Add_Missing_Shaders.png)

### 5. HTTPS Server Setup

1. Open Tools > WebGL Settings
2. Navigate to the Server Setup section
3. Follow the step-by-step setup process:
   - Install SSL certificate
   - Generate SSL certificate
   - Install Node modules
   - Start HTTPS server
- At the end it should look like this, and have no errors in the console
![HTTPS Server Setup](SupportingDocuments/HTTPS_Server_Setup.png)

### 7. Build configurable driver scene to Build directory previously made
 - Set the active scene to the ConfigurableDriver test scene from the package manager assets, and build to the "Build" directory we previously made that is parallel to the "Assets" directory of this project
 - ![HTTPS Server Setup](SupportingDocuments/Configurable_Driver_Test_Scene.png)
 - Note: the first build will likely take a little while longer than expected - 10-30 minutes is normal
 - Open a new browser instance and go to https://create.viverse.com - you should see logs in the console of your unity editor inidicating requests are being served from it, and the configurable direver scene, where you can use test credentials provided in the prerequisites section at the top of this file
- ![HTTPS Server Setup](SupportingDocuments/test_against_create_viverse_com.png)

### 8. Leaderboard Configuration (Optional)

1. Create account on [VIVEPORT Developer Console](https://developer.viveport.com/console/titles)
2. Get your VIVEPORT ID from the listing
3. Configure leaderboard names in the VIVEPORT SDK section, using the viveport id as the identifier in the leaderboard section
![Viveport ID Setup](SupportingDocuments/Viveport_id_setup.png)

## Known Issues

- Firefox browser not supported (mkcert limitation)
- Build & Run doesn't work directly (build then view in browser instead)
- Login/logout/login sequence redirects to account.viverse.com
- Some avatars may show "ArgumentException: Neutral key already added"
- URP hair rendering may have issues
- CORS errors may appear for leaderboard requests (can be ignored)

## Build Output Structure

After building, your `Build` folder should contain:
```
Build/
├── Build/
│   ├── Build.data
│   ├── Build.framework.js
│   ├── Build.loader.js
│   └── Build.wasm
├── TemplateData/
└── index.html
```

## Troubleshooting

If you encounter SSL certificate issues:
1. Close Unity and all browser instances
2. Delete the `tools` directory
3. Reopen project
4. Reconfigure HTTPS server in WebGL settings
5. Clear HSTS data if needed:
   - Chrome: chrome://net-internals/#hsts
   - Edge: edge://net-internals/#hsts
   - Firefox: Not supported

## Version History

See [CHANGELOG.md](CHANGELOG.md) for version history and updates.