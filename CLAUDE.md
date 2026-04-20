# CLAUDE.md for C# Playground

## Project Guidelines
- **Target Framework**: .NET 8.0
- **Architecture**: SOLID 원칙과 Clean Architecture 적용.
- **DI**: Microsoft.Extensions.DependencyInjection을 기본으로 사용.
- **Test**: xUnit + Moq 스택 고정.

## Working Process (Harness Loop)
1. **Planning**: 새로운 기능 추가 전 `implementation_plan.md`를 작성하고 설계를 검토한 후에 구현 시작.
2. **Implementation**: 코드를 작성할 때 인터페이스를 먼저 정의하고 구현체를 DI 컨테이너에 등록할 것.
3. **Verification**: xUnit과 Moq를 사용하여 단위 테스트를 작성하고, 모든 테스트를 통과한 이후에만 커밋할 것.

## C# Naming Convention

| 대상 | 규칙 | 예시 |
|------|------|------|
| Public Property (`get; set;`) | PascalCase, 언더바 없음 | `UserPath`, `IsEnabled` |
| Private Field (멤버 변수) | `_camelCase` (언더바 + 소문자 시작) | `_userName`, `_dbConnection` |
| Constant (`const`) | PascalCase, 언더바 없음 | `Prefix`, `MaxRetryCount` |
| Method | PascalCase, 언더바 없음 | `GetUser()`, `SelectTest()` |
| Local Variable | camelCase, 언더바 없음 | `userPath`, `encryptor` |

> `.editorconfig` 파일에 Roslyn 분석기 규칙으로 등록되어 있음.

### 예외 — 테스트 메서드 (`Tests/**/*.cs`)
- xUnit 관례상 `대상_시나리오_기대결과` 형태의 **언더바 허용**.
- 예: `Indexer_Get_WhenKeyMissing_ReturnsNull`, `Encrypt_WithEmptyString_ThrowsArgumentException`.
- `.editorconfig`에서 `Tests/**/*.cs` 경로에 한해 `method_pascal` 규칙을 `severity = none`으로 비활성화함.

## Key Commands
- `/plan`: 계획 없이 구현 시작 방지 — 구현 계획을 먼저 작성.
- `/di-check`: DI 등록 현황 조회 및 라이프타임(Scoped, Singleton 등) 문제 탐지.
- `/test-all`: 모든 xUnit 테스트 실행 및 결과 확인.
- `/di-refactor <ClassName>`: 클래스를 인터페이스 기반으로 분리하고 DI 등록. (`.claude/commands/di-refactor.md`)
- `/mock-gen <ClassName>`: xUnit + Moq 단위 테스트 템플릿 자동 생성. (`.claude/commands/mock-gen.md`)

## Agents
서브 에이전트는 `agents/` 폴더에 정의되어 있으며, 워크플로우 단계를 분담한다.

| 에이전트          | 파일                              | 담당 단계       |
|-------------------|-----------------------------------|-----------------|
| Architect Agent   | `agents/architect-agent.md`       | Planning        |
| DI Auditor        | `agents/di-auditor.md`            | Implementation  |
| Unit Tester       | `agents/unit-tester.md`           | Verification    |

### 에이전트 협업 흐름
```
새 기능 요청
    → Architect Agent (설계 + implementation_plan.md)
    → /di-refactor (인터페이스 분리)
    → DI Auditor (DI 등록 검증)
    → /mock-gen (테스트 스텁 생성)
    → Unit Tester (테스트 구현 + dotnet test)
```
