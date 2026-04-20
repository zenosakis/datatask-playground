using Microsoft.Extensions.Options;
using Feature.Encryption.interfaces;

namespace Feature.Dapper
{
    public class DbOptions // Options 로 사용할 POCO 클래스는 기존 생성자를 써야함. 기본 생성자 방식 (C# 12 = .NET 8 이상 지원)은 쓰면 안 됨 => 기존 생성자를 자동으로 생성하지 않음
    {
        public string Ip { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public string ToConnectionString() =>
            $"Server={Ip},{Port};Database={Database};"
          + $"User Id={User};Password={Password};TrustServerCertificate=True;";
    }

    public class DbOptionsPostConfigure(IEncryptor encryptor) : IPostConfigureOptions<DbOptions>
    {
        private readonly IEncryptor _encryptor = encryptor;

        public void PostConfigure(string? name, DbOptions options)
        {
            options.Ip = _encryptor.Unprotect(options.Ip);
            options.Port = _encryptor.Unprotect(options.Port);
            options.Database = _encryptor.Unprotect(options.Database);
            options.User = _encryptor.Unprotect(options.User);
            options.Password = _encryptor.Unprotect(options.Password);
        }
    }
}
