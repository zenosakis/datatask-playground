using Microsoft.Extensions.Configuration; // Nuget 패키지 Microsoft.Extensions.Configuration.Abstractions 필요
using Microsoft.Extensions.Primitives;
using Feature.Encryption.Interfaces;

namespace Feature.LoadSettings
{
    public class LoadSettingsTest
    {
        private readonly IConfiguration _config;
        private readonly IEncryptor _encryptor;

        public LoadSettingsTest(IConfiguration config, IEncryptor encryptor)
        {
            _config = config;
            _encryptor = encryptor;
        }

        public string? this[String key]
        {
            get
            {
                var value = _config[key];
                return value is null ? null : _encryptor.Unprotect(value);
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
