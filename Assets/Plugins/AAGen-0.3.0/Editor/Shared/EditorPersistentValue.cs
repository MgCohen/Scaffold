using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using UnityEditor;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents a value that can persist through Unity editor assembly domain reloads.
    /// </summary>
    /// <typeparam name="T">A type that can be serialized by the <see cref="JsonConvert"/> class.</typeparam>
    public struct EditorPersistentValue<T>
    {
        #region Fields
        /// <summary>
        /// The identifier that the value is associated with.
        /// </summary>
        private readonly string _persistenceKey;

        /// <summary>
        /// Notifies subscribers that value has changed.
        /// </summary>
        // NOTE: make this a public event?
        private readonly Action _onValueChanged;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public T Value
        {
            get => Load();
            set
            {
                // If the new value is the same as the persistent value, then:
                if (object.Equals(Value, value))
                {
                    // Do nothing else.
                    return;
                }

                // Otherwise, the new value is different than the persistent value.

                // Set the new value as the persistent value.
                Save(value);
                
                // Notify subscribers that the value has changed.
                _onValueChanged?.Invoke();
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="EditorPersistentValue"/> class.
        /// </summary>
        /// <param name="defaultValue">The value to initialize the instance with.</param>
        /// <param name="persistenceKey">The identifier that the value is associated with.</param>
        /// <param name="onValueChanged">A function object that handles when the value changes.</param>
        public EditorPersistentValue(T defaultValue, [NotNull] string persistenceKey, Action onValueChanged = null)
        {
            _persistenceKey = persistenceKey;
            _onValueChanged = onValueChanged;

            // NOTE: where is default value being used?
        }

        /// <summary>
        /// Saves the persistent value.
        /// </summary>
        /// <param name="value"></param>
        private void Save(T value)
        {
            try
            {
                // Serialize the object into a JSON formatted string value.
                string jsonValue = JsonConvert.SerializeObject(value);

                // Attempt to set the value associated with the identifier; it will lazily instantiate an entry if there is none.
                EditorPrefs.SetString(_persistenceKey, jsonValue);
            }
            catch (Exception e)
            {
                // If there were any exceptions thrown during the load, log them. 
                Debug.LogError($"Save failed!: {e}");
            }
        }

        /// <summary>
        /// Loads the persistent value.
        /// </summary>
        /// <returns>The persistent value.</returns>
        private T Load()
        {
            try
            {
                // If there is a persistent value associated wit hthe identifier, then:
                if (EditorPrefs.HasKey(_persistenceKey))
                {
                    // Attempt to retrieve the string value associated with the identifier.
                    string jsonValue = EditorPrefs.GetString(_persistenceKey);

                    // Assume that the string value is in JSON format;
                    // Deserialize the string into an object of persistent value's type.
                    T value = JsonConvert.DeserializeObject<T>(jsonValue);

                    // Return the value.
                    return value;
                }
            }
            catch (Exception e)
            {
                // If there were any exceptions thrown during the load, log them. 
                Debug.LogError($"Load failed: {e}");
            }

            // Otherwise, there was an issue with loading.

            // Return the default value.
            return default;
        }

        /// <summary>
        /// Remove the value and entry from being persistent.
        /// </summary>
        public void ClearPersistentData()
        {
            // Remove the value and entry from being persistent in the Unity editor.
            EditorPrefs.DeleteKey(_persistenceKey);
        }
        #endregion
    }
}
