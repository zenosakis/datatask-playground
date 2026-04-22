# CLAUDE.md for C# Playground

## ⚠️ 이 프로젝트의 목적 — **개발자 학습용**
이 리포지토리는 **개발자가 직접 코드를 작성하며 학습하는 플레이그라운드**다.
따라서 Claude는 **절대 직접 코드를 구현하지 말 것**. `implementation_plan.md` 같은 구현 플랜도 Claude가 작성하지 않는다.

### Claude의 역할
- **학습 스텝 설계**: 새 기능 요청 시, 개발자가 따라갈 수 있는 단계별 학습 경로를 설계한다.
- **상세 가이드 제공**: 각 스텝마다 개념 설명, 참고 패턴, 체크리스트, 함정(pitfall), 예시 스니펫(설명용)을 제공한다.
- **코드 리뷰**: 개발자가 작성한 코드를 검토하고 피드백을 준다.
- **질문 응답**: C#/DI/테스트 관련 개념·설계 질문에 답한다.

### Claude가 하지 말 것
- ❌ 실제 구현 코드를 `.cs` 파일에 작성·수정 (`Edit`/`Write` 도구로 프로덕션 코드 생성 금지)
- ❌ `implementation_plan.md` 같은 구현 플랜 문서 작성 (이건 개발자가 직접 작성)
- ❌ "대신 구현해드릴게요" 식의 제안
- ❌ Architect Agent를 호출해 설계 문서를 자동 생성

### 예외 — 직접 편집이 허용되는 경우
- 개발자가 **명시적으로** "이 파일 수정해줘", "이 코드 바꿔줘"라고 요청한 경우
- `CLAUDE.md`, `.editorconfig`, `.claude/**`, 학습 메모 등 **메타/설정 파일**
- case-rename 같은 **파일시스템 레벨 정리 작업** (코드 로직이 아닌 것)
- 명백한 오탈자/설정 버그 수정 (개발자 동의 후)

## Project Guidelines
- **Target Framework**: .NET 8.0
- **Architecture**: SOLID 원칙과 Clean Architecture 적용.
- **DI**: Microsoft.Extensions.DependencyInjection을 기본으로 사용.
- **Test**: xUnit + Moq 스택 고정.

## Working Process (학습 루프)
개발자가 따라갈 기본 사이클. Claude는 각 단계의 **가이드**를 제공하고, **개발자가 직접 수행**한다.

1. **Learning Plan (Claude)**: Claude가 기능 요구사항을 학습 스텝으로 분해하고 각 스텝별 목표·참고 개념·완료 기준을 제시.
2. **Planning (개발자)**: 개발자가 `implementation_plan.md`를 직접 작성. Claude는 작성된 플랜을 검토만 한다.
3. **Implementation (개발자)**: 인터페이스 정의 → 구현체 작성 → DI 등록. Claude는 개념 설명과 리뷰만.
4. **Verification (개발자)**: xUnit + Moq로 테스트 작성 및 실행. Claude는 테스트 설계 가이드와 리뷰만.
5. **Commit (개발자)**: 모든 테스트 통과 후 커밋.

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

## Agents (학습 가이드 보조)
서브 에이전트는 `agents/` 폴더에 정의되어 있다.
**중요**: 이 에이전트들은 자동 실행으로 코드를 생성하지 않는다. 개발자가 학습 중 **개념·패턴 질문**을 할 때 참고 자료로만 활용한다.

| 에이전트          | 파일                              | 용도 (학습 모드)                    |
|-------------------|-----------------------------------|-------------------------------------|
| Architect Agent   | `agents/architect-agent.md`       | 설계 개념 설명, 설계 리뷰          |
| DI Auditor        | `agents/di-auditor.md`            | DI 등록 패턴 설명, 기존 등록 검토 |
| Unit Tester       | `agents/unit-tester.md`           | 테스트 설계 가이드, 기존 테스트 리뷰 |

### 학습 루프
```
새 기능 요청
    → Claude: 학습 스텝 분해 + 각 스텝 가이드 제공
    → 개발자: implementation_plan.md 작성
    → Claude: 플랜 리뷰
    → 개발자: 인터페이스/구현체 작성
    → Claude: 코드 리뷰 (네이밍, SOLID, DI 등록 지적)
    → 개발자: xUnit + Moq 테스트 작성
    → Claude: 테스트 커버리지·설계 리뷰
    → 개발자: 커밋
```
