namespace AJut.Core.UnitTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AJut.Security;

    [TestClass]
    public class SecurityTests
    {
        // Original - "Wow that's great!"
        ObfuscatedString g_wowThatsGreat = new ObfuscatedString(
            "yg4UVNKI7GZPf45IB47jxtOQaemMkwKQoWADjkY7nfqyGNta1ybWVm1l0eQ3751s",
            "yg4UVNKI7GZPf45IB47jxtOQaemMkwKQoWADjkY7nfqyGNta1ybWVm1l0eQ3751s"
        );

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

        [TestMethod]
        public void Encrypt_And_Decrypt_WithObscureString ()
        {
            string test = "Fancy String";

            // This is a super simple basic test, not differentiating between if we're x64 bit or not
            ObfuscatedString testEncrypted = new ObfuscatedString(CryptoObfuscation.Encrypt(test), CryptoObfuscation.Encrypt(test));

            Assert.AreNotEqual(test, testEncrypted.Active);
            Assert.AreEqual(test, CryptoObfuscation.Decrypt(testEncrypted));
        }

        [TestMethod]
        public void Decrypt_Fixed_Str ()
        {
            string wow = CryptoObfuscation.Encrypt("Wow that's great!");
            Assert.AreEqual("Wow that's great!", CryptoObfuscation.Decrypt(g_wowThatsGreat));
        }
    }
}
