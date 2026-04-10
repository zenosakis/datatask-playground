using Serilog; // Serilog 참조해야 쓸 수 있음

namespace Feature.Logger
{
    public class SerilogTest
    {
        /// <summary>
        /// 각 스타트 프로젝트(ASP.NET Core 또는 서비스 등)의 Program.cs 에서 SerilogTest.Configure(); 를 호출해야 함
        /// 이 후 어느 프로젝트에서든 using Serilog; 를 참조 후 Log.Information("=== 서비스 가동 ==="); 와 같이 사용하면 됨
        /// </summary>
        public static void Configure()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                // 콘솔 출력
                .WriteTo.Console()
                // 일자별 파일 저장 (날짜별 파일 생성: log-20260407.txt)
                .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
                // 작업별 분리는 Serilog.Sinks.Map 사용 (nuget 설치 필요)
                // Loki 전송은 Serilog.Sinks.Grafana.Loki 사용 (nuget 설치 필요)
                .CreateLogger();

            Log.Information("=== 시스템 시작 ===");
        }
    }
}
