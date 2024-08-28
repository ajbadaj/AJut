namespace AJut.Security
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Essentially this class is only meant when your goal is to obscure what it is you're doing
    /// Obviously anyone willing to put in the effort can decompile, figure out the algorithm, and
    /// reverse engineer what you're doing, so this is more of a minor deterent that should only
    /// be used for things that don't matter to you anyway.
    /// 
    /// Intended usage:
    /// Step 1) Setup is called, other more technology specific AJut libraries may do this for you
    /// Step 2) Locally encrypt the naked string you are storing and save the result
    /// Step 3) Change the code to only used the encrypted string + a call to decrypt
    /// </summary>
    public static class CryptoObfuscation
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static string g_key;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static int g_defaultIterations;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static HashAlgorithmName g_defaultAlgorithm;

        public static Encoding DefaultEncoding { get; set; } = Encoding.Unicode;

        public static void SeedDefaults (string key, int iterations = 1000, HashAlgorithmName? algorithm = null)
        {
            g_key = RandomizeAndSalt(key);
            g_defaultIterations = iterations;
            g_defaultAlgorithm = algorithm ?? HashAlgorithmName.SHA1;
        }

        public static string RandomizeAndSalt (string seedString)
        {
            var rng = new Random(seedString.GenerateStableHashCode());
            var charList = seedString.ToList();
            for (int i = 0; i < 20; ++i)
            {
                charList.Add((char)rng.Next(32, 126));
            }

            charList.Randomize(rng.Next);
            return String.Join("", charList);
        }

        private static Rfc2898DeriveBytes CreateCryptoBytes (string key, int? iterationCountOverride, HashAlgorithmName? hashAlgorithmOverride)
        {
            if (key == null)
            {
                key = g_key;
            }
            else
            {
                key = RandomizeAndSalt(key);
            }

            int iterations = iterationCountOverride ?? g_defaultIterations;
            HashAlgorithmName hashAlgorithm = hashAlgorithmOverride ?? g_defaultAlgorithm;

            Random rng = new Random(key.GenerateStableHashCode());
            byte[] crytpoSalt = new byte[rng.Next(8, 16)];
            rng.NextBytes(crytpoSalt);
            return new Rfc2898DeriveBytes(key, crytpoSalt, iterations, hashAlgorithm);
        }

        /// <summary>
        /// Use <see cref="Aes"/> to generate an encrypted string
        /// </summary>
        /// <param name="toEncrypt">The string to encrypt</param>
        /// <param name="encryptionKey">The plain encryption key to use (will be scrambled and salted before applying), or null if you want to use the default key registered via <see cref="SeedDefaults(string, int, HashAlgorithmName?)"/></param>
        /// <param name="encodingOverride">The encoding to use</param>
        /// <param name="iterationCountOverride"></param>
        /// <param name="hashAlgorithmOverride"></param>
        /// <returns></returns>
        public static string Encrypt (string toEncrypt, string encryptionKey = null, Encoding encodingOverride = null, int? iterationCountOverride = null, HashAlgorithmName? hashAlgorithmOverride = null)
        {
            Rfc2898DeriveBytes cryptoBytes = CreateCryptoBytes(encryptionKey, iterationCountOverride, hashAlgorithmOverride);
            byte[] bytesToEncode = (encodingOverride ?? DefaultEncoding).GetBytes(toEncrypt);

            using Aes encryptor = Aes.Create();
            encryptor.Padding = PaddingMode.PKCS7;
            encryptor.Key = cryptoBytes.GetBytes(32);
            encryptor.IV = cryptoBytes.GetBytes(16);
            using var ms = new MemoryStream();
            using CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(bytesToEncode, 0, bytesToEncode.Length);
            cs.FlushFinalBlock();
            cs.Close();

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt (string toDecrypt, string decryptionKey = null, Encoding encodingOverride = null, int? iterationCountOverride = null, HashAlgorithmName? hashAlgorithmOverride = null)
        {
            Rfc2898DeriveBytes cryptoBytes = CreateCryptoBytes(decryptionKey, iterationCountOverride, hashAlgorithmOverride);

            byte[] encodedBytes = Convert.FromBase64String(toDecrypt);
            using Aes decryptor = Aes.Create();
            decryptor.Padding = PaddingMode.PKCS7;
            decryptor.Key = cryptoBytes.GetBytes(32);
            decryptor.IV = cryptoBytes.GetBytes(16);
            using var memoryStreamOfEncodedBytes = new MemoryStream(encodedBytes);
            using var cryptographicStreamToRead = new CryptoStream(memoryStreamOfEncodedBytes, decryptor.CreateDecryptor(), CryptoStreamMode.Read);
            using StreamReader reader = new StreamReader(cryptographicStreamToRead, encodingOverride ?? DefaultEncoding);
            string value = reader.ReadToEnd();
            return value;
        }

        public static string Decrypt (ObfuscatedString toDecrypt, string decryptionKey = null, Encoding encodingOverride = null, int? iterationCountOverride = null, HashAlgorithmName? hashAlgorithmOverride = null)
        {
            return CryptoObfuscation.Decrypt(toDecrypt.Active, decryptionKey, encodingOverride, iterationCountOverride, hashAlgorithmOverride);
        }
    }
}
