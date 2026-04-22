using Microsoft.Extensions.Configuration; // appsettings.json 설정 불러오기 위함. Nuget 패키지 Microsoft.Extensions.Configuration, Microsoft.Extensions.Configuration.Json, Microsoft.Extensions.Configuration.Binder 필요
// 로그용 참조
using Feature.Logger;
using Serilog;
// 설정정보용 참조
using Feature.LoadSettings;
using Feature.Dapper;
using Feature.Encryption;
using Feature.Encryption.Interfaces;
using Microsoft.Data.SqlClient;
// 빌더용 (의존성 주입)
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Data;
using Microsoft.Extensions.Options;
using Feature.Transfer;

//////////////////////////////////////////
/// 의존성 주입 방식: 빌더 사용

// 설정 정보 빌드 -> 설정 정보를 주입하기 위함
//var configuration = new ConfigurationBuilder()
//    .SetBasePath(AppContext.BaseDirectory)
//    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true) // 암호화 키 관련 등 민감 설정파일(git 커밋X) = 옵션
//    .Build();

var builder = Host.CreateApplicationBuilder(args); // 여기에서 appsettings.json, appsettings.{Environment}.json 을 자동으로 등록하기 때문에 appsettings.local.json 만 하면 됨

// configuration 을 쓰지 않고 builder.Configuration 에 직접 등록
// builder.Configuration에 local.json 추가 (자동 로드된 appsettings.json + 환경별 json과 함께 병합됨)
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<EncryptionOptions>(
    //configuration.GetSection("Encryption")
    // 이곳 역시 configuration 을 쓰지 않고 builder.Configuration 에서 직접 가져옴
    builder.Configuration.GetSection("Encryption")
);
builder.Services.AddSingleton<IEncryptor, AesCbcEncryptor>();
builder.Services.AddSingleton<LoadSettingsTest>();
// DB 옵션 설정 추가
builder.Services.Configure<DbOptions>(
    builder.Configuration.GetSection("DB")
);
// DB 옵션을 PostConfigure 추가 -> PostConfigure 가 호출됨 여기서 복호화 진행 (DbOptionsPostConfigure(IEncryptor encryptor) 로 되어있기 때문에 DI 의존성 주입됨)
builder.Services.AddSingleton<IPostConfigureOptions<DbOptions>, DbOptionsPostConfigure>();
// IDbConnection 을 요구하는 곳에서는 SqlConnection 을 생성(DbOptions 정보로 생성함)하여 주입시킴
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<DbOptions>>().Value;
    return new SqlConnection(opt.ToConnectionString());
});
// 실제 사용할 Dapper 클래스 주입 -> 여기선 IDbConnection 을 요구(DI 주입) -> 여기선 IOptions<DbOptions> 를 요구 -> IOptions<DbOptions> 는 이미 Configure 로 등록되어있음 (PostConfigure 를 통해 복호화까지 되어있음)
builder.Services.AddScoped<DapperTest>();

// HTTP
builder.Services.Configure<HttpTransferOptions>(
    builder.Configuration.GetSection("HTTPTransfer")
);
builder.Services.AddScoped<HttpClient>();
builder.Services.AddScoped<HttpTransferClient>();

// 여기서부터 실제 동작할 코드들. builder.Build() 이후에는 등록이 안 됨. 위에 정의된 레시피에 따라 동작함
using var host = builder.Build();

// 로그
SerilogTest.Configure(); // Configure 를 호출해줘야 로그 기록이 시작 됨
Log.Information("=== 콘솔 프로그램 시작 ===");
Log.Debug(AppContext.BaseDirectory); // appsettings.json 위치해야 할 경로

var config = host.Services.GetRequiredService<LoadSettingsTest>();
// 설정 로그 테스트
Log.Information("설정 정보:");
foreach (var pair in config.AsEnumerable())
{
    Log.Information("   {Key} = {Value}", pair.Key, pair.Value);
}
Log.Debug("테스트: {value}", config["MyValue:TestKey"]);
Log.Debug("테스트: {value}", config["DB:Ip"]);

// DB
var dapper = host.Services.GetRequiredService<DapperTest>();

// DB 테스트
var rows = dapper.SelectTest("TBL_DIALEDLOG");
foreach (var row in rows)
{
    Log.Information("   DIALEDKEY: {Key}", row.DIALEDKEY);
}

// HTTP 테스트
var http = host.Services.GetRequiredService<HttpTransferClient>();
await http.GetStreamAsync("/");


Log.Information("=== 콘솔 프로그램 종료 ===");

Log.CloseAndFlush(); // Serilog 버퍼 플러시 및 종료 (마지막 로그 유실 방지)
