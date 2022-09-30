﻿namespace AJut.Security
{
    using System;
    using System.IO;
    using System.Linq;
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
        private static string g_key;

        public static void Seed (string key)
        {
            g_key = RandomizeAndSalt(key);
        }

        public static string RandomizeAndSalt (string seedString)
        {
            Random rng = new Random(seedString.GetHashCode());
            var charList = seedString.ToList();
            for (int i = 0; i < 20; ++i)
            {
                charList.Add((char)rng.Next(32, 126));
            }

            charList.Randomize(rng.Next);
            return String.Join("", charList);
        }

        private static Rfc2898DeriveBytes CreateCryptoBytes (string key)
        {
            Random rng = new Random(key.GetHashCode());
            byte[] crytpoSalt = new byte[rng.Next(8, 16)];
            rng.NextBytes(crytpoSalt);
            return new Rfc2898DeriveBytes(g_key, crytpoSalt);
        }

        public static string Encrypt (string toEncrypt, string key = null)
        {
            byte[] clearBytes = Encoding.Unicode.GetBytes(toEncrypt);
            using (Aes encryptor = Aes.Create())
            {
                encryptor.Padding = PaddingMode.PKCS7;
                var cryptoBytes = CreateCryptoBytes(key ?? g_key);
                encryptor.Key = cryptoBytes.GetBytes(32);
                encryptor.IV = cryptoBytes.GetBytes(16);
                using (var ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt (string toDecrypt, string key = null)
        {
            byte[] cipherBytes = Convert.FromBase64String(toDecrypt.Replace(" ", "+"));
            using (Aes decryptor = Aes.Create())
            {
                decryptor.Padding = PaddingMode.PKCS7;
                var cryptoBytes = CreateCryptoBytes(key ?? g_key);
                decryptor.Key = cryptoBytes.GetBytes(32);
                decryptor.IV = cryptoBytes.GetBytes(16);
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, decryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }

                    return Encoding.Unicode.GetString(ms.ToArray());
                }
            }
        }

        public static string Decrypt (ObfuscatedString toDecrypt, string key = null)
        {
            return CryptoObfuscation.Decrypt(toDecrypt.Active, key);
        }
    }
}
