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
// ============================================================================

using Feature.Transfer;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace Test.Transfer
{
    public class TestHttpTransferClient
    {
        // [Q9] Strict 모드 — Setup 하지 않은 호출은 즉시 예외.
        //      오타/매칭 실수를 조용히 넘기지 않아 학습 단계에 유리.
        private readonly Mock<HttpMessageHandler> _handlerMock = new(MockBehavior.Strict);

        [Fact]
        public async Task GetStreamAsync_WhenResponseOk_ReturnsStreamContent()
        {
            // [Q9] disposeHandler: false — HttpClient 가 handler 를 Dispose 하지 않게 함.
            //      Strict 모드에서 Dispose(bool) 호출이 Setup 없이 들어와 폭발하는 것을 방지.
            //      Mock handler 의 수명은 테스트 프레임워크가 관리.
            using var client = new HttpClient(_handlerMock.Object, false);

            // [Q3] Options 는 POCO — Mock 하지 않고 실제 인스턴스로 주입.
            //      BaseAddress 에 http:// 스킴 필수 (없으면 new Uri 가 UriFormatException).
            var options = new HttpTransferOptions()
            {
                BaseAddress = "http://localhost",
                TimeoutSeconds = 60,
            };
            var sut = new HttpTransferClient(client, options);

            // MemoryStream(byte[]) 생성자는 Position=0 에서 시작.
            // Write() + Position=0 리셋을 하지 않아도 돼서 안전.
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("200 Ok"));

            // [Q6] protected virtual SendAsync 는 일반 Setup 으로 접근 불가.
            //      .Protected() + 문자열 "SendAsync" + ItExpr.IsAny<T>() 조합으로 우회.
            // [Q7] Content 가 없으면 GetStreamAsync 가 빈 스트림을 반환.
            //      ReturnsAsync 에 람다(() => new ...) 형태 유지 → 매 호출마다 새 인스턴스
            //      (HttpResponseMessage 가 IDisposable 이라 인스턴스 재사용 시 문제).
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>
                (
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(stream)
                }
                );

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
            _handlerMock.Protected()
                .Verify<Task<HttpResponseMessage>>
                (
                    "SendAsync", Times.Once(),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        [Fact]
        public async Task GetStreamAsync_WithRelativePath_SendsGetToBaseAddressPlusPath()
        {
            using var client = new HttpClient(_handlerMock.Object, false);
            var options = new HttpTransferOptions()
            {
                BaseAddress = "http://localhost",
                TimeoutSeconds = 30
            };
            var sut = new HttpTransferClient(client, options);

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>
                (
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([])
                });

            await using var _ = await sut.GetStreamAsync("/some");

            _handlerMock.Protected()
                .Verify<Task<HttpResponseMessage>>
                (
                    "SendAsync", Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath == "/some"),
                    ItExpr.IsAny<CancellationToken>()
                );
        }
    }
}
