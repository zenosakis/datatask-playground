# /mock-gen — xUnit + Moq 테스트 템플릿 생성 Skill

## Purpose
주어진 C# 클래스의 의존성을 분석하여 xUnit + Moq 기반 단위 테스트 템플릿을 자동 생성한다.
CLAUDE.md의 **Verification** 단계를 자동화한다.

## Trigger
```
/mock-gen <ClassName> [--project <TestProjectName>]
```
- `ClassName`: 테스트 대상 클래스명 (예: `OrderService`)
- `--project`: 테스트 파일을 생성할 프로젝트명 (기본값: `Test.RunConsole` 또는 `*.Tests` 프로젝트)

---

## Workflow

### Step 1 — 대상 클래스 분석
1. `Glob`으로 `**/<ClassName>.cs`를 찾는다.
2. `Read`로 파일을 읽어 다음을 파악한다:
   - **생성자 파라미터**: 모든 의존성과 타입 목록
   - **public 메서드**: 시그니처, 반환 타입, 파라미터
   - **네임스페이스**
3. 각 의존성에 대해 `I<DependencyName>` 인터페이스 존재 여부를 `Glob`으로 확인한다.

### Step 2 — 테스트 프로젝트 확인
1. `Glob`으로 `**/*.Tests.csproj` 또는 `**/Test.*.csproj`를 검색한다.
2. 발견된 테스트 프로젝트의 `<PackageReference>`를 `Read`로 확인한다:
   - `xunit` 패키지 유무
   - `Moq` 패키지 유무
   - 없으면 설치 명령어를 출력한다:
     ```
     dotnet add package xunit
     dotnet add package Moq
     dotnet add package Microsoft.NET.Test.Sdk
     ```

### Step 3 — 테스트 파일 생성

파일 위치: `<TestProject>/<ClassName>Tests.cs`

**생성 템플릿:**
```csharp
using Moq;
using Xunit;
// using <대상 네임스페이스>;
// using <의존성 네임스페이스>;

namespace <TestProject>;

public class <ClassName>Tests
{
    // ── Mocks ───────────────────────────────────────────────
    // 각 의존성 인터페이스마다 Mock<I*> 필드 생성
    private readonly Mock<I<Dep1>> _mock<Dep1> = new();
    private readonly Mock<I<Dep2>> _mock<Dep2> = new();

    // ── SUT (System Under Test) ──────────────────────────────
    private readonly <ClassName> _sut;

    public <ClassName>Tests()
    {
        _sut = new <ClassName>(
            _mock<Dep1>.Object,
            _mock<Dep2>.Object
        );
    }

    // ── Tests ────────────────────────────────────────────────

    [Fact]
    public void <MethodName>_<Scenario>_<ExpectedResult>()
    {
        // Arrange
        // TODO: _mock<Dep1>.Setup(x => x.<Method>(...)).Returns(...);

        // Act
        // var result = _sut.<MethodName>(...);

        // Assert
        // Assert.Equal(expected, result);
        // _mock<Dep1>.Verify(x => x.<Method>(...), Times.Once);
    }

    // TODO: 각 public 메서드마다 아래 시나리오를 추가할 것
    // - 정상 경로 (happy path)
    // - 경계값 (null, empty, max/min)
    // - 예외 발생 시나리오
}
```

### Step 4 — 메서드별 테스트 케이스 스텁 생성

대상 클래스의 각 `public` 메서드마다 다음 세 가지 `[Fact]` 스텁을 생성한다:

| 테스트 유형     | 네이밍 규칙                                      |
|----------------|--------------------------------------------------|
| 정상 경로      | `<Method>_ValidInput_Returns<ExpectedType>()`    |
| null/빈값 처리 | `<Method>_NullInput_ThrowsArgumentException()`   |
| Mock 호출 검증 | `<Method>_Called_Invokes<Dependency><Method>()`  |

### Step 5 — 완료 보고

```
## /mock-gen 결과

| 항목            | 내용                                        |
|-----------------|---------------------------------------------|
| 대상 클래스     | OrderService (3개 public 메서드)            |
| 감지된 의존성   | IOrderRepository, INotificationService      |
| 생성된 파일     | Test.RunConsole/OrderServiceTests.cs        |
| 생성된 테스트   | 9개 스텁 (메서드 3개 × 시나리오 3개)       |
| 주의사항        | IOrderRepository 인터페이스 없음 → /di-refactor 먼저 실행 권장 |
```

---

## Rules
- 테스트 로직은 `// TODO:` 주석으로 남기고, **실제 assert 코드는 사용자가 채운다.**
- 의존성에 인터페이스가 없으면 `/di-refactor`를 먼저 실행하도록 경고를 출력한다.
- 기존 `<ClassName>Tests.cs`가 있으면 덮어쓰지 않고 `<ClassName>Tests.Generated.cs`로 생성한다.
- CLAUDE.md의 **xUnit + Moq 스택** 외 다른 테스트 프레임워크는 사용하지 않는다.
