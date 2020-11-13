namespace AJut.UnitTests.Core
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AJut.Security;

    [TestClass]
    public class SecurityTests
    {
        [TestInitialize]
        public void Setup ()
        {
            CryptoObfuscation.Seed("Unit Testing");
        }

        [TestMethod]
        public void Encrypt_And_Decrypt ()
        {
            string test = "Fancy String";
            string testEncrypted = CryptoObfuscation.Encrypt(test);

            Assert.AreNotEqual(test, testEncrypted);
            Assert.AreEqual(test, CryptoObfuscation.Decrypt(testEncrypted));
        }
    }
}
