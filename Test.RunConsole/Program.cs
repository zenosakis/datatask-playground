using Microsoft.Extensions.Configuration; // appsettings.json 설정 불러오기 위함. Nuget 패키지 Microsoft.Extensions.Configuration, Microsoft.Extensions.Configuration.Json, Microsoft.Extensions.Configuration.Binder 필요
// 로그용 참조
using Feature.Logger;
using Serilog;
using Microsoft.Extensions.Logging;
// 설정정보용 참조
using Feature.LoadSettings;

SerilogTest.Configure(); // Configure 를 호출해줘야 로그 기록이 시작 됨

Log.Information("=== 콘솔 프로그램 시작 ===");

Log.Debug(AppContext.BaseDirectory); // appsettings.json 위치해야 할 경로

// 설정 정보 빌드 -> 설정 정보를 주입하기 위함
var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true); // optional: false 는 appsettings.json 이 꼭 필수라는 의미

// 로그 정보 빌드 -> Serilog 를 주입하기 위함
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.ClearProviders();
    builder.AddSerilog(); // Serilog <- Microsoft.Extensions.Logging로 브릿지 => NuGet "Serilog.Extensions.Logging" 필요
});
var logger = loggerFactory.CreateLogger<LoadSettingsTest>();

LoadSettingsTest config = new LoadSettingsTest(builder.Build(), logger);

//Log.Information("Configuration: {MyValue}", config["MyKey"]);
config.ShowSetting();


Log.Information("=== 콘솔 프로그램 종료 ===");

Log.CloseAndFlush(); // Serilog 버퍼 플러시 및 종료 (마지막 로그 유실 방지)