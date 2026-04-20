using Feature.Encryption;
using System.Text;

namespace Test.Encryption
{
    public class TestEncryptOptions
    {
        const string Key = "12345678901234567890123456789012";
        const string Iv = "1234567890123456";

        [Fact]
        public void EncryptionOptions_GeyKeyBytes_ReturnsKeyBytes()
        {
            var encryptionOptions = new EncryptionOptions(Key, Iv);
            Assert.Equal(Encoding.UTF8.GetBytes(Key), encryptionOptions.KeyBytes);
        }

        [Fact]
        public void EncryptionOptions_GetIvBytes_ReturnsIvBytes()
        {
            var encryptionOptions = new EncryptionOptions(Key, Iv);
            Assert.Equal(Encoding.UTF8.GetBytes(Iv), encryptionOptions.IvBytes);
        }
    }
}
