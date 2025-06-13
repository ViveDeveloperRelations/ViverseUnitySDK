using System;
using ViverseWebGLAPI;

namespace ViverseUI.Infrastructure
{
    /// <summary>
    /// Shared context interface providing access to core SDK services and state
    /// </summary>
    public interface IViverseServiceContext
    {
        /// <summary>
        /// The main ViverseCore SDK instance
        /// </summary>
        ViverseCore Core { get; }
        
        /// <summary>
        /// Whether the SDK is initialized and authenticated
        /// </summary>
        bool IsInitialized { get; }
        
        /// <summary>
        /// UI state management interface
        /// </summary>
        IUIStateManager UIStateManager { get; }
        
        /// <summary>
        /// Event fired when initialization state changes
        /// </summary>
        event Action<bool> OnInitializationChanged;
    }
}