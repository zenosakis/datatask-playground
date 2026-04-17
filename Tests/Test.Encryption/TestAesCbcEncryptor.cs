using Feature.Encryption;

namespace Test.Encryption
{
    public class TestAesCbcEncryptor
    {
        const string Key = "12345678901234567890123456789012";
        const string Iv = "1234567890123456";

        [Fact]
        public void EncryptionAesCbc_Encrypt_Test()
        {
            var encryptionOptions = new EncryptionOptions(Key, Iv);
            var aesCbc = new AesCbcEncryptor(encryptionOptions);
            // 암호화 결과는 https://www.devglan.com/online-tools/aes-encryption-decryption 참고
            Assert.Equal("zg13oJBqhA68jrgIx43iBw==", aesCbc.Encrypt("테스트"));
        }

        [Fact]
        public void EncryptionAesCbc_Decrypt_Test()
        {
            var encryptionOptions = new EncryptionOptions(Key, Iv);
            var aesCbc = new AesCbcEncryptor(encryptionOptions);
            Assert.Equal("테스트", aesCbc.Decrypt("zg13oJBqhA68jrgIx43iBw=="));
        }

        [Fact]
        public void EncryptionAescbc_RoundTrip_Test()
        {
            var encryptionOptions = new EncryptionOptions(Key, Iv);
            var aesCbc = new AesCbcEncryptor(encryptionOptions);

            var encrypted = aesCbc.Encrypt("테스트");
            var decrypted = aesCbc.Decrypt(encrypted);

            Assert.Equal("테스트", decrypted);
        }
    }
}
