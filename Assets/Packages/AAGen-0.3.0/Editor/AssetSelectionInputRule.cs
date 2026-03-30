using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents the rule that includes a finite set of assets as input in the AAGen pipeline.
    /// Adds support for JSON TextAsset files that contain a simple array of asset paths.
    /// </summary>
    [CreateAssetMenu(menuName = Constants.ContextMenus.InputRulesMenu + nameof(AssetSelectionInputRule))]
    public class AssetSelectionInputRule : InputRule
    {
        #region Fields
        /// <summary>Assets added one-by-one by the user.</summary>
        [SerializeField] public List<UnityEngine.Object> m_SelectedAssets = new List<UnityEngine.Object>();

        /// <summary>TextAssets (.json/.txt) each containing a JSON array of asset paths.</summary>
        [SerializeField] public List<TextAsset> m_JsonAssetLists = new List<TextAsset>();

        /// <summary>Include all current Addressables entries as inputs.</summary>
        [SerializeField] public bool m_IncludeCurrentAddressables = false;
        #endregion

        #region Helper DTO
        [Serializable]
        private class StringArrayWrapper
        {
            public List<string> items;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Get a unique set of file paths for assets that should be included as inputs for AAGen.
        /// Pulls from:
        /// 1) m_SelectedAssets (object refs),
        /// 2) m_JsonAssetLists (JSON arrays of asset paths),
        /// 3) Current Addressables entries (optional).
        /// </summary>
        public override HashSet<string> GetIncludedAssets()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            // 1) One-by-one selected assets
            foreach (var asset in m_SelectedAssets)
            {
                if (asset == null) continue;
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path)) result.Add(path);
            }

            // 2) JSON path lists
            foreach (var jsonTextAsset in m_JsonAssetLists)
            {
                if (jsonTextAsset == null)
                    continue;

                try
                {
                    // Unity's JsonUtility cannot parse a bare array at the root,
                    // so we wrap it: {"items":[ ... ]}.
                    string raw = jsonTextAsset.text;
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    string wrapped = raw.TrimStart().StartsWith("[")
                        ? $"{{\"items\":{raw}}}"
                        : raw; // if user already wrapped, accept as-is

                    var parsed = JsonUtility.FromJson<StringArrayWrapper>(wrapped);

                    if (parsed?.items == null)
                        continue;

                    foreach (var p in parsed.items)
                    {
                        // Be defensive about whitespace/nulls.
                        string path = (p ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(path))
                            continue;

                        // Optionally validate the path points to an asset.
                        // We keep it permissive: if AssetDatabase can't load, skip silently.
                        var loaded = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (loaded == null) continue;

                        // Also skip folders to avoid surprises.
                        if (AssetDatabase.IsValidFolder(path)) continue;

                        result.Add(path);
                    }
                }
                catch (Exception ex)
                {
                    // Corrupt JSON should not break the pipeline; warn and continue.
                    Debug.LogWarning(
                        $"[AAGen] Invalid JSON in '{jsonTextAsset.name}'. " +
                        $"Expected a simple array of asset paths (e.g. [\"Assets/X.prefab\"]). " +
                        $"This file will be skipped. Error: {ex.Message}",
                        jsonTextAsset
                    );
                }
            }

            // 3) Current Addressables entries
            if (m_IncludeCurrentAddressables)
            {
                result.UnionWith(AddressableUtil.GetExtendedAddressableEntries());
            }

            return result;
        }
        #endregion
    }
}
