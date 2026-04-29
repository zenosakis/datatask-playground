using System.Text;

namespace Feature.Encryption
{
    public class EncryptionOptions
    {
        public string Key { get; set; } = string.Empty;
        public string? Iv { get; set; }

        public byte[] KeyBytes => string.IsNullOrEmpty(Key) // Key 는 non-nullable 이기 때문에, ?? 가 먹히지 않으므로 IsNullOrEmpty 로 체크
            ? throw new InvalidOperationException("Encryption Key is not configured.")
            : Encoding.UTF8.GetBytes(Key);
        public byte[] IvBytes => string.IsNullOrEmpty(Iv) // Iv 는 nullable 이기 때문에, ?? 로도 할 수 있지만, "" 로 셋팅되는 경우 문제가 발생하므로 IsNullOrEmpty 로 체크
            ? throw new InvalidOperationException("Encryption IV is not configured.")
            : Encoding.UTF8.GetBytes(Iv);

        public EncryptionOptions() { }

        public EncryptionOptions(string key, string iv = "")
        {
            Key = key;
            Iv = iv;
        }
    }
}
