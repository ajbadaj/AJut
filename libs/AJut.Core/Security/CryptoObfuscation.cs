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
        // FNV-style hash seed pinned to the x86 baseline of the old String.GetHashCode(). The crypto
        // seed - and therefore the ciphertext - must not vary by process bitness, so we deliberately
        // do not use the bitness-dependent GenerateStableHashCode extension to derive it.
        private const int kFixedHashSeed = 5381;

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
            var rng = new Random(GenerateBitnessIndependentHashCode(seedString));
            var charList = seedString.ToList();
            for (int i = 0; i < 20; ++i)
            {
                charList.Add((char)rng.Next(32, 126));
            }

            charList.Randomize(rng.Next);
            return String.Join("", charList);
        }

        /// <summary>
        /// Sets <paramref name="aes"/>'s key and IV, derived from <paramref name="key"/> (or the seeded default key when it is
        /// null) the way the strings this class has made were derived.
        /// </summary>
        private static void ApplyDerivedKeyAndIV (Aes aes, string key, int? iterationCountOverride, HashAlgorithmName? hashAlgorithmOverride)
        {
            // AES-256 takes a 32 byte key, and CBC mode a 16 byte IV
            const int kKeyByteCount = 32;
            const int kIVByteCount = 16;

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

            Random rng = new Random(GenerateBitnessIndependentHashCode(key));
            byte[] crytpoSalt = new byte[rng.Next(8, 16)];
            rng.NextBytes(crytpoSalt);

            // The strings this class has made were keyed by an Rfc2898DeriveBytes object asked for 32 bytes and then 16.
            //  .NET 10 makes that constructor obsolete (SYSLIB0060) in favor of the one-shot Rfc2898DeriveBytes.Pbkdf2, and
            //  the one-shot has to give the very same bytes, or no stored string decrypts:
            // * PBKDF2's output is one stream that the object's GetBytes calls read in order, so the first 32 bytes of a
            //   single 48 byte Pbkdf2 call are the old key, and the next 16 are the old IV.
            // * The old constructor encoded the key with Encoding.UTF8, which writes the replacement character for a lone
            //   surrogate. Pbkdf2's string overloads encode with a UTF-8 encoder that throws on one instead. RandomizeAndSalt
            //   shuffles the key's characters, so a key holding an emoji (a surrogate pair) can come out with lone
            //   surrogates, and the string overload would throw for it. So the key is encoded here with Encoding.UTF8, as it
            //   always was, and handed to the byte overload.
            // Changing any of it (the sizes, their order, the encoding, the salt) breaks every string already stored.
            //  StoredObfuscatedStringTests pins strings made the old way, and runs on Windows, Android and macOS.
            // https://aka.ms/dotnet-warnings/SYSLIB0060
            // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/Rfc2898DeriveBytes.OneShot.cs
            byte[] keyAndIV = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(key),
                crytpoSalt,
                iterations,
                hashAlgorithm,
                kKeyByteCount + kIVByteCount
            );

            aes.Key = keyAndIV[..kKeyByteCount];
            aes.IV = keyAndIV[kKeyByteCount..];
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
            byte[] bytesToEncode = (encodingOverride ?? DefaultEncoding).GetBytes(toEncrypt);

            using Aes encryptor = Aes.Create();
            encryptor.Padding = PaddingMode.PKCS7;
            ApplyDerivedKeyAndIV(encryptor, encryptionKey, iterationCountOverride, hashAlgorithmOverride);
            using var ms = new MemoryStream();
            using CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(bytesToEncode, 0, bytesToEncode.Length);
            cs.FlushFinalBlock();
            cs.Close();

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt (string toDecrypt, string decryptionKey = null, Encoding encodingOverride = null, int? iterationCountOverride = null, HashAlgorithmName? hashAlgorithmOverride = null)
        {
            byte[] encodedBytes = Convert.FromBase64String(toDecrypt);
            using Aes decryptor = Aes.Create();
            decryptor.Padding = PaddingMode.PKCS7;
            ApplyDerivedKeyAndIV(decryptor, decryptionKey, iterationCountOverride, hashAlgorithmOverride);
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

        /// <summary>
        /// A copy of <see cref="StringXT.GenerateStableHashCode(string)"/> with the hash seed pinned to
        /// the x86 baseline (<see cref="kFixedHashSeed"/>) so the result never depends on process bitness.
        /// This is what keeps the obfuscated ciphertext identical across x86 and x64 processes.
        /// </summary>
        private static int GenerateBitnessIndependentHashCode (string source)
        {
            int hash1 = kFixedHashSeed;
            int hash2 = hash1;

            for (int index = 0; index < source.Length && source[index] != '\0'; index += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ source[index];
                if (index == source.Length - 1 || source[index + 1] == '\0')
                {
                    break;
                }

                hash2 = ((hash2 << 5) + hash2) ^ source[index + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }
    }
}
