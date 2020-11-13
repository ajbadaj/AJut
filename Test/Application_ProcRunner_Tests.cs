namespace AJut.Application.Tests
{
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [TestClass]
    public class Application_ProcRunner_Tests
    {
        #region Batch Script Text
        static readonly string kAppWithOutput =
@"@echo off
echo Input: '%~1'";
        static readonly string kAppWithErrOutput =
@"@echo off
echo Input: '%~1' 1>&2";
        #endregion
        List<string> m_filesToDelete = new List<string>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (string file in m_filesToDelete)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }


        string GenerateTestBatchFile(string batchText)
        {
            string tempBatchPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".bat");
            m_filesToDelete.Add(tempBatchPath);
            File.WriteAllText(tempBatchPath, batchText);
            return tempBatchPath;
        }

        [TestMethod]
        public void ProcRunner_GetsStdOutput()
        {
            string batchFile = GenerateTestBatchFile(kAppWithOutput);
            ProcConfiguration testApp = new ProcConfiguration(batchFile);
            ProcRunner runner = testApp.BuildRunner();
            ProcRunResults result = runner.Run("Test");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Input: 'Test'", result.OutputText.Trim('\r', '\n', ' '));
        }

        [TestMethod]
        public async Task ProcRunner_GetsStdOutput_Async()
        {
            string batchFile = GenerateTestBatchFile(kAppWithOutput);
            ProcConfiguration testApp = new ProcConfiguration(batchFile);
            ProcRunner runner = testApp.BuildRunner();
            ProcRunResults result = await runner.RunAsync("Test");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Input: 'Test'", result.OutputText.Trim('\r', '\n', ' '));
        }

        [TestMethod]
        public void ProcRunner_GetsErrOutput()
        {
            string batchFile = GenerateTestBatchFile(kAppWithErrOutput);
            ProcConfiguration testApp = new ProcConfiguration(batchFile);
            ProcRunner runner = testApp.BuildRunner();
            ProcRunResults result = runner.Run("Test");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Input: 'Test'", result.ErrorText.Trim('\r', '\n', ' '));
        }
    }
}
