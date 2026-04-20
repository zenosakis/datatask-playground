using Feature.Encryption;

namespace Test.Encryption
{
    public class TestAesEcbEncryptor
    {
        const string Key = "12345678901234567890123456789012";

        [Fact]
        public void Encryptor_Encrypt_ReturrnsEncrypted()
        {
            var encryptionOptions = new EncryptionOptions(Key);
            var aesECb = new AesEcbEncryptor(encryptionOptions);
            // 암호화 결과는 https://www.devglan.com/online-tools/aes-encryption-decryption 참고
            Assert.Equal("9AmLhIyiCl3fDvjcAkATYQ==", aesECb.Encrypt("테스트"));
        }

        [Fact]
        public void Encryptor_Decrypt_ReturnsDecrypted()
        {
            var encryptionOptions = new EncryptionOptions(Key);
            var aesECb = new AesEcbEncryptor(encryptionOptions);
            Assert.Equal("테스트", aesECb.Decrypt("9AmLhIyiCl3fDvjcAkATYQ=="));
        }

        [Fact]
        public void Encryptor_RoundTrip_ReturnOriginal()
        {
            var encryptionOptions = new EncryptionOptions(Key);
            var aesECb = new AesEcbEncryptor(encryptionOptions);

            var encrypted = aesECb.Encrypt("테스트");
            var decrypted = aesECb.Decrypt(encrypted);

            Assert.Equal("테스트", decrypted);
        }
    }
}
