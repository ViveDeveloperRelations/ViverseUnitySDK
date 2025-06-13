namespace ViverseUI.Infrastructure
{
    /// <summary>
    /// Interface for managing UI state, loading indicators, and user feedback
    /// </summary>
    public interface IUIStateManager
    {
        /// <summary>
        /// Show or hide loading overlay with optional message
        /// </summary>
        /// <param name="isLoading">Whether to show loading state</param>
        /// <param name="message">Optional loading message</param>
        void SetLoading(bool isLoading, string message = "");
        
        /// <summary>
        /// Display a success message to the user
        /// </summary>
        /// <param name="message">Success message to display</param>
        void ShowMessage(string message);
        
        /// <summary>
        /// Display an error message to the user
        /// </summary>
        /// <param name="error">Error message to display</param>
        void ShowError(string error);
        
        /// <summary>
        /// Enable or disable service-related buttons based on authentication state
        /// </summary>
        /// <param name="enabled">Whether to enable service buttons</param>
        void SetServiceButtonsEnabled(bool enabled);
    }
}