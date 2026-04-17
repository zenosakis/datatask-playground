using System.Text;

namespace Feature.Encryption
{
    public class EncryptionOptions
    {
        public string? Key { get; set; }
        public string? Iv { get; set; }

        public byte[] KeyBytes => Encoding.UTF8.GetBytes(Key ?? throw new InvalidOperationException("Encryption Key is not configured."));
        public byte[] IvBytes => Encoding.UTF8.GetBytes(Iv ?? throw new InvalidOperationException("Encryption IV is not configured."));

        public EncryptionOptions() { }

        public EncryptionOptions(string key, string iv)
        {
            Key = key;
            Iv = iv;
        }
    }
}
