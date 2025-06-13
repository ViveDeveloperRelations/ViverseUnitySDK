using UnityEngine.UIElements;

namespace ViverseUI.Infrastructure
{
    /// <summary>
    /// Extension methods for UI elements to simplify common operations
    /// </summary>
    public static class UIElementExtensions
    {
        /// <summary>
        /// Set display style for a UI element
        /// </summary>
        /// <param name="element">UI element</param>
        /// <param name="displayStyle">Display style to set</param>
        public static void SetDisplayStyle(this VisualElement element, DisplayStyle displayStyle)
        {
            if (element != null)
            {
                element.style.display = displayStyle;
            }
        }
        
        /// <summary>
        /// Show a UI element
        /// </summary>
        /// <param name="element">UI element</param>
        public static void Show(this VisualElement element)
        {
            element?.SetDisplayStyle(DisplayStyle.Flex);
        }
        
        /// <summary>
        /// Hide a UI element
        /// </summary>
        /// <param name="element">UI element</param>
        public static void Hide(this VisualElement element)
        {
            element?.SetDisplayStyle(DisplayStyle.None);
        }
        
        /// <summary>
        /// Toggle visibility of a UI element
        /// </summary>
        /// <param name="element">UI element</param>
        /// <param name="visible">Whether to show the element</param>
        public static void SetVisible(this VisualElement element, bool visible)
        {
            if (visible)
                element.Show();
            else
                element.Hide();
        }
    }
}