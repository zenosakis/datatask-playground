using System.Security.Cryptography;
using System.Text;
using Feature.Encryption.interfaces;

namespace Feature.Encryption
{
    public class AesEcbEncryptor : IEncryptor
    {
        private readonly byte[] _Key;
        private readonly byte[] _IV;
        private readonly PaddingMode _paddingMode;

        public AesEcbEncryptor(byte[] key, byte[] iv, PaddingMode paddingMode = PaddingMode.PKCS7)
        {
            _Key = key;
            _IV = iv;
            _paddingMode = paddingMode;
        }

        public string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _Key;
            aes.IV = _IV;

            var encrypted = aes.EncryptEcb(Encoding.UTF8.GetBytes(plainText), _paddingMode);

            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string encryptedValue)
        {
            using var aes = Aes.Create();
            aes.Key = _Key;
            aes.IV = _IV;

            var cipher = Convert.FromBase64String(encryptedValue);

            return Encoding.UTF8.GetString(aes.DecryptEcb(cipher, _paddingMode));
        }
    }
}
