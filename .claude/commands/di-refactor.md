# /di-refactor — Interface-Based DI Refactor Skill

## Purpose
주어진 C# 클래스를 인터페이스 기반으로 분리하고, Microsoft.Extensions.DependencyInjection 컨테이너에 등록하는 리팩토링을 수행한다.
CLAUDE.md의 **Implementation** 단계를 자동화한다.

## Trigger
```
/di-refactor <ClassName> [<ProjectName>]
```
- `ClassName`: 리팩토링 대상 클래스명 (예: `UserRepository`)
- `ProjectName`: 선택 사항. 명시하지 않으면 현재 컨텍스트에서 추론.

---

## Workflow

### Step 1 — 대상 클래스 탐색
1. `Glob`으로 `**/<ClassName>.cs` 파일을 찾는다.
2. `Read`로 파일 전체를 읽어 다음을 파악한다:
   - 클래스의 public 메서드 시그니처
   - 생성자 파라미터 (의존성)
   - 네임스페이스
   - 이미 인터페이스가 있는지 여부 (`I<ClassName>` 검색)

### Step 2 — 인터페이스 추출
- 클래스의 모든 `public` 메서드를 추출해 `I<ClassName>` 인터페이스를 생성한다.
- 인터페이스 파일은 클래스와 같은 폴더 또는 `Abstractions/` 하위에 생성한다.
- 파일명: `I<ClassName>.cs`

**인터페이스 템플릿:**
```csharp
namespace <Namespace>;

/// <summary>
/// <ClassName>의 추상화 인터페이스.
/// DI 컨테이너를 통해 주입되며, 단위 테스트 시 Moq로 대체된다.
/// </summary>
public interface I<ClassName>
{
    // public 메서드 시그니처 목록
}
```

### Step 3 — 클래스에 인터페이스 적용
- `<ClassName> : I<ClassName>` 으로 수정한다.
- 기존 코드는 **변경 최소화** 원칙을 지킨다.

### Step 4 — DI 등록 확인 및 추가
1. `Glob`으로 `**/ServiceCollectionExtensions.cs` 또는 `**/Program.cs`를 찾는다.
2. 이미 등록되어 있으면 스킵.
3. 없으면 아래 패턴으로 등록 코드를 추가한다:

```csharp
services.AddScoped<I<ClassName>, <ClassName>>();
```

> Singleton/Transient 여부는 클래스 특성에 따라 판단하고, 이유를 코멘트로 남긴다.

### Step 5 — 소비자(Consumer) 업데이트
- `Grep`으로 `new <ClassName>(` 패턴을 검색한다.
- 직접 인스턴스화하는 코드가 있으면 인터페이스 타입으로 교체 제안을 출력한다.
- **자동 수정은 하지 않고** 위치와 변경안을 사용자에게 보고한다.

### Step 6 — 완료 보고
다음 형식으로 결과를 요약한다:

```
## /di-refactor 결과

| 항목        | 내용                              |
|-------------|-----------------------------------|
| 인터페이스  | IUserRepository.cs (신규 생성)    |
| 클래스 수정 | UserRepository : IUserRepository  |
| DI 등록     | services.AddScoped<> 추가 (Program.cs:42) |
| 소비자 알림 | OrderService.cs:17 — 직접 new 사용 중 |
```

---

## Rules
- 기존 로직은 절대 수정하지 않는다. 인터페이스 추출과 등록만 수행한다.
- 이미 인터페이스가 존재하면 중복 생성하지 않고 Step 4부터 시작한다.
- CLAUDE.md의 **SOLID 원칙** (특히 DIP) 준수 여부를 검증 후 보고한다.
