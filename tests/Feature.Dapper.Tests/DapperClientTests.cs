// [Q1] HttpMessageHandler 처럼 SqlMapper 를 Moq 으로 가로챌 수 있나?
//   - 시도: Mock<SqlMapper> 또는 Mock<IDbConnection> 으로 Query 셋업.
//   - 원인 ①: SqlMapper 는 static class — 서브클래스 자체 불가.
//   - 원인 ②: Query 는 static 확장 메서드 — 컴파일러가 c.Query(...) 를
//             SqlMapper.Query(c, ...) 로 *정적 호출* 변환. vtable 안 거침.
//   - 원리: Moq(Castle DynamicProxy) = "서브클래스 + virtual override" 메커니즘.
//          static 디스패치에는 가로챌 *지점이 없음*.
//   - HttpTransferClient [Q2] 와의 비교:
//     [Q2] non-virtual 인스턴스 메서드 Mock 불가 — 내부 의존(HttpMessageHandler)이
//          abstract+virtual 이라 *우회 가능* 했음.
//     [Q1] static 확장 메서드 Mock 불가 — 우회로 없음 (BCL 깊게 가야 함).
//   - 일반화: Moq 가능 = "override 가능한가?"
//     · interface 멤버 / abstract / virtual / protected virtual → 가능
//     · static / 확장 / non-virtual / sealed → 불가

// [Q2] Setup(c => c.Query(...)) 시 어떤 오류가 나나?
//   - 시나리오 A (컴파일 타임): "Cannot infer type arguments for SqlMapper.Query<T>"
//   - 시나리오 B (런타임): NotSupportedException —
//     "Extension methods (here: SqlMapper.Query) may not be used in setup expressions"
//   - 의미: Moq 이 직접 *확장 메서드는 셋업 불가* 라고 거부. [Q1] 의 공식 증명.
//   - 위치 함정: 오류가 It.IsAny<string>() 줄에서 보이지만 *진짜 원인* 은
//                전체 람다가 static 호출이라는 것.

// [Q3] 왜 DapperClient 자체를 단위 테스트하기 *어색* 했나?
//   - 처음 DapperClient: ctor + _dbConnection.Query(...) 1줄 — *thin wrapper*.
//   - 결정/변환/검증 로직 *없음* → 단위 테스트가 Mock 셋업이 곧 검증 본문이 되는
//     기묘한 구조. 테스트 가치 거의 0.
//   - 본인 직관 ("이거 테스트할 게 없는데?") 이 정확했음.
//   - 해결: SUT 에 *진짜 로직* 부여. SEC-001 (SQL Injection 화이트리스트) 추가.
//     · 허용/거부 분기 → 검증 명제 명확
//     · _allowedTables HashSet 으로 화이트리스트 보관
//     · 미허용 시 ArgumentException 던짐
//   - 원칙: "결정/변환/검증" 이 있는 클래스만 단위 테스트 가치가 있음.

// [Q4] IDapperAdapter — 인터페이스는 IDbConnection 을 가지나?
//   - 아니다. 인터페이스는 *메서드 시그니처만* 선언. 필드/상태/구현 없음.
//   - 구조:
//     · IDapperAdapter (interface)  — IEnumerable<dynamic> Query(string sql) 시그니처만
//     · DapperAdapter  (구현체)     — IDbConnection 을 ctor 로 받아 보관.
//                                     내부에서 connection.Query(sql) 호출.
//     · DapperClient                — IDapperAdapter 의존. Adapter 호출.
//   - 호출 체인: Program.cs → SqlConnection → DapperAdapter(conn) → DapperClient(adapter)
//   - 효과:
//     · DapperClient 테스트에서 *우리가 정의한* IDapperAdapter 를 Mock — 처음부터 가능
//     · Dapper 의 static API 는 DapperAdapter 한 곳에만 등장 — 격리됨
//   - DapperAdapter 자체는 thin wrapper 라 단위 테스트 안 함 (통합 테스트 영역 — SQLite/Testcontainers).
//   - 의존성 역전 원칙(DIP) 의 구체 적용 사례.

// [Q5] _dapperMock.SetupGet(a => a.Query(...)) 에서
//      "Expression is not a property access" 오류.
//   - 원인: SetupGet 은 *프로퍼티 접근만* 받음. 람다에 메서드 호출 (Query(...)) 들어가면 거부.
//   - 해결: Setup 으로 변경 (메서드용).
//   - 헷갈린 이유: SettingsLoaderTests 의 SetupGet(c => c["missing"]) 잔상.
//                인덱서는 *프로퍼티의 일종* (get_Item) 이라 SetupGet 가능했던 것.
//   - 일반화 규칙:
//     · Setup       — 메서드 (Method())
//     · SetupGet    — 프로퍼티 getter / 인덱서 (Property, this[key])
//     · SetupSet    — 프로퍼티/인덱서 setter (Property = ..., this[key] = ...)
//     · 동일 분류가 Verify / VerifyGet / VerifySet 에도 적용.
//   - 단순 식별: "( ) 가 있으면 메서드 → Setup, 없으면 프로퍼티 → SetupGet"
//                (예외: 인덱서는 [ ] 가 있어도 프로퍼티 — get_Item)

// [Q6] _dapperMock.Setup(a => a.Query(...)).CallBase() 에서
//      "DynamicProxy2: interceptor attempted to Proceed" 오류.
//   - 원인: .CallBase() = "베이스 구현 호출". 인터페이스의 추상 메서드엔 *구현 없음*
//          → Proceed 할 대상 없음 → DynamicProxy 폭발.
//   - 해결: Returns(...) 사용. 반환값에 관심 없으면 Enumerable.Empty<dynamic>() 또는
//          new List<dynamic>() 같은 placeholder.
//   - 헷갈린 이유: SettingsLoaderTests 의 IEncryptor.Unprotect 에선 .CallBase() 동작.
//                IEncryptor 가 default interface method 또는 abstract 클래스의 구체 메서드라
//                구현이 *존재했음*. 일반 인터페이스 추상 메서드와 다름.
//   - .CallBase() 적용 가능 케이스:
//     · abstract 클래스의 구체 메서드 (구현 있음)
//     · 클래스의 virtual 메서드
//     · 인터페이스 default method (.NET 8+, 본문 있음)
//   - 적용 불가:
//     · 인터페이스 추상 메서드 ← 본 케이스

using System.Data.Common;
using Feature.Dapper.Interfaces;
using Moq;

namespace Feature.Dapper.Tests;

public class DapperClientTests
{
    private readonly Mock<IDapperAdapter> _dapperMock = new();
    private readonly DapperClient _sut;

    public DapperClientTests()
    {
        _sut = new DapperClient(_dapperMock.Object);
    }

    [Fact]
    public void SelectTop10_WithAllowedTable_CallsQuery()
    {
        const string tableName = "TBL_DIALEDLOG";
        _dapperMock.Setup(dbAdapter => dbAdapter.Query(It.IsAny<string>())).Returns(new List<dynamic>());
        _ = _sut.SelectTop10(tableName);

        _dapperMock.Verify(dbAdapter => dbAdapter.Query(It.Is<string>(sql => sql.Contains(tableName))), Times.Once);
    }

    [Fact]
    public void SelectTop10_WithDisallowedTable__ThrowsAndDoesNotCallAdapter()
    {
        // tableName은 "TBL_DIALEDLOG" 만 허용함
        const string tableName = "DISALLOWED_TABLE";
        var ex = Assert.Throws<ArgumentException>(() => _sut.SelectTop10(tableName));
        Assert.Contains(tableName, ex.Message);
        _dapperMock.Verify(dbAdapter => dbAdapter.Query(It.IsAny<string>()), Times.Never);
    }

    // (선택) MockBehavior.Strict 도입 — HttpTransferClient 패턴 일관성
}
