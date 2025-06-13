using UnityEngine;
using UnityEngine.UIElements;

namespace ViverseUI.Infrastructure
{
    /// <summary>
    /// Base class for all Viverse service managers providing common functionality
    /// </summary>
    public abstract class ViverseManagerBase
    {
        /// <summary>
        /// Shared context providing access to core services
        /// </summary>
        protected IViverseServiceContext Context { get; }
        
        /// <summary>
        /// UI state manager for loading states and user feedback
        /// </summary>
        protected IUIStateManager UIState => Context.UIStateManager;
        
        /// <summary>
        /// Root visual element for UI queries
        /// </summary>
        protected VisualElement Root { get; }
        
        /// <summary>
        /// Whether this manager has been initialized
        /// </summary>
        protected bool IsInitialized { get; private set; }
        
        /// <summary>
        /// Constructor requiring context and root UI element
        /// </summary>
        /// <param name="context">Service context</param>
        /// <param name="root">Root UI element</param>
        protected ViverseManagerBase(IViverseServiceContext context, VisualElement root)
        {
            Context = context;
            Root = root;
        }
        
        /// <summary>
        /// Initialize the manager - must be called before use
        /// </summary>
        public virtual void Initialize()
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"{GetType().Name} already initialized");
                return;
            }
            
            InitializeUIElements();
            SetupEventHandlers();
            LoadInitialState();
            
            IsInitialized = true;
            Debug.Log($"{GetType().Name} initialized successfully");
        }
        
        /// <summary>
        /// Clean up resources when manager is disposed
        /// </summary>
        public virtual void Cleanup()
        {
            if (!IsInitialized) return;
            
            CleanupEventHandlers();
            CleanupResources();
            
            IsInitialized = false;
            Debug.Log($"{GetType().Name} cleaned up");
        }
        
        /// <summary>
        /// Initialize UI elements - override in derived classes
        /// </summary>
        protected virtual void InitializeUIElements() { }
        
        /// <summary>
        /// Setup event handlers - override in derived classes
        /// </summary>
        protected virtual void SetupEventHandlers() { }
        
        /// <summary>
        /// Load initial state - override in derived classes
        /// </summary>
        protected virtual void LoadInitialState() { }
        
        /// <summary>
        /// Cleanup event handlers - override in derived classes
        /// </summary>
        protected virtual void CleanupEventHandlers() { }
        
        /// <summary>
        /// Cleanup resources - override in derived classes
        /// </summary>
        protected virtual void CleanupResources() { }
        
        /// <summary>
        /// Helper method to check if manager is properly initialized
        /// </summary>
        protected bool CheckInitialization()
        {
            if (!IsInitialized)
            {
                Debug.LogError($"{GetType().Name} not initialized");
                return false;
            }
            
            if (!Context.IsInitialized)
            {
                UIState.ShowError("Please initialize and login first");
                return false;
            }
            
            return true;
        }
    }
}