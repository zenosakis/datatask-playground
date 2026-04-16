using Feature.Encryption.interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Feature.Encryption
{
    public class AesCbcEncryptor: IEncryptor
    {
        private readonly EncryptionOptions _options;
        private readonly PaddingMode _paddingMode;

        public AesCbcEncryptor(EncryptionOptions options, PaddingMode paddingMode = PaddingMode.PKCS7)
        {
            _options = options;
            _paddingMode = paddingMode;
        }

        public string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _options.Key;
            aes.IV = _options.Iv;

            var encrypted = aes.EncryptCbc(Encoding.UTF8.GetBytes(plainText), _options.Iv, _paddingMode);

            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string encryptedValue)
        {
            using var aes = Aes.Create();
            aes.Key = _options.Key;
            aes.IV = _options.Iv;

            var cipher = Convert.FromBase64String(encryptedValue);

            return Encoding.UTF8.GetString(aes.DecryptCbc(cipher, _options.Iv, _paddingMode));
        }
    }
}
