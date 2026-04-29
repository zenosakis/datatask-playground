// ============================================================================
// HttpTransferClient 테스트 학습 여정 기록
// ============================================================================
// 이 파일은 "HttpClient 를 감싼 SUT 를 xUnit + Moq 으로 테스트하기" 를
// 밑바닥부터 배우며 부딪힌 문제들을 해결해 온 궤적입니다.
//
// [Q1] Moq 의 Returns 셋팅에서 stream 에 컴파일 에러가 납니다. 왜?
//   - 원인: HttpClient.GetStreamAsync 는 Task<Stream> 을 반환하는데
//           Returns(MemoryStream) 로 Stream 을 넘겨서 타입 불일치(CS1503).
//   - 해결: .ReturnsAsync(stream) 사용 → Task<Stream> 으로 자동 래핑.
//
// [Q2] HttpClient 자체를 Mock 할 수 있나요?
//   - 원인: HttpClient 의 메서드는 virtual 이 아니라서 Moq 로 가로챌 수 없음.
//           Moq 는 Castle DynamicProxy 로 "서브클래스를 런타임에 생성 + override"
//           하는 방식이라 virtual / abstract / protected virtual 멤버만 가능.
//   - 해결: HttpClient 를 Mock 하지 말고, 내부 의존인 HttpMessageHandler 를 Mock.
//           HttpMessageHandler 는 abstract 클래스 + SendAsync 가 protected virtual.
//           Mock<HttpMessageHandler> 로 만든 뒤 new HttpClient(handlerMock.Object)
//           로 진짜 HttpClient 에 주입하면, SendAsync 만 가짜로 가로챌 수 있음.
//
// [Q3] HttpTransferOptions 는 Mock 했는데도 BaseAddress Setup 이 안 먹힙니다.
//      ctor 에서 new Uri("") 로 UriFormatException 이 나요.
//   - 원인 ①: HttpTransferOptions 의 프로퍼티들이 virtual 이 아니라서
//             _options.Setup(o => o.BaseAddress).Returns(...) 가 무시됨.
//             Mock 객체는 원본 기본값(string.Empty) 을 그대로 돌려줌.
//   - 원인 ②: xUnit 은 [Fact] 마다 테스트 클래스를 새로 생성.
//             테스트 클래스 ctor → SUT ctor → new Uri(BaseAddress) 가
//             테스트 메서드 본문의 Setup 보다 먼저 실행됨. 순서상 이미 늦음.
//   - 해결: Options 같은 POCO 는 Mock 대상이 아님. 실제 인스턴스 new 해서 주입.
//           new HttpTransferOptions { BaseAddress = "http://...", ... }
//
// [Q4] HttpTransferClient 는 아예 Mock 테스트를 못 하나요?
//   - 해결: 3가지 선택지가 있음.
//       A. HttpMessageHandler 를 Mock (.NET 표준 패턴) ← 이 파일이 선택한 방식
//       B. HttpClient 를 IHttpInvoker 같은 인터페이스로 감싸기 (SOLID/DI 관점)
//       C. IHttpClientFactory + HttpMessageHandler Mock (실무 패턴)
//
// [Q5] HttpResponseMessage 를 직접 반환 타입으로 바꾸면 테스트가 훨씬 간단해지지 않을까?
//   - 원인: "테스트가 쉬워진다" 는 이유로 프로덕션 API 를 바꾸는 건
//           test-induced design damage (안티패턴).
//   - 해결: IDataTransferClient 의 추상화(HTTP 비종속) 의도를 깨고
//           호출자에게 HTTP 지식/Dispose 책임을 전가하게 됨.
//           → 그대로 두고 테스트 복잡도를 감수하는 것이 건강한 선택.
//
// [Q6] SendAsync 는 protected 인데 어떻게 Setup 하나요?
//   - 원인: 람다 h => h.SendAsync(...) 는 외부 코드에서 접근 불가 (컴파일 에러).
//   - 해결: Moq.Protected 네임스페이스의 .Protected() 확장 사용.
//           메서드는 "SendAsync" 문자열로, 인자 매처는 It.IsAny 대신
//           ItExpr.IsAny<T>() 로 지정. (델파이의 RTTI 와 비슷해 보이지만
//           실제로는 "서브클래스 override 를 문자열로 지정" 하는 우회 기법)
//
// [Q7] HttpResponseMessage() 에 뭘 넣어야 하나요?
//   - 원인: GetStreamAsync 는 내부적으로 response.Content.ReadAsStreamAsync() 호출.
//           Content 가 없으면 빈 스트림이 돌아옴.
//   - 해결: new HttpResponseMessage(HttpStatusCode.OK)
//             { Content = new StreamContent(stream) }
//           + ReturnsAsync 를 람다(() => new ...) 형태로 유지.
//             (HttpResponseMessage 는 IDisposable — 인스턴스 고정으로 전달하면
//              두 번째 호출 시 ObjectDisposedException. 람다로 매번 새로 생성.)
//
// [Q8] stream 과 result 를 어떻게 비교하나요?
//   - 원인 ①: result 는 Task<Stream> — await 없이 비교하면 영원히 다름.
//   - 원인 ②: HttpClient.GetStreamAsync 는 원본 stream 을 그대로 반환한다는
//             보장이 없음 (래퍼/버퍼링 스트림일 수 있음).
//             → Assert.Same / Assert.Equal (참조 동등) 은 실패 가능.
//   - 해결: 테스트를 async Task 로 바꾸고 await.
//           Stream 내용(bytes/string) 을 읽어 내용 기반으로 Assert.Equal.
//
// [Q9] MockBehavior.Strict 로 바꿨더니 Dispose 에서 MockException 이 터져요.
//   - 원인: HttpClient(handler) 생성자는 disposeHandler=true 가 기본값.
//           즉 "HttpClient 가 Dispose 될 때 handler 도 함께 Dispose" 가 약속.
//           using var client → 스코프 종료 시 handler.Dispose(true) 호출 →
//           Strict 모드의 "Setup 안 된 호출은 모두 예외" 에 걸려 폭발.
//   - 해결: new HttpClient(_handlerMock.Object, disposeHandler: false)
//           로 소유권 이전을 끊음. Mock handler 의 수명은 테스트가 관리.
//
// [Q10] IDisposable 인지 어떻게 알아보나요? HttpClient 선언부에 IDisposable 이
//       안 보이던데요?
//   - 원인: HttpClient : HttpMessageInvoker 만 적혀 있고, IDisposable 은
//           부모 HttpMessageInvoker 가 구현. 인터페이스는 상속되므로 HttpClient
//           도 IDisposable 이지만 선언부에는 안 적힘. 또한 partial class 라
//           상속 선언이 다른 파일에 있을 수도 있음.
//   - 해결: 가장 빠른 확인 = "using var x = new T(...);" 시도해 컴파일 여부 확인.
//           또는 F12 로 부모 클래스 따라 올라가기, IDE 의 Type Hierarchy 창,
//           공식 문서의 Implements 섹션 확인.
//
// [Q11] var 와 타입 명시는 언제 구분해서 써야 하나요?
//   - 원칙: 우변에서 타입이 명백하면 var (DRY + 가독성 + 리팩터 저항성).
//           내부 메서드의 반환 타입이 이름만으로 불명확하면 타입 명시.
//           StreamReader.ReadToEnd() 같은 BCL 관용구는 var 가 일반적.
//
// [Q12] 같은 명제를 여러 입력으로 검증하려면? ([Fact] vs [Theory])
//   - 원인: [Fact] 는 "이 시나리오에서 이렇게 된다" 한 점만 찍음.
//           "비-2xx 응답이면 모두 throw" 같은 일반화된 명제를 한 케이스(404)
//           로만 검증하면 이름(WhenResponseIsNotSuccess)이 본문보다 넓은 약속을
//           하게 됨 — 이름이 거짓말을 함.
//   - 해결: [Theory] + [InlineData(...)] 조합.
//           xUnit 이 [InlineData] 개수만큼 테스트 인스턴스를 복제 → 각 인자를
//           메서드 매개변수에 1:1 매핑 → 독립 테스트로 실행.
//           한 케이스만 깨져도 어느 입력에서 깨졌는지 러너 출력에서 즉시 식별
//           가능 (별개 줄로 표시).
//           Setup 과 Assert 양쪽에서 매개변수를 사용해야 일반성이 진짜로 검증됨.
//   - 함정 ①: [InlineData] 는 **컴파일 상수만** 가능.
//             가능: enum / 숫자 / string / bool / typeof(X) / null / 1차원 배열
//             불가: new HttpResponseMessage(...), Guid.NewGuid(), 람다, 익명 객체
//             동적/객체 케이스가 필요하면 [MemberData] (정적 멤버) 또는
//             [ClassData] (별도 클래스) 로 우회.
//   - 함정 ②: [InlineData] 인자 개수 ≠ 메서드 시그니처 불일치는
//             **컴파일이 아닌 런타임 실패** 로 잡힘.
//   - 적용 기준: 본문에 if (input == X) Assert.A else Assert.B 같은 분기가
//                생기면 [Theory] 자격 미달 — 명제가 다른 두 동작이므로
//                [Fact] 두 개로 분리. [Theory] 는 "동일한 행동 명제를 입력만
//                바꿔 검증" 할 때만.
//
// [Q13] HttpResponseMessage 의 본문은 StringContent 프로퍼티가 아니라 Content.
//   - 원인: HttpResponseMessage 의 본문 슬롯은 프로퍼티 하나 — Content (타입은
//           추상 클래스 HttpContent).
//           StringContent / StreamContent / ByteArrayContent 는 프로퍼티가 아니라
//           HttpContent 의 자손 *타입*(클래스 이름). 슬롯에 꽂을 구체 값일 뿐.
//   - 해결: new HttpResponseMessage() { Content = new StringContent("...") }
//           프로퍼티 = 슬롯(Content), 우변 = 어떤 HttpContent 자손을 꽂을지 결정.
//           OOP 다형성 패턴 — 슬롯은 추상, 값은 구체 자손.
//   - 컴파일 에러 읽기: "HttpResponseMessage does not contain a definition for
//                       'StringContent'" → "StringContent 라는 *프로퍼티*는 없다"
//                       로 정확히 읽기. StringContent 는 타입이니까.
//
// [Q14] Setup<T> 의 T 는 반환 타입 — HttpRequestMessage 와 헷갈리지 말 것.
//   - 원인: SendAsync 시그니처는
//           Task<HttpResponseMessage> SendAsync(HttpRequestMessage, CancellationToken).
//           Setup<T> 의 T 는 *반환 타입* 자리 → Task<HttpResponseMessage>.
//           ItExpr.IsAny<T>() 의 T 는 *파라미터 타입* 자리 → HttpRequestMessage 등.
//           이름이 닮은 두 타입이 한 호출 안에 등장해 혼동하기 쉬움.
//   - 에러 메시지 읽기: "Expression of type 'Task<HttpResponseMessage>' cannot be
//                       used for return type 'Task<HttpRequestMessage>'" →
//                       ReturnsAsync 가 넘긴 응답 객체가 Setup<T> 가 약속한 반환
//                       타입과 안 맞음 → Setup<T> 의 T 가 잘못 적힌 것.
//   - 자리별 매칭법:
//        Setup<...>                       ← 반환 타입: Task<HttpResponseMessage>
//        ItExpr.IsAny<...>() (1번째)       ← 파라미터: HttpRequestMessage
//        ItExpr.IsAny<...>() (2번째)       ← 파라미터: CancellationToken
//
// [Q15] await using var _ = sut.GetStringAsync(...) 는 왜 안 되나?
//   - 원인 ①: GetStringAsync 의 결과(await 후)는 string. string 은 IDisposable /
//             IAsyncDisposable 둘 다 구현하지 않음 → using/await using 자격 미달.
//   - 원인 ②: await using 은 두 일을 동시에 함 —
//             (1) `var x = expr` 로 대입 (await 키워드는 우변에 안 붙음)
//             (2) 스코프 종료 시 await x.DisposeAsync() 호출
//             x 의 컴파일 타임 타입이 IAsyncDisposable 이어야 (2) 성립.
//   - 비교: GetStreamAsync → await 후 Stream → Stream 은 IDisposable + IAsyncDisposable
//          → await using var _ = await sut.GetStreamAsync(...) 성립.
//          GetStringAsync → await 후 string → 자원 없음 → using 못 붙임.
//   - 해결: 결과를 안 쓸 때는 그냥 `await sut.GetStringAsync("/...")`.
//          명시적 discard 가 필요하면 `_ = await sut.GetStringAsync("/...")`.
//   - 원칙: using/await using 은 "해제할 자원이 있을 때" 만. string/int/Task 결과
//          같은 자원 없는 값에는 붙이지 않음.
//
// [Q16] 보일러플레이트를 ctor 로 추출할 때 [Q9] 의 함정이 어떻게 부활하나?
//      그리고 xUnit 라이프사이클(ctor/Dispose) 은 NUnit [SetUp]/[TearDown] 을 어떻게 대체하나?
//   - 배경: 6 개 테스트가 같은 SUT 셋업을 반복하던 시점에서, 매 테스트마다 만들던
//           `using var client = new HttpClient(_handlerMock.Object, false)` 를
//           테스트 클래스 ctor 의 `_client` 필드 초기화로 옮기는 추출 리팩토링.
//
//   - 함정 ①: 옮기는 과정에서 두 번째 인자 `disposeHandler: false` 를 빠뜨리기 쉬움.
//             이 시점에는 `using` 도 함께 사라지므로 client.Dispose() 가 호출되지
//             않음 → handler.Dispose(true) 도 호출 안 됨 → 테스트 통과.
//             → "지금은 잘 돌아가니까 OK" 라고 착각하기 쉬움.
//   - 함정 ②: 미래에 HttpClient 누수 정리하려고 테스트 클래스를 IDisposable 로
//             만들면 즉시 [Q9] 의 MockException 부활. 잠복하던 함정이 한 번에 폭발.
//             → 시한폭탄을 코드에 심는 효과.
//
//   - 해결: 두 가지를 **동시에** 적용해야 완전 봉인.
//           ① ctor 에서 `disposeHandler: false` 명시 — handler 와의 수명 분리.
//           ② 테스트 클래스 `: IDisposable` + `Dispose() => _client.Dispose();`
//              — xUnit 이 매 테스트 후 자동 호출 → 누수 방지.
//
//   - 한 단계 더 — xUnit 라이프사이클의 정석 (NUnit 과 비교):
//
//       NUnit                          xUnit
//       ─────────────                  ──────────────────────────
//       [SetUp] 메서드            →    테스트 클래스 ctor (매 테스트마다 호출)
//       [TearDown] 메서드         →    IDisposable.Dispose() (매 테스트 후 호출)
//       클래스 인스턴스 1개을     →    매 테스트마다 새 인스턴스 (강한 격리)
//       모든 테스트가 공유
//
//     xUnit 은 별도 어트리뷰트 없이 **C# 표준 라이프사이클 메커니즘(ctor/Dispose)
//     을 그대로 활용**. 따라서 readonly 필드 초기화도 매 테스트마다 새로 실행되어
//     안전 — 예: `private readonly Mock<...> _handlerMock = new(Strict);`
//
//     이 모델 덕분에 테스트 간 상태 누수가 구조적으로 막힘 — NUnit 에서 흔한
//     "이전 테스트가 남긴 mock 셋업 때문에 다음 테스트가 오염" 이슈가 원천 차단.
//
//   - 추가 옵션 (참고용 — 지금 단계에선 안 씀):
//       [Fact]/[Theory] 마다 인스턴스    ← 기본 동작. 강한 격리.
//       IClassFixture<T>                 ← 한 클래스 내 모든 테스트가 T 공유
//                                           (DB 컨테이너 같은 무거운 리소스용).
//       ICollectionFixture<T>            ← 여러 테스트 클래스가 T 공유.
//
//   - 원칙: 추출(extract) 리팩토링은 **행동 변화 없는 변환** 이어야 함. 한 줄을
//          옮길 때도 모든 인자/생성자 옵션을 보존했는지 점검. 빠뜨린 한 인자가
//          미래의 함정이 됨. 학습 일지에 적어둔 [Q9] 같은 기존 함정들이
//          리팩토링 시 다시 떠오르도록 자기 점검 체크리스트로 활용.
// ============================================================================

using Feature.Transfer;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace Feature.Transfer.Tests
{
    public class HttpTransferClientTests : IDisposable
    {
        // [Q9] Strict 모드 — Setup 하지 않은 호출은 즉시 예외.
        //      오타/매칭 실수를 조용히 넘기지 않아 학습 단계에 유리.
        private readonly Mock<HttpMessageHandler> _handlerMock = new(MockBehavior.Strict);
        private readonly HttpClient _client;

        public HttpTransferClientTests()
        {
            _client = new HttpClient(_handlerMock.Object, disposeHandler: false);
        }

        private HttpTransferClient CreateSut(
            string baseAddress = "http://localhost",
            int timeoutSeconds = 60)
        {
            var options = new HttpTransferOptions()
            {
                BaseAddress = baseAddress,
                TimeoutSeconds = timeoutSeconds,
            };
            return new HttpTransferClient(_client, options);
        }

        public void Dispose() => _client.Dispose();

        [Fact]
        public async Task GetStreamAsync_WhenResponseOk_ReturnsStreamContent()
        {
            var sut = CreateSut("http://localhost", 60);

            // MemoryStream(byte[]) 생성자는 Position=0 에서 시작.
            // Write() + Position=0 리셋을 하지 않아도 돼서 안전.
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("200 Ok"));

            // [Q6] protected virtual SendAsync 는 일반 Setup 으로 접근 불가.
            //      .Protected() + 문자열 "SendAsync" + ItExpr.IsAny<T>() 조합으로 우회.
            // [Q7] Content 가 없으면 GetStreamAsync 가 빈 스트림을 반환.
            //      ReturnsAsync 에 람다(() => new ...) 형태 유지 → 매 호출마다 새 인스턴스
            //      (HttpResponseMessage 가 IDisposable 이라 인스턴스 재사용 시 문제).
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(stream)
                });

            await using var result = await sut.GetStreamAsync("/");
            using var streamReader = new StreamReader(result);
            var text = await streamReader.ReadToEndAsync();

            // [Q8] 참조 동등(Assert.Same) 이 아닌 내용 동등으로 단언.
            //      HttpClient 가 원본 stream 을 그대로 돌려준다는 보장이 없으므로
            //      "어떤 바이트/문자열이 흘러나왔는가" 를 검증하는 것이 의미 있음.
            Assert.Equal("200 Ok", text);

            // SendAsync 가 정확히 1번 호출됐는지 확인.
            // (구현이 HttpClient 를 우회해 하드코딩된 값을 반환해도 Assert.Equal 은
            //  통과할 수 있으므로, 실제로 HTTP 파이프라인을 탔는지 검증.)
            _handlerMock
                .Protected()
                .Verify<Task<HttpResponseMessage>>(
                    "SendAsync", Times.Once(),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        [Fact]
        public async Task GetStreamAsync_WithRelativePath_SendsGetToBaseAddressPlusPath()
        {
            var sut = CreateSut("http://localhost", 60);

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([])
                });

            await using var _ = await sut.GetStreamAsync("/some");

            _handlerMock
                .Protected()
                .Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath == "/some"),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task GetStreamAsync_WhenResponseIsNotSuccess_ThrowsHttpRequestException(HttpStatusCode statusCode)
        {
            var sut = CreateSut("http://localhost", 60);

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(statusCode));

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetStreamAsync("/"));
            Assert.Equal(statusCode, ex.StatusCode);
        }

        [Fact]
        public async Task GetStringAsync_WhenResponseOk_ReturnsStringContent()
        {
            var sut = CreateSut("http://localhost", 60);

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("200 Ok")
                });

            Assert.Equal("200 Ok", await sut.GetStringAsync("/"));

            _handlerMock
                .Protected()
                .Verify<Task<HttpResponseMessage>>(
                    "SendAsync", Times.Once(),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        [Fact]
        public async Task GetStringAsync_WithRelativePath_SendsGetToBaseAddressPlusPath()
        {
            var sut = CreateSut("http://localhost", 60);

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK));

            await sut.GetStringAsync("/some");

            _handlerMock
                .Protected()
                .Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath == "/some"),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task GetStringAsync_WhenResponseIsNotSuccess_ThrowsHttpRequestException(HttpStatusCode statusCode)
        {
            var sut = CreateSut("http://localhost", 60);

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(statusCode));

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetStringAsync("/"));
            Assert.Equal(statusCode, ex.StatusCode);
        }
    }
}
