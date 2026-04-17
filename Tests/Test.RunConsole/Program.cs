using Microsoft.Extensions.Configuration; // appsettings.json 설정 불러오기 위함. Nuget 패키지 Microsoft.Extensions.Configuration, Microsoft.Extensions.Configuration.Json, Microsoft.Extensions.Configuration.Binder 필요
// 로그용 참조
using Feature.Logger;
using Serilog;
// 설정정보용 참조
using Feature.LoadSettings;
using Feature.Dapper;
using Feature.Encryption;
using Feature.Encryption.interfaces;
using Microsoft.Data.SqlClient;

//////////////////////////////////////////
/// 의존성 주입 방식: Pure DI (직접 new 로 생성한 다음, 사용하는 클래스에 직접 넣어주는 방식)

// 설정 정보 빌드 -> 설정 정보를 주입하기 위함
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // optional: false 는 appsettings.json 이 꼭 필수라는 의미
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true) // 암호화 키 관련 등 민감 설정파일(git 커밋X) = 옵션
    .Build();

// 로그
SerilogTest.Configure(); // Configure 를 호출해줘야 로그 기록이 시작 됨
Log.Information("=== 콘솔 프로그램 시작 ===");
Log.Debug(AppContext.BaseDirectory); // appsettings.json 위치해야 할 경로

// 암호화 관련 모듈
EncryptionOptions encryptionOptions = new EncryptionOptions(configuration["Key"], configuration["Iv"]);
IEncryptor encryptor = new AesCbcEncryptor(encryptionOptions);

// 설정 로드
LoadSettingsTest config = new LoadSettingsTest(configuration, encryptor);

// 설정 로그 테스트
Log.Information("설정 정보:");
if (config != null)
{
    foreach (var pair in config.AsEnumerable())
    {
        Log.Information("   {Key} = {Value}", pair.Key, pair.Value);
    }
}
Log.Debug("테스트: {value}", config["TestKey"]);
Log.Debug("테스트: {value}", config["DB:Ip"]);


// DB
var connection = new SqlConnection($"Server={config["DB:Ip"]},{config["DB:Port"]};Database={config["DB:Database"]};User Id={config["DB:User"]};Password={config["DB:Password"]};TrustServerCertificate=True;");
var dapper = new DapperTest(connection);

// DB 테스트
var rows = dapper.SelectTest("TBL_DIALEDLOG");
foreach (var row in rows)
{
    Log.Information("   DIALEDKEY: {Key}", row.DIALEDKEY);
}


Log.Information("=== 콘솔 프로그램 종료 ===");

Log.CloseAndFlush(); // Serilog 버퍼 플러시 및 종료 (마지막 로그 유실 방지)
