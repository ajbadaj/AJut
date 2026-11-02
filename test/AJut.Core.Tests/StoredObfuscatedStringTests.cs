namespace AJut.Core.UnitTests
{
    using System.Security.Cryptography;
    using System.Text;
    using AJut.Security;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Obfuscated strings made the way an app makes and keeps them: application setup seeds the defaults from the app's
    /// project name, the text is encrypted once (from the debugger, say), and the result goes into the code as an
    /// <see cref="ObfuscatedString"/> the app decrypts at runtime. Each string here was made by <see cref="CryptoObfuscation"/>
    /// as it shipped, so each has to keep decrypting to its text, and its text has to keep encrypting to it, through any
    /// change inside CryptoObfuscation and on every platform AJut.Core runs on. The Android device tests build this file
    /// too, so the same strings are checked there.
    /// </summary>
    [TestClass]
    public class StoredObfuscatedStringTests
    {
        // What application setup seeds with: ApplicationSetupConfig.DetermineCryptoSeed gives the app's project name
        private const string kAppProjectName = "ExampleApp";

        [TestInitialize]
        public void Setup ()
        {
            CryptoObfuscation.SeedDefaults(kAppProjectName);
        }

        [TestMethod]
        public void StoredObfuscatedString_MadeUnderTheAppSeed_StillDecryptsAndEncrypts ()
        {
            const string kSiteUrl = "https://ajut.dev";
            var stored = new ObfuscatedString("73ENQ6wjFNz6oNqi1LPR8PejDjaO3UQkI6v/pxLlxDS3nDae38xKdxjjveNYdU0W");

            Assert.AreEqual(kSiteUrl, CryptoObfuscation.Decrypt(stored));
            Assert.AreEqual(stored.Active, CryptoObfuscation.Encrypt(kSiteUrl));
        }

        [TestMethod]
        public void StoredString_MadeWithItsOwnKeyIterationsAndSha256_StillDecryptsAndEncrypts ()
        {
            const string kText = "A setting kept out of plain sight";
            const string kKey = "A key of its own";
            const int kIterations = 5000;
            const string kStored = "5jfuUUeVUsR2GJR2G62i30CPFAEM1aJF9acNozvx9KycU+EL5vTA44fYWqGC+nQzB1Ko9aOLw5i+KmFWYwKyssWQ0u5k9WPKAkeHFPlkKg4=";

            string decrypted = CryptoObfuscation.Decrypt(
                kStored,
                kKey,
                iterationCountOverride: kIterations,
                hashAlgorithmOverride: HashAlgorithmName.SHA256
            );
            string encrypted = CryptoObfuscation.Encrypt(
                kText,
                kKey,
                iterationCountOverride: kIterations,
                hashAlgorithmOverride: HashAlgorithmName.SHA256
            );

            Assert.AreEqual(kText, decrypted);
            Assert.AreEqual(kStored, encrypted);
        }

        [TestMethod]
        public void StoredString_MadeWithSha512AndUtf8_StillDecryptsAndEncrypts ()
        {
            const string kText = "Stored as UTF-8, with an accent: caf\u00e9";
            const string kKey = "Another key";
            const string kStored = "ocF0ffg+Ci+HJU1e/doifZmRWuggm49HkDjoZOZpn91qqGjojaMh0j82SoxIbF7R";

            string decrypted = CryptoObfuscation.Decrypt(
                kStored,
                kKey,
                encodingOverride: Encoding.UTF8,
                hashAlgorithmOverride: HashAlgorithmName.SHA512
            );
            string encrypted = CryptoObfuscation.Encrypt(
                kText,
                kKey,
                encodingOverride: Encoding.UTF8,
                hashAlgorithmOverride: HashAlgorithmName.SHA512
            );

            Assert.AreEqual(kText, decrypted);
            Assert.AreEqual(kStored, encrypted);
        }

        [TestMethod]
        public void StoredString_MadeWithAKeyHoldingAnEmoji_StillDecryptsAndEncrypts ()
        {
            // RandomizeAndSalt shuffles the key's characters, which splits this emoji's surrogate pair, and the shuffled key
            //  is then UTF-8 encoded. A lone surrogate has always encoded as the replacement character, so it has to keep
            //  encoding that way rather than throw.
            const string kText = "Stored under a key with an emoji";
            const string kKey = "A key with an emoji \uD83D\uDE00 in it";
            const string kStored = "LpbAbAQM8Izrgy10rxdHIErxL1ECkcjKfJWI0aiVpZp4fJyG5h0hBK0ueqCIQyFeq8aE2SmxyvSA7SjhZYOAgO76kVIwkjzIGGn1z8W/mzw=";

            Assert.AreEqual(kText, CryptoObfuscation.Decrypt(kStored, kKey));
            Assert.AreEqual(kStored, CryptoObfuscation.Encrypt(kText, kKey));
        }
    }
}
