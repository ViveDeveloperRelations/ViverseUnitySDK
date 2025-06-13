using System;
using ViverseWebGLAPI;

namespace ViverseUI.Infrastructure
{
    /// <summary>
    /// Implementation of IViverseServiceContext providing shared state and services
    /// </summary>
    public class ViverseServiceContext : IViverseServiceContext
    {
        private ViverseCore _core;
        private bool _isInitialized;
        
        /// <summary>
        /// The main ViverseCore SDK instance
        /// </summary>
        public ViverseCore Core 
        { 
            get => _core;
            set
            {
                _core = value;
                OnCoreChanged?.Invoke(_core);
            }
        }
        
        /// <summary>
        /// Whether the SDK is initialized and authenticated
        /// </summary>
        public bool IsInitialized 
        { 
            get => _isInitialized;
            set
            {
                if (_isInitialized != value)
                {
                    _isInitialized = value;
                    OnInitializationChanged?.Invoke(_isInitialized);
                }
            }
        }
        
        /// <summary>
        /// UI state management interface
        /// </summary>
        public IUIStateManager UIStateManager { get; }
        
        /// <summary>
        /// Event fired when initialization state changes
        /// </summary>
        public event Action<bool> OnInitializationChanged;
        
        /// <summary>
        /// Event fired when core instance changes
        /// </summary>
        public event Action<ViverseCore> OnCoreChanged;
        
        /// <summary>
        /// Constructor requiring UI state manager
        /// </summary>
        /// <param name="uiStateManager">UI state manager implementation</param>
        public ViverseServiceContext(IUIStateManager uiStateManager)
        {
            UIStateManager = uiStateManager ?? throw new ArgumentNullException(nameof(uiStateManager));
        }
    }
}