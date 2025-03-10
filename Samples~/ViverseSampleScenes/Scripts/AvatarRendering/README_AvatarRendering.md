# VIVERSE SDK VRM Extension

This extension adds VRM avatar support to the VIVERSE SDK. It allows you to preview, animate, and cycle through VRM avatars in your application.

## Requirements

To use the VRM extension, you need to install the following packages:

1. **UniVRM** - For VRM file parsing and rendering (tested with version 0.128.2)
2. **UniGLTF** - For GLB/GLTF format support (required by UniVRM, tested with version 0.128.2)

## Installation

### 1. Install Required Packages

Add the UniVRM and UniGLTF packages to your project through the UPM (Unity Package Manager):

1. Open the Package Manager (Window > Package Manager)
2. Click the "+" button in the top left corner
3. Select "Add package from git URL..."
4. Add the following URLs one by one (specifying version 0.128.2 which has been tested):

```
https://github.com/vrm-c/UniVRM.git?path=/Assets/UniGLTF#v0.128.2
https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM#v0.128.2
https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM10#v0.128.2
```

These correspond to the packages:
- com.vrmc.gltf (UniGLTF)
- com.vrmc.univrm (VRM)
- com.vrmc.vrm (VRM10)

Alternatively, you can download the UniVRM package from the [UniVRM releases page](https://github.com/vrm-c/UniVRM/releases) and import it manually.

### 2. Setup Required Shaders

To ensure proper rendering of VRM avatars, make sure the following shaders are included in your build:

For Built-in Render Pipeline (BRP):
- VRM/MToon
- UI/Default
- UniGLTF/UniUnlit
- Standard
- Legacy Shaders/Diffuse
- Hidden/CubeBlur
- Hidden/CubeCopy
- Hidden/CubeBlend
- Hidden/VideoDecode
- Hidden/VideoComposite
- Sprites/Default

For Universal Render Pipeline (URP):
- VRM/MToon
- UI/Default
- UniGLTF/UniUnlit
- VRM10/Universal Render Pipeline/MToon10
- Sprites/Default

You can use the WebGL Settings window (Tools > WebGL Build Settings) to automatically add these shaders to your always included shaders list based on your current render pipeline.

## Usage

Once installed, the VRM extension components will be automatically activated in the VIVERSE SDK UI, enabling:

1. **Avatar Preview**: Preview VRM avatars with the "Preview Avatar" button
2. **Animation Control**: Play and control animations on the loaded avatars
3. **Avatar Cycling**: Automatically cycle through available avatars with customizable duration

## Troubleshooting

### Pink/Magenta Avatars

If your avatars appear pink or magenta, it may be due to one of these issues:

1. **Missing Shaders**: The required shaders are missing from your build. Use the WebGL Settings window (Tools > WebGL Build Settings) to add the necessary shaders to your "Always Included Shaders" list.

2. **Shader Variant Issues**: If adding shaders doesn't solve the problem, you may need to include shader variants:
   - Create a dummy material that uses the MToon shader in your Resources folder
   - Set the rendering mode to different options to force Unity to compile all variants
   - For URP, use the URPMaterialWorkarounds prefab in the VIVERSE SDK's Resources folder

3. **Render Pipeline Mismatch**: Make sure you're using the correct MToon shader for your render pipeline (MToon for BRP, MToon10 for URP).

### Performance Issues

VRM avatars can be resource-intensive, especially on WebGL platforms. For better performance:

- Reduce the number of avatars you load simultaneously
- Use lower LOD models when available
- Optimize animations to reduce CPU usage

## Additional Resources

- [UniVRM Documentation](https://vrm.dev/en/)
- [VRM Format Specification](https://github.com/vrm-c/vrm-specification)