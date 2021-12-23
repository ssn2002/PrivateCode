using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProjectFactory.Cryptography
{
    /// <summary>
    /// Encrypter class. 
    /// </summary>
    public class Encryptor
    {
        #region Private Fields

        /// <summary>
        /// The password for encryption/decryption
        /// </summary>
        private readonly string password;
        
        #endregion

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the Encryptor class.
        /// </summary>
        /// <param name="password">The password for encryption/decryption</param>
        public Encryptor(string password)
        {
            if (password == null)
            {
                throw new EncryptorException("Password cannot be null.");
            }

            this.password = password;         
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Encrypts text
        /// </summary>
        /// <param name="text">The text to encrypt</param>
        /// <returns>The encrypted text</returns>
        public string EncryptString(string text)
        {
            if (text == null)
            {
                throw new EncryptorException("Input text cannot be null.");
            }

            // Create an instance of the Rihndael class. 
            RijndaelManaged rijndaelCipher = new RijndaelManaged();

            PasswordDeriveBytes secretKey = GetSecretKey();

            // First we need to turn the input strings into a byte array.
            byte[] textData = System.Text.Encoding.Unicode.GetBytes(text);

            // Create a encryptor from the existing secretKey bytes.
            // We use 32 bytes for the secret key. The default Rijndael 
            // key length is 256 bit (32 bytes) and then 16 bytes for the 
            // Initialization Vector (IV). The default Rijndael IV length is 
            // 128 bit (16 bytes).
            ICryptoTransform encryptor = rijndaelCipher.CreateEncryptor(secretKey.GetBytes(32), secretKey.GetBytes(16));

            MemoryStream memoryStream = null;
            CryptoStream cryptoStream = null;
            byte[] encryptedData = null;

            // Create a MemoryStream that is going to hold the encrypted bytes:
            try
            {
                using (memoryStream = new MemoryStream())
                {
                    // Create a CryptoStream through which we are going to be processing 
                    // our data. CryptoStreamMode.Write means that we are going to be 
                    // writing data to the stream and the output will be written in the 
                    // MemoryStream we have provided.
                    using (cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        // Start the encryption process.
                        cryptoStream.Write(textData, 0, textData.Length);

                        // Finish encrypting.
                        cryptoStream.FlushFinalBlock();

                        // Convert our encrypted data from a memoryStream into a byte array.
                        encryptedData = memoryStream.ToArray();

                        // Close both streams.
                        memoryStream.Close();
                        cryptoStream.Close();
                    }
                }
            }
            finally
            {
                memoryStream = null;
                cryptoStream = null;
            }

            // Convert encrypted data into a base64-encoded string.
            // A common mistake would be to use an Encoding class for that.
            // It does not work, because not all byte values can be
            // represented by characters. We are going to be using Base64 encoding.
            // That is designed exactly for what we are trying to do.
            string encryptedText = Convert.ToBase64String(encryptedData);

            // Return encrypted string.
            return encryptedText;
        }

        /// <summary>
        /// Decrypts encrypted text
        /// </summary>
        /// <param name="encryptedText">The encrypted text</param>
        /// <returns>The decrypted text</returns>
        public string DecryptString(string encryptedText)
        {
            if (encryptedText == null)
            {
                throw new EncryptorException("Input text cannot be null.");
            }

            // Create an instance of the Rihndael class. 
            RijndaelManaged rijndaelCipher = new RijndaelManaged();

            PasswordDeriveBytes secretKey = GetSecretKey();

            // First we need to turn the input strings into a byte array.
            byte[] encryptedData = Convert.FromBase64String(encryptedText);

            // Create a decryptor from the existing SecretKey bytes.
            ICryptoTransform decryptor = rijndaelCipher.CreateDecryptor(secretKey.GetBytes(32), secretKey.GetBytes(16));

            MemoryStream memoryStream = null;
            CryptoStream cryptoStream = null;
            byte[] unencryptedData = null;
            int decryptedDataLength;

            try
            {
                using (memoryStream = new MemoryStream(encryptedData))
                {
                    // Create a CryptoStream. Always use Read mode for decryption.
                    using (cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        // Since at this point we don't know what the size of decrypted data
                        // will be, allocate the buffer long enough to hold EncryptedData;
                        // DecryptedData is never longer than EncryptedData.
                        unencryptedData = new byte[encryptedData.Length];

                        // Start decrypting.
                        decryptedDataLength = cryptoStream.Read(unencryptedData, 0, unencryptedData.Length);

                        memoryStream.Close();
                        cryptoStream.Close();
                    }
                }
            }
            finally
            {
                memoryStream = null;
                cryptoStream = null;
            }

            // Convert decrypted data into a string.
            string decryptedText = Encoding.Unicode.GetString(unencryptedData, 0, decryptedDataLength);

            // Return decrypted string.  
            return decryptedText;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets an instance of the secret key
        /// </summary>
        /// <returns>The secret key</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.Int32.ToString", Justification = "This is a special case")]
        private PasswordDeriveBytes GetSecretKey()
        {
            // We are using salt to make it harder to guess our key
            // using a dictionary attack.
            byte[] salt = Encoding.ASCII.GetBytes(password.Length.ToString());

            // The Secret Key will be generated from the specified
            // password and salt.
            return new PasswordDeriveBytes(password, salt);
        }
        
        #endregion
    }
}