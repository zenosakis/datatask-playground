using Microsoft.Extensions.Configuration; // Nuget 패키지 Microsoft.Extensions.Configuration.Abstractions 필요
using Feature.Encryption;
using System.Text;
using Microsoft.Extensions.Primitives;
using System.Reflection.Metadata;
using Feature.Encryption.interfaces;

namespace Feature.LoadSettings
{
    public class LoadSettingsTest: IConfiguration
    {
        private readonly IConfiguration _config;

        private readonly byte[] _Key = [];
        private readonly byte[] _Iv = [];
        private readonly IEncryptor _Encryptor;

        public LoadSettingsTest(IConfiguration config)
        {
            _config = config;
            if (config != null)
            {
                var key = config["Key"];
                if (key != null )
                    _Key = Encoding.UTF8.GetBytes(key);
                var iv = config["Iv"];
                if (iv != null)
                    _Iv = Encoding.UTF8.GetBytes(iv);
            }
            _Encryptor = new AesCbcEncryptor(_Key, _Iv);
        }

        public string? this[String key]
        {
            get
            {
                var value = _config[key];
                return value is null ? null : _Encryptor.Unprotect(value);
            }
            set => _config[key] = value;
        }

        // 나머지는 IConfiguration 원본에 그대로 위임
        public IEnumerable<KeyValuePair<string, string?>> AsEnumerable() => _config.AsEnumerable();
        public IConfigurationSection GetSection(string key) => _config.GetSection(key);
        public IEnumerable<IConfigurationSection> GetChildren() => _config.GetChildren();
        public IChangeToken GetReloadToken() => _config.GetReloadToken();
    }
}
