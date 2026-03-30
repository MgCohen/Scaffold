using NUnit.Framework;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.TestTools;

namespace AAGen.Tests
{
    public class FileUtilsTests
    {
        const string TempFolder = "Temp/AAGenTests";
        string testFilePath;

        [SetUp]
        public void Setup()
        {
            if (!Directory.Exists(TempFolder))
                Directory.CreateDirectory(TempFolder);
            testFilePath = Path.Combine(TempFolder, "test.json");
        }

        [TearDown]
        public void Teardown()
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
            if (Directory.Exists(TempFolder))
                Directory.Delete(TempFolder, true);
        }

        [Test]
        public void SaveToFileAndLoadFromFile_WritesAndReadsCorrectly()
        {
            string testData = "Hello AAGen!";
            FileUtils.SaveToFile(testFilePath, testData);

            Assert.IsTrue(File.Exists(testFilePath));
            var readData = FileUtils.LoadFromFile(testFilePath);
            Assert.AreEqual(testData, readData);
        }

        [UnityTest]
        public IEnumerator SaveToFileAsync_WritesCorrectly()
        {
            string content = new string('A', 1024 * 10); // 10KB
            bool completed = false;

            yield return FileUtils.SaveToFileAsync(content, testFilePath, success => completed = success, 1024);

            Assert.IsTrue(completed);
            Assert.IsTrue(File.Exists(testFilePath));
            Assert.AreEqual(content, File.ReadAllText(testFilePath, Encoding.UTF8));
        }

        [UnityTest]
        public IEnumerator LoadFromFileAsync_DeserializesCorrectly()
        {
            var original = new Dummy { Name = "LoadTest", Value = 99 };
            string json = JsonUtility.ToJson(original);
            File.WriteAllText(testFilePath, json);

            Dummy loaded = null;
            yield return FileUtils.LoadFromFileAsync<Dummy>(testFilePath, result => loaded = result, 1024);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("LoadTest", loaded.Name);
            Assert.AreEqual(99, loaded.Value);
        }

        [System.Serializable]
        class Dummy
        {
            public string Name;
            public int Value;
        }
    }
}