using System.Security.Cryptography;
using System.Text;

namespace BudgetPilot_API.Services
{
    /// <summary>
    /// Provides AES encryption and decryption for sensitive card data.
    /// Uses a 32-byte key from configuration.
    /// </summary>
    public class DataProtectionService
    {
        private readonly byte[] _key;

        /// <summary>
        /// Initializes a new instance of the DataProtectionService.
        /// </summary>
        /// <param name="configuration">The configuration containing the encryption key.</param>
        public DataProtectionService(IConfiguration configuration)
        {
            var keyBase64 = configuration["Encryption:Key"]
                ?? throw new InvalidOperationException("Encryption key not configured.");

            _key = Convert.FromBase64String(keyBase64);

            if (_key.Length != 32)
                throw new InvalidOperationException("Encryption key must be 32 bytes (256 bits).");
        }

        /// <summary>
        /// Encrypts a plaintext string using AES-256-CBC.
        /// </summary>
        /// <param name="plaintext">The text to encrypt.</param>
        /// <returns>Base64-encoded ciphertext with IV prepended.</returns>
        public string Encrypt(string plaintext)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            var result = new byte[aes.IV.Length + ciphertext.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);

            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Decrypts a base64-encoded ciphertext with prepended IV.
        /// </summary>
        /// <param name="ciphertext">The base64-encoded ciphertext.</param>
        /// <returns>The decrypted plaintext.</returns>
        public string Decrypt(string ciphertext)
        {
            var fullBytes = Convert.FromBase64String(ciphertext);

            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = new byte[16];
            Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
            aes.IV = iv;

            var encryptedData = new byte[fullBytes.Length - 16];
            Buffer.BlockCopy(fullBytes, 16, encryptedData, 0, encryptedData.Length);

            using var decryptor = aes.CreateDecryptor();
            var plaintextBytes = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
    }
}
