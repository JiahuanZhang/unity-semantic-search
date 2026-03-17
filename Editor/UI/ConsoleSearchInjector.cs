using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SemanticSearch.Editor.UI
{
    [InitializeOnLoad]
    static class ProjectSearchInjector
    {
        const string SearchBtnName = "semantic-search-btn";

        static readonly Type ProjectBrowserType;
        static readonly FieldInfo LastInteractedField;

        static readonly Dictionary<EditorWindow, Button> _injectedButtons = new();

        static ProjectSearchInjector()
        {
            ProjectBrowserType = Type.GetType("UnityEditor.ProjectBrowser,UnityEditor");
            if (ProjectBrowserType == null) return;

            LastInteractedField = ProjectBrowserType.GetField(
                "s_LastInteractedProjectBrowser",
                BindingFlags.Public | BindingFlags.Static);

            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            var closedKeys = _injectedButtons.Where(kv => kv.Key == null).Select(kv => kv.Key).ToList();
            foreach (var key in closedKeys)
            {
                _injectedButtons.Remove(key);
            }

            var projectWindows = GetAllProjectWindows();
            foreach (var window in projectWindows)
            {
                EnsureInjected(window);
            }
        }

        static void EnsureInjected(EditorWindow projectWindow)
        {
            if (_injectedButtons.ContainsKey(projectWindow)) return;

            var root = projectWindow.rootVisualElement;
            if (root == null) return;
            if (root.Q(SearchBtnName) != null) return;

            const float buttonWidth = 22f;

#if UNITY_2022_2_OR_NEWER
            const float baseRight = 470f;
#else
            const float baseRight = 440f;
#endif
            // ProjectWindowHistory 的 undo/redo 按钮占 2×20=40px，放在它们左侧
            const float offsetForHistoryButtons = 40f;
            float right = baseRight + offsetForHistoryButtons;

            var btn = new Button(() => SearchResultWindow.Show(""))
            {
                name = SearchBtnName,
                text = "🔍",
                tooltip = "Semantic Search (Shift+Alt+F)",
                focusable = false,
                style =
                {
                    width = buttonWidth,
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    position = new StyleEnum<Position>(Position.Absolute),
                    right = right
                }
            };

            root.Add(btn);
            _injectedButtons[projectWindow] = btn;
        }

        static List<EditorWindow> GetAllProjectWindows()
        {
            if (ProjectBrowserType == null) return new List<EditorWindow>();

            var listType = typeof(List<>).MakeGenericType(ProjectBrowserType);
            var projectWindowsField = ProjectBrowserType.GetField("s_ProjectBrowsers", BindingFlags.NonPublic | BindingFlags.Static);
            if (projectWindowsField == null) return new List<EditorWindow>();

            var projectWindowsObj = projectWindowsField.GetValue(null);
            if (projectWindowsObj == null) return new List<EditorWindow>();

            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetProperty("Item");
            if (countProp == null || indexer == null) return new List<EditorWindow>();

            int count = (int)countProp.GetValue(projectWindowsObj);
            var result = new List<EditorWindow>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add((EditorWindow)indexer.GetValue(projectWindowsObj, new object[] { i }));
            }

            return result;
        }

        [MenuItem("Window/Semantic Search #&f")]
        static void OpenSearchWindow()
        {
            SearchResultWindow.Show("");
        }
    }
}
