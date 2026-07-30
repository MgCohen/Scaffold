using NUnit.Framework;
using Scaffold.Figma.Editor.Import;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Scaffold.Figma.Tests.Editor
{
    public sealed class ImporterTests
    {
        const string FixtureRelative = "Packages/com.scaffold.figma/Tests/Fixtures/sample.figma.json";

        static string FixtureAssetPath => "Assets/" + FixtureRelative;

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset("Assets/_FigmaImportTests");
            AssetDatabase.Refresh();
        }

        [UnityTest]
        public System.Collections.IEnumerator ImportSample_ProducesHierarchyWithTextMeshPro()
        {
            var destDir = "Assets/_FigmaImportTests";
            var destPath = destDir + "/sample_copy.figma.json";
            AssetDatabase.CopyAsset(FixtureAssetPath, destPath);
            yield return null;
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(destPath));

            var root = AssetDatabase.LoadMainAssetAtPath(destPath) as GameObject;
            Assert.NotNull(root);
            Assert.AreEqual("Fixture Screen", root.name);

            var screen = root.transform.Find("Screen");
            Assert.NotNull(screen);
            var title = screen.GetComponentsInChildren<TextMeshProUGUI>(true);
            Assert.AreEqual(1, title.Length);
            Assert.AreEqual("Hello Figma", title[0].text);

            var panelTr = screen.Find("StretchedPanel");
            Assert.NotNull(panelTr);
            var img = panelTr.GetComponent<Image>();
            Assert.NotNull(img);
            Assert.That(img.color.b, Is.GreaterThan(0.9f));
        }

        [UnityTest]
        public System.Collections.IEnumerator StretchConstraints_ResizeParent_PreservesProportionalMargins()
        {
            var destPath = "Assets/_FigmaImportTests/sample_stretch.figma.json";
            AssetDatabase.CopyAsset(FixtureAssetPath, destPath);
            yield return null;

            var prefabRoot = AssetDatabase.LoadMainAssetAtPath(destPath) as GameObject;
            Assert.NotNull(prefabRoot);

            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(800, 800);

            var instance = Object.Instantiate(prefabRoot, canvasRt);
            var instanceRt = instance.GetComponent<RectTransform>();
            instanceRt.anchorMin = instanceRt.anchorMax = new Vector2(0.5f, 0.5f);
            instanceRt.pivot = new Vector2(0.5f, 0.5f);
            instanceRt.anchoredPosition = Vector2.zero;

            var screen = instance.transform.Find("Screen")?.GetComponent<RectTransform>();
            Assert.NotNull(screen);

            var panel = instance.transform.Find("Screen/StretchedPanel")?.GetComponent<RectTransform>();
            Assert.NotNull(panel);

            var leftMargin = 0f;

            screen.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500f);
            screen.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 700f);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(screen);

            Assert.That(panel.rect.width, Is.EqualTo(500f - leftMargin * 2f).Within(1f),
                "Stretched panel should span parent content width.");
            Assert.That(panel.rect.height, Is.EqualTo(700f - 48f - 8f).Within(2f),
                "Stretched panel should grow with parent height minus header and spacing.");

            Object.DestroyImmediate(canvasGo);
        }
    }
}
