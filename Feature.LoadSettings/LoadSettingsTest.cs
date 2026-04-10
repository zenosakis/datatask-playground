using Microsoft.Extensions.Configuration; // Nuget 패키지 Microsoft.Extensions.Configuration.Abstractions 필요
using Microsoft.Extensions.Logging; // Serilog 대신 추상화 사용 -> 보통 설정 레이어가 로거 레이어보다 하위에 있어야 하기 때문

namespace Feature.LoadSettings
{
    public class LoadSettingsTest
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LoadSettingsTest> _logger; // 나중에 시작점 프로젝트에서 Serilog 를 주입받아 동작하기 때문에 추상화만 사용

        public LoadSettingsTest(IConfiguration config, ILogger<LoadSettingsTest> logger)
        {
            _config = config;
            _logger = logger;
        }

        public void ShowSetting()
        {
            _logger.LogInformation("설정 정보:");
            foreach (var pair in _config.AsEnumerable())
            {
                _logger.LogInformation("   {Key} = {Value}", pair.Key, pair.Value);
            }
            
        }
    }
}
