# Agents

이 폴더는 CSharpPlayground 프로젝트의 서브 에이전트 정의를 담는다.
각 에이전트는 CLAUDE.md의 워크플로우(Plan → Implementation → Verification) 단계를 분담한다.

## 에이전트 구조

```
User Request
     │
     ▼
┌─────────────────┐
│  Architect Agent │  ← Planning 단계
│  (설계 전문)     │    implementation_plan.md 작성
└────────┬────────┘
         │ 설계 완료
         ├─────────────────────────┐
         ▼                         ▼
┌─────────────────┐     ┌─────────────────┐
│   DI Auditor    │     │   Unit Tester   │
│  (DI 검증 전문) │     │  (테스트 전담)  │
│  Implementation │     │  Verification   │
│  단계 보조      │     │  단계 전담      │
└─────────────────┘     └─────────────────┘
```

## 에이전트 목록

| 파일                  | 역할                  | CLAUDE.md 단계   | 관련 커맨드  |
|-----------------------|-----------------------|------------------|--------------|
| [architect-agent.md](architect-agent.md) | 아키텍처 설계 및 구현 계획 수립 | Planning         | `/plan`      |
| [di-auditor.md](di-auditor.md)       | DI 등록 검증 및 충돌 감지    | Implementation   | `/di-check`  |
| [unit-tester.md](unit-tester.md)      | xUnit + Moq 단위 테스트 작성 | Verification     | `/test-all`  |

## 관련 Skill Commands

| 커맨드         | 파일                                   | 설명                              |
|----------------|----------------------------------------|-----------------------------------|
| `/di-refactor` | `.claude/commands/di-refactor.md`      | 클래스를 인터페이스 기반으로 분리  |
| `/mock-gen`    | `.claude/commands/mock-gen.md`         | xUnit + Moq 테스트 템플릿 생성     |

## 협업 시나리오 예시

**새 기능 추가 요청 시:**
```
1. Architect Agent → implementation_plan.md 작성
2. 사용자가 코드 구현
3. /di-refactor 로 인터페이스 분리
4. DI Auditor → DI 등록 검증
5. /mock-gen 으로 테스트 스텁 생성
6. Unit Tester → 테스트 구현 및 실행
```
