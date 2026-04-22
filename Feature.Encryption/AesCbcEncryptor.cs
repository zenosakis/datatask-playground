using Feature.Encryption.Interfaces;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

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

        public AesCbcEncryptor(IOptions<EncryptionOptions> options, PaddingMode paddingMode = PaddingMode.PKCS7)
        {
            _options = options.Value;
            _paddingMode = paddingMode;
        }

        public string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _options.KeyBytes;
            aes.IV = _options.IvBytes;

            var encrypted = aes.EncryptCbc(Encoding.UTF8.GetBytes(plainText), _options.IvBytes, _paddingMode);

            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string encryptedValue)
        {
            using var aes = Aes.Create();
            aes.Key = _options.KeyBytes;
            aes.IV = _options.IvBytes;

            var cipher = Convert.FromBase64String(encryptedValue);

            return Encoding.UTF8.GetString(aes.DecryptCbc(cipher, _options.IvBytes, _paddingMode));
        }
    }
}
