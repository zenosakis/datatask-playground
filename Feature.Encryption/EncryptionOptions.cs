using System.Text;

namespace Feature.Encryption
{
    public class EncryptionOptions
    {
        public byte[] Key { get; set; }
        public byte[] Iv { get; set; }

        public EncryptionOptions(string key, string iv) 
        { 
            Key = Encoding.UTF8.GetBytes(key);
            Iv = Encoding.UTF8.GetBytes(iv);
        }
    }
}
