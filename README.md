# Viverse Unity SDK for WebGL

A Unity SDK for integrating Viverse functionality into WebGL applications, including avatar rendering, leaderboards, and SSO capabilities.

## Project Structure

```
Viverse_Unity_SDK/
├── Assets/
│   ├── Editor/               # Editor-specific utilities
│   └── Samples/             # Sample scenes and implementations
├── Packages/
│   └── com.viverse.unityviversewebglapi/
│       ├── Editor/          # WebGL build tools and settings
│       ├── Runtime/         # Core SDK functionality
│       └── Tests/           # SDK test suite
└── ProjectSettings/         # Unity project settings
```

## Prerequisites

- Unity 6.x LTS (tested with 6000.0.34f1)
- Unity WebGL Build Support Module
- Windows Operating System
- [mkcert](https://github.com/FiloSottile/mkcert) installed globally
- Administrator access (for hosts file modification)

## Installation

### 1. System Configuration

1. **Configure Hosts File**:
   - Open `C:\Windows\System32\drivers\etc\hosts` as administrator
   - Add: `127.0.0.1 create.viverse.com`

2. **Install mkcert**:
   - Follow instructions at [mkcert Windows installation](https://github.com/FiloSottile/mkcert?tab=readme-ov-file#windows)
   - Verify installation by running `mkcert -version` in a new terminal
   - Restart your computer if you just added mkcert to PATH

3. **Verify Port Availability**:
   - Navigate to https://localhost
   - Ensure you see "site cannot be reached" (confirming no other HTTPS server is running)

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
   ├── Build/           <- Build output here
   ├── Library/
   └── ...
   ```

### 3. VRM Package Setup (Optional)

For avatar rendering support, install these packages via Package Manager:
```
https://github.com/vrm-c/UniVRM.git?path=/Assets/UniGLTF#v0.128.2
https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM#v0.128.2
https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM10#v0.128.2
```

### 4. HTTPS Server Setup

1. Open Tools > WebGL Settings
2. Navigate to the Server Setup section
3. Follow the step-by-step setup process:
   - Install SSL certificate
   - Generate SSL certificate
   - Install Node modules
   - Start HTTPS server

### 5. Leaderboard Configuration (Optional)

1. Create account on [VIVEPORT Developer Console](https://developer.viveport.com/console/titles)
2. Get your VIVEPORT ID from the listing
3. Configure leaderboard names in the VIVEPORT SDK section

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