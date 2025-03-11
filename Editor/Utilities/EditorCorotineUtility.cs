using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ViverseWebGLAPI
{
    /// <summary>
    /// Utility for running coroutines in the Unity Editor.
    /// Since normal coroutines don't work in editor code, this provides a similar functionality.
    /// </summary>
    public static class EditorCoroutineUtility
    {
        /// <summary>
        /// Starts a coroutine in the editor.
        /// </summary>
        /// <param name="routine">The coroutine to run.</param>
        /// <returns>A reference to the started coroutine.</returns>
        public static EditorCoroutine StartCoroutine(IEnumerator routine)
        {
            if (routine == null)
                throw new ArgumentNullException(nameof(routine));

            return new EditorCoroutine(routine);
        }

        /// <summary>
        /// Stops a running editor coroutine.
        /// </summary>
        /// <param name="coroutine">The coroutine to stop.</param>
        public static void StopCoroutine(EditorCoroutine coroutine)
        {
            if (coroutine == null)
                throw new ArgumentNullException(nameof(coroutine));

            coroutine.Stop();
        }
    }

    /// <summary>
    /// Represents a coroutine running in the editor.
    /// </summary>
    public class EditorCoroutine
    {
        private readonly IEnumerator _routine;
        private bool _stopped;
        private Stack<IEnumerator> _routineStack = new Stack<IEnumerator>();

        // Store all active coroutines to make sure they're executed
        private static readonly List<EditorCoroutine> _activeCoroutines = new List<EditorCoroutine>();

        // Make sure we're registered with the editor update callback
        static EditorCoroutine()
        {
            EditorApplication.update += UpdateAllCoroutines;
        }

        internal EditorCoroutine(IEnumerator routine)
        {
            _routine = routine;
            _routineStack.Push(routine);
            _activeCoroutines.Add(this);
        }

        /// <summary>
        /// Stops this coroutine from executing.
        /// </summary>
        public void Stop()
        {
            if (!_stopped)
            {
                _stopped = true;
                _activeCoroutines.Remove(this);
                _routineStack.Clear();
            }
        }

        // Process the next step in all active coroutines
        private static void UpdateAllCoroutines()
        {
            // Create a copy to handle coroutines that might be added during iteration
            EditorCoroutine[] coroutinesToUpdate = _activeCoroutines.ToArray();

            foreach (var coroutine in coroutinesToUpdate)
            {
                if (coroutine._stopped)
                    continue;

                try
                {
                    if (!coroutine.MoveNext())
                    {
                        coroutine.Stop();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogError($"Error in coroutine: {ex.Message}\nStack trace: {ex.StackTrace}");
                    coroutine.Stop();
                }
            }
        }

        // Move the coroutine to the next step with improved nested coroutine handling
        private bool MoveNext()
        {
            if (_routineStack.Count == 0)
                return false;

            IEnumerator currentEnumerator = _routineStack.Peek();
            bool moveNextResult = false;

            try
            {
                moveNextResult = currentEnumerator.MoveNext();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in coroutine MoveNext: {ex.Message}\nStack trace: {ex.StackTrace}");
                throw;
            }

            if (!moveNextResult)
            {
                // Current enumerator is complete, pop it from the stack
                _routineStack.Pop();

                // If we still have parent enumerators, continue with those
                if (_routineStack.Count > 0)
                    return MoveNext(); // Recursively process parent enumerators

                return false; // No more enumerators, we're done
            }

            // Check if the current yield value is another enumerator (nested coroutine)
            if (currentEnumerator.Current is IEnumerator nestedRoutine)
            {
                // Push the nested routine onto the stack and process it
                _routineStack.Push(nestedRoutine);
                return true;
            }

            // Special handling for WaitForSeconds in editor context
            if (currentEnumerator.Current is WaitForSeconds)
            {
                // Just continue immediately in editor context
                return true;
            }

            // Any other yield type just indicates we're waiting for the next update
            return true;
        }
    }
}
