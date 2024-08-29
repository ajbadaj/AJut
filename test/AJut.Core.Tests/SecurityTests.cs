namespace AJut.Core.UnitTests
{
    using AJut.Security;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SecurityTests
    {
        // Original - "Wow that's great!"
        string g_wowThatsGreatSource = "Wow that's great!";
        ObfuscatedString g_wowThatsGreat = new ObfuscatedString(
            "gvwzqsAlCm4sQOfl9W08/PN1BZX0nQXeiPrs7tyfoS17U3T9CueatfTPS9BmvuWV",
            "yg4UVNKI7GZPf45IB47jxtOQaemMkwKQoWADjkY7nfqyGNta1ybWVm1l0eQ3751s"
        );

        [TestInitialize]
        public void Setup ()
        {
            CryptoObfuscation.SeedDefaults("Unit Testing");
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
        public void Encrypt_IsDeterministic_WithObscureString ()
        {
            string test = "Fancy String II";

            string expected = CryptoObfuscation.Encrypt(test);
            Assert.AreEqual(expected, CryptoObfuscation.Encrypt(test));
            Assert.AreEqual(expected, CryptoObfuscation.Encrypt(test));
            Assert.AreEqual(expected, CryptoObfuscation.Encrypt(test));
        }

        [TestMethod]
        public void Decrypt_Fixed_Str ()
        {
            string test = CryptoObfuscation.Encrypt(g_wowThatsGreatSource);
            Assert.AreEqual(g_wowThatsGreat.Active, CryptoObfuscation.Encrypt(g_wowThatsGreatSource));
            Assert.AreEqual(g_wowThatsGreatSource, CryptoObfuscation.Decrypt(g_wowThatsGreat));
        }
    }
}
