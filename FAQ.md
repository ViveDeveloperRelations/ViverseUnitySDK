# FAQ notes need to figure out how to properly set images/etc

- if you add full stacktraces in exceptions look for this to see if you have the shader preloaded, since it means you don't have the shader that it is trying to load in your build (and/or it got stripped out by shader stripping settings)
- EXPAND ON THESE STEPS - this was requested
SampleAvatar.framework.js:3191 ArgumentNullException: Value cannot be null.
Parameter name: Shader
  at UniGLTF.MaterialFactory.LoadAsync (UniGLTF.MaterialDescriptor matDesc, UniGLTF.GetTextureAsyncFunc getTexture, UniGLTF.IAwaitCaller awaitCaller) [0x00000] in <00000000000000000000000000000000>:0 
  at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].Start[TStateMachine] (TStateMachine& stateMachine) [0x00000] in <00000000000000000000000000000000>:0 

- urp render cameras need postprocessing to render avatars correctly
- if you're having issues with the cert not being installed / showing not secure when going to https://create.viverse.com after running everything, 
  - ensure no other web server is serving content on https://create.viverse.com - close unity and then look at that page, nothing should be serving https://create.viverse.com
    - orphaned processes can cause crashes when unity crashes
  - there may have been another cert that overwrote this cert - either from mkcert or on your project, so regenerate the certs
    - Close your browser instances, delete the tools\ directory at the top level of the project, close the project, open it again, then re-run the steps in the webgl build settings to set up the http server again
    - rebooting may be needed too?? not sure about this - there may be a stray process running
    - if not, clear your HSTS data
      Chrome: Go to chrome://net-internals/#hsts, enter "create.viverse.com" in the "Delete domain security policies" field and click "Delete".
      Edge: Similar to Chrome, use edge://net-internals/#hsts.
      Firefox: mkcert doesn't support this browser

# KNOWN ISSUES
- mkcert does not support firefox, so this browser does not work
- build and run does not work, but build then viewing in a browser does
- if you login/logout/login, the user will get redirected to the account.viverse.com page

- if you see "ArgumentException: An item with the same key has already been added. Key: Neutral"
  - this is a reported issue, and the public avatars that have this issue are being fixed, if you have this issue on your own avatar, please report this to us with the email used to login to your htc account

- Shader stripping may be causing issues in rendering avatars, there are some workarounds, but report any found issues


