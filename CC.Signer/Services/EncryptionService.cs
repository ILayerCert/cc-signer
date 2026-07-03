using System;
using System.IO;
using System.Security.Cryptography;
using CC.Signer.Models;

namespace CC.Signer.Services;

public class EncryptionService
{
    private const int KeySizeBytes = 32; // AES-256
    private const int IvSizeBytes = 12;  // GCM nonce
    private const int TagSizeBytes = 16; // GCM authentication tag

    /// <summary>
    /// Encrypts the plaintext with a randomly generated AES-256-GCM key.
    /// Returns the encrypted result and the key (as base64).
    /// The encrypted file format: [IV (12 bytes)] [Tag (16 bytes)] [Ciphertext]
    /// </summary>
    public SaveResult EncryptAndSave(string plaintext, string outputDir, string? fileName = null)
    {
        try
        {
            var key = new byte[KeySizeBytes];
            RandomNumberGenerator.Fill(key);

            var iv = new byte[IvSizeBytes];
            RandomNumberGenerator.Fill(iv);

            var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSizeBytes];

            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Encrypt(iv, plainBytes, cipherBytes, tag);

            // Format: [IV] [Tag] [Ciphertext]
            using var ms = new MemoryStream();
            ms.Write(iv);
            ms.Write(tag);
            ms.Write(cipherBytes);
            var encryptedData = ms.ToArray();

            // Ensure output directory exists
            Directory.CreateDirectory(outputDir);

            // Save encrypted file
            fileName ??= $"cc-sign-{DateTime.Now:yyyyMMdd-HHmmss}.enc";
            var filePath = Path.Combine(outputDir, fileName);
            File.WriteAllBytes(filePath, encryptedData);

            // Save key file alongside
            var keyFileName = Path.GetFileNameWithoutExtension(fileName) + ".key";
            var keyFilePath = Path.Combine(outputDir, keyFileName);
            var keyBase64 = Convert.ToBase64String(key);
            File.WriteAllText(keyFilePath, keyBase64);

            return new SaveResult
            {
                Success = true,
                FilePath = filePath,
                KeyFilePath = keyFilePath,
                EncryptionKey = keyBase64
            };
        }
        catch (Exception ex)
        {
            return new SaveResult { Error = ex.Message };
        }
    }

    /// <summary>
    /// Decrypts a file encrypted by this service using the provided key.
    /// Used by iLayerCert to read back the signature.
    /// </summary>
    public static string? Decrypt(string filePath, string keyBase64)
    {
        try
        {
            var key = Convert.FromBase64String(keyBase64);
            var encryptedData = File.ReadAllBytes(filePath);

            using var ms = new MemoryStream(encryptedData);
            var iv = new byte[IvSizeBytes];
            var tag = new byte[TagSizeBytes];
            ms.Read(iv, 0, IvSizeBytes);
            ms.Read(tag, 0, TagSizeBytes);
            var cipherBytes = new byte[encryptedData.Length - IvSizeBytes - TagSizeBytes];
            ms.Read(cipherBytes, 0, cipherBytes.Length);

            var plainBytes = new byte[cipherBytes.Length];
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Decrypt(iv, cipherBytes, tag, plainBytes);

            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }
}
