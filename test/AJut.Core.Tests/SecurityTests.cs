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
            "gvwzqsAlCm4sQOfl9W08/PN1BZX0nQXeiPrs7tyfoS17U3T9CueatfTPS9BmvuWV"
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

            ObfuscatedString testEncrypted = new ObfuscatedString(CryptoObfuscation.Encrypt(test));

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
            Assert.AreEqual(g_wowThatsGreat.Active, CryptoObfuscation.Encrypt(g_wowThatsGreatSource));
            Assert.AreEqual(g_wowThatsGreatSource, CryptoObfuscation.Decrypt(g_wowThatsGreat));
        }

        [TestMethod]
        public void Encrypt_IsBitnessIndependent ()
        {
            // The crypto seed used to be derived from String.GetHashCode()'s bitness-dependent hash, so
            // the same source produced different ciphertext in x86 vs x64 processes. The seed is now
            // pinned to the x86 baseline, so this captured ciphertext must reproduce regardless of the
            // bitness of the process running the test.
            const string kBitnessIndependentCipher = "gvwzqsAlCm4sQOfl9W08/PN1BZX0nQXeiPrs7tyfoS17U3T9CueatfTPS9BmvuWV";
            Assert.AreEqual(kBitnessIndependentCipher, CryptoObfuscation.Encrypt(g_wowThatsGreatSource));
        }
    }
}
