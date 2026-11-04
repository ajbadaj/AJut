namespace AJut.Core.UnitTests
{
    using System;
    using System.IO;
    using AJut.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// DirectoryHelpers' moves and copies, each in a folder of its own under the temp folder
    /// </summary>
    [TestClass]
    public class DirectoryHelpersTests
    {
        private string m_root;

        [TestInitialize]
        public void Setup ()
        {
            m_root = Path.Combine(Path.GetTempPath(), "AJut.DirectoryHelpersTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_root);
        }

        [TestCleanup]
        public void Teardown ()
        {
            if (Directory.Exists(m_root))
            {
                Directory.Delete(m_root, recursive: true);
            }
        }

        [TestMethod]
        public void DirectoryHelpers_CopyAndMerge_KeepsSubfoldersUnderTheDestination ()
        {
            string source = this.MakeSourceWithASubfolder();
            string destination = Path.Combine(m_root, "destination");

            DirectoryHelpers.CopyAndMergeDirectory(source, destination);

            Assert.AreEqual("top", File.ReadAllText(Path.Combine(destination, "top.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "sub", "inner.txt")), "The subfolder's file is under the destination's subfolder");
            Assert.IsFalse(File.Exists(Path.Combine(source, "inner.txt")), "The subfolder's file was merged into the source folder");
        }

        [TestMethod]
        public void DirectoryHelpers_MoveAndMergeIntoAnExistingFolder_KeepsSubfoldersUnderTheDestination ()
        {
            string source = this.MakeSourceWithASubfolder();
            string destination = Path.Combine(m_root, "destination");
            Directory.CreateDirectory(destination);

            DirectoryHelpers.MoveAndMergeDirectory(source, destination);

            Assert.AreEqual("top", File.ReadAllText(Path.Combine(destination, "top.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "sub", "inner.txt")), "The subfolder's file is under the destination's subfolder");
            Assert.IsFalse(File.Exists(Path.Combine(source, "inner.txt")), "The subfolder's file was merged into the source folder");
        }

        [TestMethod]
        public void DirectoryHelpers_MoveOrCopyOntoItselfByCase_OnAVolumeThatIgnoresCase_LeavesItAlone ()
        {
            // A platform whose rule tells case apart can still have a volume that ignores it (Android's shared storage, a FAT
            //  card), and there 'photos' is 'Photos'. Merging a folder onto itself deletes each file before it moves it. The
            //  paths are written with '/', the one separator every platform takes, since the one simulated doesn't take '\'.
            string root = m_root.Replace('\\', '/');
            string source = root + "/Photos";
            Directory.CreateDirectory(source);
            File.WriteAllText(source + "/a.txt", "a");
            string sameFolderByCase = root + "/photos";
            if (!Directory.Exists(sameFolderByCase))
            {
                Assert.Inconclusive("Needs a temp folder on a volume that ignores case");
            }

            using (PathPlatform.Simulate(ePathPlatform.CaseSensitive))
            {
                DirectoryHelpers.MoveAndMergeDirectory(source, sameFolderByCase);
                DirectoryHelpers.CopyAndMergeDirectory(source, sameFolderByCase);
            }

            Assert.AreEqual("a", File.ReadAllText(Path.Combine(source, "a.txt")));
        }

        [TestMethod]
        public void DirectoryHelpers_MoveByCase_OnAVolumeThatTellsCaseApart_Moves ()
        {
            string source = Path.Combine(m_root, "Photos");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "a.txt"), "a");
            string otherFolderByCase = Path.Combine(m_root, "photos");
            if (Directory.Exists(otherFolderByCase))
            {
                Assert.Inconclusive("Needs a temp folder on a volume that tells case apart");
            }

            DirectoryHelpers.MoveAndMergeDirectory(source, otherFolderByCase);

            Assert.AreEqual("a", File.ReadAllText(Path.Combine(otherFolderByCase, "a.txt")));
        }

        private string MakeSourceWithASubfolder ()
        {
            string source = Path.Combine(m_root, "source");
            Directory.CreateDirectory(Path.Combine(source, "sub"));
            File.WriteAllText(Path.Combine(source, "top.txt"), "top");
            File.WriteAllText(Path.Combine(source, "sub", "inner.txt"), "inner");
            return source;
        }
    }
}
