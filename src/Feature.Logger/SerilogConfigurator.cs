using Serilog; // Serilog 참조해야 쓸 수 있음 -> NuGet 패키지 Serilog 필요
using Serilog.Core; // FixedLevelEnricher 클래스를 위함
using Serilog.Events; // FixedLevelEnricher 클래스를 위함

namespace Feature.Logger
{
    public sealed class FixedLevelEnricher : ILogEventEnricher
    {
        public void Enrich(
            LogEvent logEvent,
            ILogEventPropertyFactory propertyFactory)
        {
            var levelName = logEvent.Level switch
            {
                LogEventLevel.Verbose => "TRACE",
                LogEventLevel.Debug => "DEBUG",
                LogEventLevel.Information => "INFO ",
                LogEventLevel.Warning => "WARN ",
                LogEventLevel.Error => "ERROR",
                LogEventLevel.Fatal => "FATAL",
                _ => "UNKWN"
            };

            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty(
                    "LevelName",
                    levelName));
        }
    }

    public class SerilogConfigurator
    {
        /// <summary>
        /// <para>각 스타트 프로젝트(ASP.NET Core 또는 서비스 등)의 Program.cs 에서 SerilogConfigurator.Configure(); 를 호출해야 함<br/>
        /// 이 후 어느 프로젝트에서든 using Serilog; 를 참조 후 Log.Information("=== 서비스 가동 ==="); 와 같이 사용하면 됨</para>
        /// </summary>
        public static void Configure()
        {
            // 예시 : [2026-07-10 17:35:49.503] [INF] 애플리케이션 시작
            const string fileOutputTemplate =
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] " +
                "[{LevelName}] " +
                "{Message:lj}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With<FixedLevelEnricher>()
                // 콘솔 출력 ->  NuGet 패키지 Serilog.Sinks.Console 필요
                .WriteTo.Console()
                // 일자별 파일 저장 (날짜별 파일 생성: log-20260407.txt) -> NuGet 패키지 Serilog.Sinks.File 필요
                .WriteTo.File(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "logs",
                        "log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: fileOutputTemplate)
                // 작업별 분리는 Serilog.Sinks.Map 사용 (nuget 설치 필요)
                // Loki 전송은 Serilog.Sinks.Grafana.Loki 사용 (nuget 설치 필요)
                .CreateLogger();
        }
    }
}
