namespace DataTransfer.Core.Interfaces
{
    public interface IEncryptionService
    {
        /// <summary>
        /// Decrypts an encrypted string
        /// </summary>
        /// <param name="encryptedValue">The encrypted string to decrypt</param>
        /// <returns>The decrypted string</returns>
        string Decrypt(string encryptedValue);
        
        /// <summary>
        /// Encrypts a plain text string
        /// </summary>
        /// <param name="plainText">The plain text to encrypt</param>
        /// <returns>The encrypted string</returns>
        string Encrypt(string plainText);
    }
} 