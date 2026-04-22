using Feature.Encryption.Interfaces;
using Feature.LoadSettings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Moq;

namespace Test.LoadSettings
{
    public class TestLoadSettings
    {
        private readonly Mock<IConfiguration> _configMock = new();
        private readonly Mock<IEncryptor> _encryptorMock = new();
        private readonly LoadSettingsTest _sut; // SUT = System Under Tests (관례적 명칭이라고 함)

        public TestLoadSettings()
        {
            _sut = new LoadSettingsTest(_configMock.Object, _encryptorMock.Object);
        }

        [Fact]
        public void Indexer_Get_WhenKeyMissing_ReturnsNull()
        {
            // IConfiguration 에 c["missing"] 가 요청이 온다면 null 로 리턴해라
            // = 실제 환경에서는 null 로 리턴할 것이지만, 검증하기 위해 확실한 조건을 걸어주는 것
            // 확실하게 null 이라는 리턴을 리턴받기 위함 -> 즉, 리턴이 null 일 때 Decrypt 를 호출하지 않는게 맞는지 검증하기 위한 조건 설정
            _configMock.SetupGet(c => c["missing"]).Returns((string?)null);

            // xUnit 테스트 - missing 으로 get 했을 때 진짜 null 로 오는지? (바로 위 SetupGet 으로 Null 로 셋팅했기 떄문
            var result = _sut["missing"];
            Assert.Null(result);

            // Moq.Verify => 검증하고자 하는 메소드가 테스트 도중에 특정 호출을 받았는지 검증
            // It.IsAny<string>() => 어떤 문자열이 들어가든 상관없음
            // Times.Never => 절대 호출되면 안 됨
            // 즉, 테스트 도중 Decrypt 가 한번도 호출된 적이 없는지를 검증하기 위함
            // 1. missing 으로 get 요청이 오면 null 을 리턴해라
            // 2. missing 으로 null 인지 테스트 진행
            // 3. 실제로 Decrypt 가 호출이 된 것인지 확인 (리턴값이 null 이기 때문에, Decrypt가 호출되면 안 됨)
            _encryptorMock.Verify(e => e.Unprotect(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Indexer_Get_WhenPlainText_ReturnsOriginal()
        {
            // Unprotect 는 원래 동작하던대로 동작하게끔 설정 (Unprotect 안에서 조건에 따라 Decrypt 를 호출하도록)
            _encryptorMock.Setup(e => e.Unprotect(It.IsAny<string>())).CallBase();
            _configMock.SetupGet(c => c["DB:Ip"]).Returns("19.19.20.73");

            var result = _sut["DB:Ip"];
            Assert.Equal("19.19.20.73", result);

            // 평문을 Unprotect 로 가져올 때, Decrypt 메소드를 안탄게 맞는지 검증
            _encryptorMock.Verify(e => e.Decrypt(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Indexer_Get_WhenEncrypted_ReturnsDecrypted()
        {
            // Unprotect 는 원래 동작하던대로 동작하게끔 설정 (Unprotect 안에서 조건에 따라 Decrypt 를 호출하도록)
            _encryptorMock.Setup(e => e.Unprotect(It.IsAny<string>())).CallBase();
            // Decrypt 에서 복호화해서 리턴 주도록 설정
            _encryptorMock.Setup(e => e.Decrypt("H77CGZZEKpABxC3jCOI3NA==")).Returns("19.19.20.73");
            _configMock.SetupGet(c => c["DB:Ip"]).Returns("ENC(H77CGZZEKpABxC3jCOI3NA==)");


            var result = _sut["DB:Ip"];
            Assert.Equal("19.19.20.73", result);

            // 평문을 Unprotect 로 가져올 때, Decrypt 메소드를 1회만 실행된것이 맞는지 검증
            _encryptorMock.Verify(e => e.Decrypt(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void Indexer_Set_DelegatesToConfiguration() // 인덱서 Set 위임 테스트
        {
            // _sut 은 LoadSettingsTest 인데, 여기에 set 을 했을 때, _configMock 인 IConfiguration 에 정상적으로 위임이 됐는가를 테스트 검증
            // 실제로 string? this[string key] 에 set 을 set => _test = value; 로 바꾸고 테스트 해보니 검증 오류 발생함 (한번도 호출되지 않았다)
            // 즉, set 메소드를 누가 잘 못 수정했을 때를 대비하기 위한 검증으로 활용 가능
            _sut["DB:Ip"] = "19.19.20.73";
            _configMock.VerifySet(c => c["DB:Ip"] = "19.19.20.73", Times.Once);
        }

        [Fact]
        public void GetSection_DelegatesToConfiguration() // GetSection 위임 테스트
        {
            var sectionMock = new Mock<IConfigurationSection>();
            _configMock.Setup(c => c.GetSection("DB")).Returns(sectionMock.Object);

            var result = _sut.GetSection("DB");

            Assert.Same(sectionMock.Object, result);
            _configMock.Verify(c => c.GetSection("DB"), Times.Once);
        }

        [Fact]
        public void GetChildren_DelegatesToConfiguration() // GetChildren 위임 테스트
        {
            var childrenMock = new Mock<IEnumerable<IConfigurationSection>>();
            _configMock.Setup(c => c.GetChildren()).Returns(childrenMock.Object);

            var result = _sut.GetChildren();

            Assert.Same(childrenMock.Object, result);
            _configMock.Verify(c => c.GetChildren(), Times.Once);
        }
        // AsEnumerable 함수는 GetChildren 을 재귀호출하는 정적 확장 메소드이기 때문에, Moq 가 intercept 할 수 없어서 테스트 함수로 구현 힘듦 (GetChildren 으로 검증하고 넘어가는 것이 좋음)

        [Fact]
        public void GetReloadToken_DelegatesToConfiguration() // GetReloadToken 위임 테스트
        {
            var changeToken = new Mock<IChangeToken>();
            _configMock.Setup(c => c.GetReloadToken()).Returns(changeToken.Object);

            var result = _sut.GetReloadToken();

            Assert.Same(changeToken.Object, result);
            _configMock.Verify(c => c.GetReloadToken(), Times.Once);
        }
    }
}
