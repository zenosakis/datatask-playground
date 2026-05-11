# Implementation Plan: Feature.Transfer (HTTP / FTP / SFTP)

> 작성일: 2026-04-29
> 목적: 다양한 프로토콜의 데이터/파일 전송 클라이언트 구현 학습
> 작성 모드: **1. 목표 기능** ~ **6. 학습 우선순위 권장** 의 학습 가이드라인은 Claude 작성. **7. 개발자가 직접 작성할 영역** ~ **8. 진행 로그** 의 구현 Phase·인터페이스 시그니처는 개발자 직접 작성.

---

## 0. 이전 plan 상태

직전 plan(Pure DI 리팩터링)은 **완료**. 잔여 항목 정리 (2026-04-30 기준):
- ~~`/mock-gen DapperTest` — 미완 (Test.Dapper 프로젝트 자체 없음)~~ → **완료**: `Feature.Dapper.Tests` 프로젝트 + `DapperClientTests` (커밋 `665ee74`, `8517e8c`)
- ~~SEC-001 (SQL Injection 화이트리스트) — 의도적 보류~~ → **완료**: `_allowedTables` HashSet + 거부 시 `ArgumentException` (커밋 `8517e8c`)

이전 plan 본문은 git history(`a9c9f7a` 이후)에서 참조. 본 문서는 새 기능에 집중.

---

## 1. 목표 기능

| 프로토콜 | 보안 변종 | 권장 라이브러리 | 인증 모델 |
|---------|----------|----------------|-----------|
| HTTP    | HTTPS    | BCL `HttpClient` | Header / Bearer / Basic |
| FTP     | FTPS (Explicit / Implicit) | FluentFTP (사실상 표준) | user/pass + TLS |
| SFTP    | (SSH 기반)| SSH.NET (Renci.SshNet) | user/pass 또는 키 페어 |

**현재 진척**:
- `Feature.Transfer/HttpTransferClient` — `GetStreamAsync` / `GetStringAsync` 구현 완료. `SendAsync<TRequest, TResponse>` 1차 구현 및 핵심 테스트 완료.
- 학습 일지 [Q1]~[Q20] 누적 (`tests/Feature.Transfer.Tests/HttpTransferClientTests.cs` 상단).
- FTP / SFTP — 미착수.

---

## 2. 실무 관점 사전 토픽 (구현 전 결정해야 할 것)

### 2.1 통합 추상화 vs 프로토콜별 추상화 — **디자인 결정 필요**

현재 두 인터페이스가 공존:
- `IDataTransferClient` — HTTP 성격 (Send / GetStream / GetString)
- `IFileTransferClient` — 파일 성격 (Connect / Upload / Download / List ...)

**의문점**:
- HTTP 도 파일 다운로드(`GetStreamAsync`)가 가능한데, 추상화가 겹치는가?
- FTP/SFTP 의 connection lifecycle(Connect/Disconnect)은 stateless 한 HTTP 와 본질적으로 다른데 같은 인터페이스로 묶을 수 있나?
- 호출자 입장에서 "전송 한다" 의 추상화 단위는 무엇인가? — 파일? 바이트? 요청/응답?

**선택지**:
1. 현 구조 유지 (HTTP 와 FTP/SFTP 를 다른 추상화 트리로 분리)
2. 공통 상위 추상화 도입 (`ITransferClient` → 하위에 `IDataTransferClient`/`IFileTransferClient`)
3. 완전 통합 (모두 `IFileTransferClient` 같은 단일 인터페이스로) — **leaky abstraction 위험 큼**

**권장**: 1번 (현 구조 유지). HTTP 의 stateless 성격과 FTP/SFTP 의 connection-stateful 성격은 추상화로 가려선 안 됨. 호출자가 lifecycle 책임을 알고 있어야 안전.

→ 최종 결정은 **7. 개발자가 직접 작성할 영역 → 7.1 디자인 결정 기록** 에서 개발자가 명시적으로 기록.

### 2.2 HttpClient 수명 — `IHttpClientFactory`

- 매번 `new HttpClient()` → **socket exhaustion** (TIME_WAIT 포트 누적, Microsoft 공식 안티패턴).
- 정적 single instance → DNS 변경 미반영.
- 해결: `IHttpClientFactory` + `AddHttpClient<T>()` — handler 풀링 + DNS 만료 자동 갱신.
- **학습 순서**: 기본 `HttpClient` 사용법(현재 진행 중)을 먼저 익힌 뒤 `IHttpClientFactory` 패턴으로 **마이그레이션** — **3. 프로토콜별 학습 가이드 → A. HTTP/HTTPS → A-2. `IHttpClientFactory` 마이그레이션** 에서 단계별 가이드.
- `ServiceCollection` 도입은 자유롭게 가능. (Pure DI 는 `Test.RunConsole` 학습 예시일 뿐 프로젝트 전반 원칙이 아님)

### 2.3 재시도 / 회복력 — Polly

네트워크 작업은 일시 실패가 *정상*. 운영 환경에선 다음 셋이 필수:
- **재시도 with 지수 백오프** — transient failure 흡수
- **서킷 브레이커** — 연속 실패 시 빠르게 차단해 다운스트림 보호
- **타임아웃** — 응답 없는 호출 정리

학습 우선순위: 후순위. 기능 동작 검증 후 도입.

### 2.4 라이브러리 선택 근거

| 후보 | 평가 |
|------|------|
| FTP: FluentFTP | 활발한 메인테너, async-first, FTPS 완성도 높음 → **권장** |
| FTP: edtFTPnet | OK 하지만 FluentFTP 가 우위 |
| FTP: `FtpWebRequest` (BCL) | **공식적으로 deprecated** — 쓰지 말 것 |
| SFTP: SSH.NET | 사실상 유일한 선택지. 대안 없음 → **권장** |

### 2.5 인증 모델은 프로토콜별로 다르다

| 프로토콜 | 인증 방식 | Options 에 들어갈 항목 |
|---------|----------|---------------------|
| HTTP    | Header (Bearer / Basic / API Key) | BaseAddress, Timeout, DefaultHeaders |
| FTP/FTPS| user/pass + (TLS 인증서) | Host, Port, User, Password, UseFtps, ValidateCertificate |
| SFTP    | user/(pass or 키 파일) + 호스트 키 검증 | Host, Port, User, Password OR PrivateKeyPath, PrivateKeyPassphrase, HostKeyFingerprint |

→ **하나의 통합 Options 로 묶지 말 것**. 프로토콜별 Options 가 자연스럽다.

### 2.6 스트리밍 vs 메모리 적재

큰 파일을 `byte[]` 로 한 번에 읽으면 OOM. 인터페이스가 `Stream` 을 받고/돌려주면 메모리 안전.
현재 `IFileTransferClient` 가 `localPath` (string) 기반 — 라이브러리가 내부에서 streaming 하므로 OK. 단, **메모리 변형이 필요한 케이스(예: 업로드 전 변환)** 가 생기면 Stream 오버로드 추가 검토.

---

## 3. 프로토콜별 학습 가이드

> 각 학습 스텝은 **개념 이해 + 완료 기준** 을 제시합니다. 변경할 파일 이름·실제 코드 작성·Phase 분해 표는 **7. 개발자가 직접 작성할 영역** 에서 개발자가 직접 정합니다.

---

### 0. DapperTest 단위 테스트 (우선순위 #1 — 이전 plan 잔여) ✅ **완료 (2026-04-30)**

> 새 기능이 아니라 **이미 학습한 Mock 패턴이 다른 도메인에서도 통하는지** 검증하는 작업. HttpClient 에서 배운 [Q1]~[Q16] 의 일반화 점검.
>
> **검토 결과 요약**: Step D-1~D-5 모두 처리됨. 학습 일지 [Q1]~[Q6] 누적 ([DapperClientTests.cs](tests/Feature.Dapper.Tests/DapperClientTests.cs) 상단 주석). 우회 전략은 선택지 B (자체 어댑터) 채택 — **7. 개발자가 직접 작성할 영역 → 7.1 디자인 결정 기록 → 결정 4** 참조.

#### Step D-1: Dapper 의 정체와 테스트 함정 사전 학습 ✅

**핵심 개념**:
- Dapper 는 BCL `IDbConnection` 위에 얹은 *micro-ORM*. `IDbConnection` 자체는 안 건드림.
- 핵심 API(`Query<T>`, `Execute`, `QueryFirstOrDefault<T>`)는 **`IDbConnection` 의 *static 확장 메서드***. 클래스 멤버가 아님.
- → [Q2] 의 변종 함정: **static 확장 메서드는 Moq 로 가로챌 수 없음** (virtual override 가 본질이라 static 은 대상 외).

**완료 기준**: "왜 `_dbConnection.Query(...)` 를 직접 Mock 할 수 없는가" 를 자기 말로 한 단락 설명 가능.

**검토 결과**: [Q1]/[Q2] 학습 일지로 정착됨. Moq 가능 조건의 일반화 규칙 도출 — *"override 가능한가?"* (interface/abstract/virtual = 가능, static/확장/non-virtual/sealed = 불가).

#### Step D-2: 우회 전략 — 디자인 결정 필요 ✅ **선택지 B 채택**

Dapper 호출을 테스트 가능하게 만드는 길은 **3 갈래**. 이 중 어느 길로 갈지가 D-2 의 결정 사항.

**선택지 A — `IDbConnection` 의 멤버를 직접 Mock**
- Dapper 가 내부에서 `CreateCommand`, `ExecuteReader` 등 BCL 멤버를 호출 → 이걸 Mock.
- 단점: Mock 셋업이 **수십 줄**. SQL 파라미터/리더 시뮬레이션 지옥. 비추천.

**선택지 B — 자체 좁은 어댑터 (`IDapperAdapter` 또는 `IQueryRunner`)**
- `IDapperAdapter.Query<T>(sql)` 같은 좁은 인터페이스 정의 → DapperTest 가 어댑터에 위임.
- 어댑터 구현체는 내부에서 진짜 Dapper 호출.
- 테스트는 어댑터 Mock 으로 진행 — Mock 셋업 깔끔.
- 단점: 추상화 1단계 추가. Dapper 의 풍부한 API 를 다시 노출하는 wrapping 부담.
- **장점**: HttpTransferClient 에서 학습한 *좁은 인터페이스 + Mock* 패턴의 직접 재현.

**선택지 C — SQLite in-memory 통합 테스트**
- `Microsoft.Data.Sqlite` 의 `:memory:` 모드로 실제 DB 띄움.
- 진짜 SQL 실행 → 진짜 결과 검증.
- 단점: SQL Server 와 SQLite 의 SQL 문법 차이 (`SELECT TOP` vs `LIMIT`).
- **장점**: 단위 테스트 vs 통합 테스트의 경계 학습. FTP/SFTP 의 Testcontainers 패턴 예고편.

**완료 기준**: 3 선택지 각각의 트레이드오프를 정리하고, **본인의 결정과 근거**를 **7. 개발자가 직접 작성할 영역 → 7.1 디자인 결정 기록** 에 기록.

**검토 결과**: 선택지 B (좁은 어댑터 `IDapperAdapter`) 채택. 결정 근거와 결과 영향은 **7.1 결정 4** 에 정착. 부수 학습 일지 — [Q4] (인터페이스/어댑터/클라이언트 3 계층 구조), [Q5] (Setup vs SetupGet), [Q6] (.CallBase() 적용 가능 케이스).

#### Step D-3: 결정에 따른 테스트 작성 ✅

선택한 우회 전략으로 다음 케이스를 단위 테스트:
- `SelectTest("TBL_DIALEDLOG")` — 정상 케이스, Query 가 1번 호출되는지 (Verify)
- 테이블명에 따라 SQL 이 어떻게 들어가는지 (선택지 B 면 인터페이스 호출 인자, C 면 실제 결과)
- 빈 결과 / 다중 결과 케이스

**완료 기준**: 최소 3개 테스트 통과. 학습 일지 [Q1]~[Q?] 누적.

**검토 결과**: 테스트 2종 작성 — `SelectTop10_WithAllowedTable_CallsQuery` (허용 케이스, Query 가 SQL 에 테이블명 포함하여 1회 호출), `SelectTop10_WithDisallowedTable__ThrowsAndDoesNotCallAdapter` (거부 케이스, `ArgumentException` 던지고 어댑터는 미호출). *"최소 3개" 기준은 미달이지만* 거부 케이스에서 단락 검증(`Times.Never`) 으로 보강 — 빈 결과/다중 결과 케이스는 어댑터가 thin wrapper 이므로 단위 테스트에서 검증 가치 낮음 ([Q3] 에서 도출). 향후 통합 테스트 도입 시 SQLite 로 검증 가능.

#### Step D-4: SEC-001 (SQL Injection 화이트리스트) 보류 해제 결정 ✅ **처리**

이전 plan 에서 `[-]` 마킹된 항목. 지금 처리할지 후순위로 둘지 결정.
- 처리 시: 테이블명 화이트리스트 검증 + 검증 실패 케이스 테스트 추가.
- 보류 시: 이유 명시 (예: "임시 학습 코드, 운영 전환 시 처리").

**완료 기준**: 결정 기록 + (처리 시) 테스트 1개 추가.

**검토 결과**: 처리 결정. 결정 근거와 부수 효과는 **7.1 결정 5** 에 정착. 부수 효과 — [Q3] (thin wrapper 의 단위 테스트 가치 0 문제) 가 화이트리스트 추가로 *결정/검증 로직* 이 생기며 자연 해결됨. 보안 처리와 학습 명제 정렬이 동시에 달성.

#### Step D-5: 함정 체크리스트 ✅
- [x] ~~Dapper 가 `IDbConnection` 의 어떤 멤버에 의존하는지 학습~~ → 선택지 B 채택으로 불필요 (어댑터로 격리됨)
- [x] **어댑터 인터페이스가 너무 넓어지지 않는가** — `IDapperAdapter.Query(string sql)` 1개 메서드만 노출 (Interface Segregation 준수)
- [ ] ~~SQLite 와 SQL Server 의 SQL 차이~~ → 선택지 C 미채택으로 해당 없음 (향후 통합 테스트 도입 시 재검토)
- [ ] **Connection 라이프사이클** — *미해결*. 현재 `DapperAdapter` 가 `IDbConnection` 보유하지만 Open/Close/Dispose 책임 명시 없음. 운영 전환 시 검토 필요.

---

### A. HTTP / HTTPS — 진행 중

**기존 자산**: [Q1]~[Q16] 학습 일지에 16개 함정 누적. 새 메서드 추가 시 5분 안에 재활용 가능.

#### A-1. `SendAsync<TRequest, TResponse>` (우선순위 #2)

> 단순 GET 두 종(GetStream/GetString)과 달리 **직렬화·역직렬화·Method 분기·요청 본문 검증** 이 한꺼번에 들어옴. HTTP 학습의 마지막 큰 산.

##### Step S-1: 시그니처 의미 분석 (코드 작성 전)

제네릭 두 개의 책임을 자기 말로 정리:
- `TRequest` — 직렬화 대상. 어떤 타입까지 허용하나?
- `TResponse` — 역직렬화 대상. `string` 이면 GetStringAsync 와 의미 겹침. `Stream` 이면? 막을지 허용할지.
- `body` 가 `null` 이면? Method 가 `GET` 인데 body 가 있으면?

**완료 기준**: "이 메서드의 의도와 한계" 한 단락. 미해결 질문은 [Q17]+ 로 일지 누적.

##### Step S-2: 구현 설계 — JSON 직렬화 라이브러리 선택

선택지:
- **`System.Text.Json`** (BCL) — 빠름, 의존성 0, 권장
- **`Newtonsoft.Json`** — 기능 풍부, 레거시 호환

BCL 헬퍼:
- **`HttpClient.PostAsJsonAsync` + `ReadFromJsonAsync<T>`** (`System.Net.Http.Json` 패키지) — 한 줄로 끝남
- **수동 구현** — `StringContent("...", Encoding.UTF8, "application/json")` + `JsonSerializer.Deserialize<T>(...)`

**학습 권장**: 첫 구현은 *수동* 으로 (직렬화 흐름 이해). 두 번째 메서드부터 헬퍼 사용 검토.

**완료 기준**: 선택과 근거 기록 (학습 일지에).

##### Step S-3: Happy path 테스트 — 200 + JSON 본문

테스트 명제: "응답 본문 JSON 을 `TResponse` POCO 로 역직렬화한다".
- `HttpResponseMessage` 의 `Content` 에 `StringContent("{...json...}")` 셋업
- `_handlerMock.Protected().Setup<...>("SendAsync", ...).ReturnsAsync(...)` 패턴 그대로 재사용
- 결과 POCO 의 프로퍼티 비교 (`Assert.Equal(expected.Name, result.Name)`)

**완료 기준**: 테스트 1개 통과. [Q1]~[Q16] 의 Setup/Verify 패턴이 그대로 재사용된다는 점 확인.

##### Step S-4: 요청 본문 검증 — 비동기 람다 함정

새 학습: 요청 본문이 정말 JSON 으로 직렬화돼서 나갔는지 검증.
- `_handlerMock.Verify(... ItExpr.Is<HttpRequestMessage>(r => ...))` 람다 안에서 `r.Content!.ReadAsStringAsync()` 를 *동기* 로 호출해야 함.
- 람다는 `bool` 즉시 반환 → `await` 못 씀 → `.Result` 사용.
- 또는 람다 밖에서 미리 읽어 캡처하는 패턴.

**함정 예고** ([Q17] 후보):
- `.Result` 는 데드락 위험 — 단, 테스트 컨텍스트(SyncContext 없음)에선 안전. *프로덕션 코드에선 금지*, 테스트는 OK.

**완료 기준**: "요청 본문이 `{"name":"X"}` 형태로 나가는가" 검증 테스트 1개.

##### Step S-5: HttpMethod Theory 함정

[Q12] 의 함정 ① 변종: `HttpMethod` 인스턴스는 `[InlineData]` 에 못 넣음 (컴파일 상수 아님).

해결:
- `[InlineData("POST")]`, `[InlineData("PUT")]`, `[InlineData("DELETE")]` 로 string 받음
- 본문에서 `HttpMethod.Parse(method)` 또는 `new HttpMethod(method)` 변환

**완료 기준**: Method 별 동일 명제(요청이 해당 Method 로 나간다) Theory 1개.

##### Step S-6: 비-2xx 처리 + EnsureSuccessStatusCode 위치

`SendAsync` 의 raw `HttpResponseMessage` 는 자동으로 status 검사 안 함. `GetStringAsync` 와 다름.

설계 결정 필요:
- A) `EnsureSuccessStatusCode()` 호출 후 본문 처리 (예외 일관성)
- B) `IsSuccessStatusCode` 분기 후 자체 예외/Result 타입 반환 (호출자에게 정보 더 줌)
- C) 호출자 책임으로 raw 반환 (`HttpResponseMessage` 노출) — **2. 실무 관점 사전 토픽 → 2.5 인증 모델은 프로토콜별로 다르다 의 leaky abstraction. 비추천.** *(참조 주의: leaky abstraction 개념은 실제로는 2.1 통합 추상화 vs 프로토콜별 추상화 의 선택지 3 에서 등장 — 원작자 검토 필요)*

[Q12] 의 비-2xx Theory 패턴 그대로 재사용 가능.

**완료 기준**: 선택과 근거 기록 + 비-2xx Theory 1개 통과.

##### 2026-05-06 학습 정리 — `SendAsync<TRequest, TResponse>` 책임 범위

오늘 학습에서 `SendAsync<TRequest, TResponse>` 는 **JSON 기반 API 호출용 고수준 메서드**로 범위를 확정.

**메서드 역할 분리**:
- `GetStreamAsync` — 단순 GET 요청의 응답 본문을 raw stream 으로 받음. 파일 다운로드나 큰 payload 처럼 클라이언트가 내용을 해석하지 않는 경우.
- `GetStringAsync` — 단순 GET 요청의 응답 본문을 raw string 으로 받음. JSON/HTML/CSV/plain text 등 해석 책임은 호출자에게 있음.
- `SendAsync<TRequest, TResponse>` — JSON request body 를 보내고 JSON response body 를 `TResponse` 로 역직렬화함. raw string/stream 응답과는 다른 책임.

**제네릭 의미 정리**:
- `TRequest` 는 HTTP 헤더나 메타데이터가 아니라 **요청 body DTO**. DTO 타입이 다양해도 제네릭 자체가 다양성을 처리하므로 공통 인터페이스/베이스 타입은 필수 아님.
- `TResponse` 는 JSON 응답을 담을 **응답 DTO**. 응답 JSON 을 raw string 으로 호출자에게 넘기는 방식은 별도 메서드 책임(`SendRawAsync` 같은 확장 후보)으로 분리하는 것이 자연스러움.
- 런타임에 필드 구성이 결정되는 동적 payload 는 DTO 클래스 대신 `Dictionary<string, object?>` 또는 `JsonObject` 같은 타입을 `TRequest` 로 사용할 수 있음.

**HTTP 구성 요소 매핑**:
- `HttpMethod method` — HTTP method.
- `string resource` — `HttpClient.BaseAddress` 기준 상대 resource path. 선행 `/` 가 붙으면 BaseAddress 의 path 일부를 무시할 수 있으므로 주의.
- `TRequest body` — JSON 으로 직렬화할 request body.
- Header 는 현재 메서드 인자가 아니며, 공통 헤더는 `HttpClient.DefaultRequestHeaders`, 요청별 헤더는 향후 `HttpRequestMessage.Headers` 또는 별도 request wrapper 도입 시 검토.

**구현 흐름 학습 메모**:
- 요청 방향: `TRequest 객체 → JSON 문자열 → StringContent → HttpRequestMessage.Content → HttpClient.SendAsync`.
- 응답 방향: `HttpResponseMessage → Content.ReadAsStringAsync → JSON 문자열 → JsonSerializer.Deserialize<TResponse>`.
- `HttpClient.SendAsync` 의 반환값은 JSON 문자열이 아니라 `HttpResponseMessage`. 응답 body 를 역직렬화하려면 반드시 `response.Content` 를 먼저 읽어야 함.
- `HttpClient.PostAsync` 의 body 인자는 DTO 가 아니라 `HttpContent`. 수동 구현에서는 DTO 를 `StringContent` 로 감싼 뒤 전송해야 함.

**현재 정책 결정**:
- JSON 옵션은 사용하지 않음. 대소문자/프로퍼티명 매핑을 느슨하게 하지 않고, 학습 단계에서는 엄격한 기본 동작을 관찰.
- `SendAsync` 에서 `GET` 은 지원하지 않음. 단순 GET 은 `GetStringAsync` 또는 `GetStreamAsync` 로 분리.
- 예외 정책은 아직 자체 wrapping 하지 않음. `Serialize`, `SendAsync`, `EnsureSuccessStatusCode`, `Deserialize` 각 단계의 원래 예외를 먼저 관찰하고, 필요 시 이후 `TransferException` 같은 자체 예외 정책을 결정.

**테스트 작성 결과 (2026-05-07)**:
- `SendAsync_WhenRequestGet_ThrowsNotSupportedException` — `GET` 은 JSON body API 호출 책임에서 제외. `HttpMessageHandler.SendAsync` 가 호출되지 않음을 `Times.Never()` 로 검증.
- `SendAsync_WhenRequest_RequestContentJsonSerialized` — 테스트 전용 `TestRequest` DTO 가 JSON request body 로 직렬화되는지 검증. `requestMessage` 가 메서드 종료 후 dispose 되므로, `Verify` 시점이 아니라 `Callback` 시점에 body 문자열을 캡처.
- `SendAsync_WhenResponseContentIsJson_ReturnsDeserializedResponse` — 응답 JSON 을 문자열 그대로 비교하지 않고, `TResponse` DTO 로 역직렬화된 결과 프로퍼티를 검증.
- `SendAsync_WhenResponseIsNotSuccess_ThrowsHttpRequestException` — `EnsureSuccessStatusCode()` 정책을 고정. 404/500/401 에서 `HttpRequestException` 이 발생하고 `StatusCode` 가 보존되는지 Theory 로 검증.

**추가 학습 일지**:
- [Q17] `TRequest` / `TResponse` 는 Mock 대상이 아니라 JSON shape 를 나타내는 테스트 DTO.
- [Q18] guard clause 테스트에서는 의존성 Setup 보다 `Times.Never()` 가 더 정확한 단언.
- [Q19] `HttpRequestMessage` / `StringContent` 는 dispose 될 수 있으므로 요청 body 검증은 `Callback` 에서 즉시 캡처.
- [Q20] JSON 은 문자열 표현보다 구조/DTO 상태를 기준으로 비교.

**다음 단계**:
- A-1 핵심 테스트는 완료. 선택 보강 후보는 Content-Type 검증, POST/PUT/DELETE method 전달 Theory, CancellationToken 전달 검증.
- 기본 흐름은 A-2 `IHttpClientFactory` 마이그레이션으로 이동.

#### A-2. `IHttpClientFactory` 마이그레이션 (우선순위 #3 — A-1 완료 직후)

> A-1 (기본 SendAsync) 까지 완료해 *직접 `new HttpClient()`* 패턴을 한 번 끝까지 경험한 뒤, 같은 클라이언트를 `IHttpClientFactory` 기반으로 **마이그레이션** 하면서 차이를 체감하는 학습 단계.

##### Step F-1: 회고 — 왜 `IHttpClientFactory` 가 필요한가

A-1 까지의 코드를 다시 보고 다음 질문에 답해보기:
- 호출자가 `new HttpTransferClient(new HttpClient(), ...)` 를 매 요청마다 하면 어떤 문제가 생기는가?
- 정적 single instance 로 만들면 무엇이 해결되고 무엇이 새로 생기는가?
- `Test.RunConsole` 같은 짧은 수명 콘솔에선 왜 별 문제 없는가? — 어디서부터 문제가 되는가?

**완료 기준**: socket exhaustion / DNS staleness 두 함정을 자기 말로 한 단락씩 설명.

##### Step F-2: `ServiceCollection` 기본 학습

`Microsoft.Extensions.DependencyInjection` NuGet 추가 후:
- `ServiceCollection` 의 기본 사용법 (`AddTransient` / `AddScoped` / `AddSingleton` 차이)
- `BuildServiceProvider()` → `GetRequiredService<T>()` 흐름
- 라이프타임 함정 — Singleton 안에 Scoped 주입하면 *captive dependency*

학습 공간으로는 기존 `Test.RunConsoleWithBuilder` 프로젝트 활용 검토 (이미 존재).

**완료 기준**: ServiceCollection 으로 `EncryptionOptions` 같은 단순 객체 등록 → 다른 클래스에 주입 → 동작 확인. 또한 라이프타임 3종의 차이를 자기 말로 설명 가능.

##### Step F-3: `AddHttpClient` 패턴 — Named vs Typed

`Microsoft.Extensions.Http` NuGet 추가 후 두 패턴 학습:

**Named client**:
```
services.AddHttpClient("transfer", c => c.BaseAddress = new Uri("..."));
// 사용: factory.CreateClient("transfer")
```
- 장점: 한 곳에 여러 외부 서비스 등록 시 자연스러움
- 단점: 문자열 키 — 컴파일 타임 안전성 부족

**Typed client**:
```
services.AddHttpClient<HttpTransferClient>(c => c.BaseAddress = new Uri("..."));
// 사용: HttpTransferClient 를 ctor 에 직접 주입
```
- 장점: DI 1급 시민. ctor 가 `HttpClient` 받으면 자동 주입.
- 단점: 1 클라이언트 = 1 클래스 묶임

**선택 결정**: 현재 `HttpTransferClient` 의 ctor 시그니처(`(HttpClient, HttpTransferOptions)`) 와 가장 자연스럽게 맞는 쪽은 **Typed**. Named 는 학습 차원에서 한 번 시도해보고 Typed 로 전환 권장.

**완료 기준**: Typed client 로 `HttpTransferClient` 등록 + 콘솔에서 한 번 호출 성공.

##### Step F-4: `HttpTransferOptions` 와의 충돌 정리

함정 예고 — `HttpTransferClient` ctor 안에서 이미 `_httpClient.BaseAddress = options.BaseAddress` 를 하고 있는데, `AddHttpClient` 람다에서도 동일하게 설정하면 **두 군데**에서 같은 일을 함. 누가 이기는가? (마지막 set 이 이김 — ctor 가 나중에 호출되므로 ctor 의 값이 최종)

디자인 결정 필요:
- **A. 등록 시점으로 일원화** — `AddHttpClient` 람다에서 `BaseAddress`/`Timeout` 설정. ctor 에선 안 건드림. `HttpTransferOptions` 는 *등록 람다 안에서만* 참조.
- **B. ctor 에서만** — `AddHttpClient` 람다는 비워두고 ctor 가 모든 설정. (현재 코드 그대로 — 변경 최소)
- **C. 분담** — 정적 설정(BaseAddress)은 등록 시점, 동적 설정(요청별 헤더)은 ctor/메서드.

**권장**: A. 이유 — `IHttpClientFactory` 의 표준 사용 패턴. ctor 에서 클라이언트 상태를 변형하는 건 두 번째 사용자가 보기 헷갈림.

**부수 결정**: `HttpTransferOptions` 를 그대로 둘지, `IOptions<HttpTransferOptions>` 패턴(`IOptionsMonitor`/`IOptionsSnapshot`)으로 승격할지. 학습 가치 있는 분기점.

**완료 기준**: 결정 + 적용. 동일 동작 유지.

##### 2026-05-11 학습 정리 — `HttpClient` 구성 책임을 composition root 로 이동

`IHttpClientFactory` 학습을 시작하면서 `HttpTransferClient` 의 생성자 책임을 정리했다.

**변경된 책임 분리**:
- `HttpTransferClient` — 이미 설정된 `HttpClient` 를 사용해 `GetStreamAsync`, `GetStringAsync`, `SendAsync<TRequest, TResponse>` 를 수행한다.
- `Sandbox.HostBuilder` — `AddHttpClient<HttpTransferClient>((sp, client) => ...)` 등록 람다에서 `HttpTransferOptions` 를 읽고 `BaseAddress` / `Timeout` 을 설정한다.
- `Sandbox.PureDi` — 같은 조립을 코드로 직접 수행한다. `HttpTransferOptions` 생성 → `HttpClient` 구성 → `new HttpTransferClient(httpClient)` 순서.
- 테스트 — `CreateSut(...)` 에서 `HttpClient` 를 설정한 뒤 SUT 에 주입한다. 운영은 factory 를 쓰지만 단위 테스트는 기존 `HttpMessageHandler` mock 패턴을 유지한다.

**학습 포인트**:
- `AddHttpClient` 는 `Microsoft.Extensions.Http` 패키지가 필요하다.
- `Configure<HttpTransferOptions>` 는 `IOptions<HttpTransferOptions>` 를 등록한다. typed client 람다에서는 `sp.GetRequiredService<IOptions<HttpTransferOptions>>()` 로 옵션을 꺼낼 수 있다.
- `HttpClient` 설정 책임을 생성자와 등록부 양쪽에 두면 중복이다. `IHttpClientFactory` 패턴에서는 등록부가 설정 책임을 갖고, client 클래스는 사용 책임만 갖는 편이 읽기 쉽다.
- Pure DI 와 HostBuilder DI 의 차이는 "누가 조립을 대신하느냐" 이다. Pure DI 는 `Program.cs` 가 직접 조립하고, HostBuilder 는 `ServiceCollection` 등록 규칙에 따라 컨테이너가 조립한다.

**추가 학습 일지**:
- [Q21] `AddHttpClient` 가 보이지 않으면 `Microsoft.Extensions.Http` 패키지 참조를 확인한다.
- [Q22] Typed client 생성자에는 보통 `HttpClient` 만 받고, `BaseAddress` / `Timeout` 설정은 등록 람다에서 처리한다.
- [Q23] Pure DI 에서는 factory 가 없으므로 설정 완료된 `HttpClient` 를 직접 만들어 주입한다.

##### Step F-5: 테스트 마이그레이션 — [Q1]~[Q16] 자산 보존

핵심 질문: `IHttpClientFactory` 를 도입하면 기존 `HttpMessageHandler` Mock 패턴이 깨지는가? **답: 깨지지 않음.** 두 가지 경로:

**경로 A — typed client 단위 테스트 (권장, 가장 단순)**
- 테스트 안에서는 `IHttpClientFactory` 안 거치고 *직접* `new HttpClient(handlerMock.Object)` 로 SUT 주입.
- 즉 [Q1]~[Q16] 의 `_handlerMock`, `_client`, `CreateSut(...)` 패턴 **완전 그대로 유지**.
- 운영은 `IHttpClientFactory` 로 가지만, 테스트는 그 레이어를 우회.

**경로 B — `ServiceProvider` 통째로 빌드 (통합 테스트성)**
- `services.AddHttpClient<HttpTransferClient>(...).ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object);`
- DI 그래프 전체가 진짜로 동작 — 마이그레이션 후 회귀 검증에 유용.
- 단점: 셋업 분량 증가, 테스트 의도가 흐려짐.

**권장**: 단위 테스트는 A 그대로, 마이그레이션 검증용 통합 테스트 1~2개만 B.

**완료 기준**: 기존 [Q1]~[Q16] 테스트가 마이그레이션 후 *수정 없이* 그대로 통과. (수정해야 한다면 마이그레이션 설계가 깨진 신호 — 점검 필요)

##### Step F-6: Handler 만료/풀링 동작 직접 확인 (선택 학습)

- `SetHandlerLifetime(TimeSpan)` — 기본 2분, 명시 가능
- 진짜로 풀링이 동작하는지 디버그 출력으로 확인 — 같은 BaseAddress 로 여러 번 호출 → 핸들러 instance 가 재사용되는지

이 단계는 *학습 만족도* 가 목적. 동작 확인 후 다음 단계로.

##### Step F-7: `DelegatingHandler` (선택 학습 — 실무 패턴)

- 모든 요청에 끼어드는 미들웨어. 예: 로깅, 인증 토큰 자동 부착, 재시도 카운트.
- `DelegatingHandler` 상속 → `SendAsync` 오버라이드 → `services.AddHttpClient<...>().AddHttpMessageHandler<MyHandler>()`.
- Polly 도 결국 이 패턴 위에 얹힌 것 — 다음 학습(Polly)의 사전 지식.

**완료 기준**: 로깅 핸들러 1개 작성 + 등록 + 요청 시 Serilog 출력 확인.

##### Step F-8: 함정 체크리스트
- [ ] Typed client 로 등록한 클래스를 `new` 로 또 만들지 않기 (DI 우회 = 등록 효과 무화)
- [ ] `IHttpClientFactory.CreateClient()` 결과를 *오래 보유*하지 않기 — handler 만료가 의미 없어짐
- [ ] Singleton 안에 typed client(transient) 주입 시 *captive dependency*
- [ ] 람다 안에서 `services.BuildServiceProvider()` 호출 금지 — provider 가 양산됨

#### A-3. Polly 회복력 (우선순위 #6 — 후순위)

**2. 실무 관점 사전 토픽 → 2.3 재시도 / 회복력 — Polly** 참고. `IHttpClientFactory` + `DelegatingHandler` 학습이 끝난 다음 자연스럽게 진입.

---

### B. FTP / FTPS

#### Step B-1: 프로토콜 이해 (코드 작성 전)

**핵심 개념**:
- **Control 채널 / Data 채널 분리** — FTP 의 가장 큰 특이점. Control(21) 로 명령 주고받고, Data 는 별도 포트에서 파일 송수신.
- **Active vs Passive 모드**:
  - Active: 서버가 클라이언트로 connection 시도 → **클라이언트 방화벽/NAT 환경에서 실패**.
  - Passive: 클라이언트가 서버로 시도 → **현대 환경 표준**. FluentFTP 기본값.
- **FTPS 변종**:
  - **Explicit (AUTH TLS)**: 평문으로 시작 → `AUTH TLS` 명령으로 TLS 협상 → 보안화. 포트 21 그대로.
  - **Implicit**: 처음부터 TLS. 포트 990. 레거시 방식.
- **인증서 검증 함정**: self-signed 인증서 받아들일 것인가? 운영은 거부, 개발은 허용. **환경별 분기 필수**.

**완료 기준**: 위 5개 항목을 자기 말로 한 줄씩 설명할 수 있음.

#### Step B-2: FluentFTP API 탐색

학습 대상 클래스:
- `AsyncFtpClient` (3.x) — 비동기 클라이언트
- 라이프사이클: `Connect()` → 작업 → `Disconnect()`. `IDisposable`.
- 주요 메서드: `UploadFile`, `DownloadFile`, `GetListing`, `FileExists`, `DeleteFile`, `Rename`, `MoveFile`.
- 진행률 콜백 (`IProgress<FtpProgress>`) — 큰 파일 업로드 UX.

**완료 기준**: 콘솔 한 번 띄워서 진짜 FTP 서버에 파일 1개 업/다운로드 성공.

#### Step B-3: 인터페이스 매핑 검토

`IFileTransferClient` 의 메서드와 FluentFTP 의 메서드를 1:1 매핑 표로 작성. 매핑 안 되는 곳이 있으면:
- 인터페이스 부족 → 메서드 추가
- FluentFTP 부족 → 자체 구현
- 의미 차이 → 인터페이스 시그니처 수정

**함정**: `string remotePath` 가 절대경로인지 상대경로인지 인터페이스에서 명확히 결정. FluentFTP 는 절대경로 권장.

#### Step B-4: Options 설계

`FtpTransferOptions` 후보 항목:
- `Host`, `Port` (default 21)
- `User`, `Password`
- `UseFtps` (bool) + `EncryptionMode` (Explicit / Implicit)
- `ValidateCertificate` (bool, dev/prod 분기)
- `ConnectTimeoutSeconds`, `DataTimeoutSeconds`
- `EnableActiveMode` (false 권장 — Passive 기본)

**검증**: ctor 검증 vs `IValidateOptions<T>` — 이전 `EncryptionOptions` 가드 패턴 재사용 가능.

#### Step B-5: 함정 체크리스트

- [ ] FTPS Explicit / Implicit 둘 중 어느 것? — 서버 사양 확인
- [ ] Passive mode 인지 확인 (방화벽 환경)
- [ ] `using` 또는 명시적 `Disconnect` — 연결 누수 방지
- [ ] 큰 파일은 `Stream` 오버로드 사용 — 메모리 폭발 방지
- [ ] 인증서 검증을 운영 환경에서 켜는지 (dev 에서만 disable)
- [ ] 한글 파일명 — UTF-8 모드 활성화 (`Config.Encoding`)
- [ ] CR/LF 변환 — Binary 모드 강제 (FluentFTP 기본값이지만 명시 권장)

---

### C. SFTP

#### Step C-1: SFTP 는 FTPS 와 다르다

가장 중요한 사전 지식:
- **SFTP ≠ FTPS**. SFTP 는 SSH-2 프로토콜 위에서 동작하는 *완전히 다른 프로토콜*. 단일 채널, 단일 포트(22).
- 이름이 비슷해서 자주 혼동됨. 운영팀과 대화할 때 어느 쪽인지 반드시 확인.

**SSH 프로토콜 핵심**:
- **호스트 키 검증** (HostKey fingerprint) — 클라이언트가 서버 신원 확인. 검증 안 하면 **MITM 가능**.
- TOFU (Trust On First Use) vs `known_hosts` 사전 등록 — 보통 첫 접속 시 fingerprint 기록 후 이후 비교.
- **인증 방식 3종**:
  - 패스워드
  - Public Key (권장 — 키 페어)
  - Keyboard-Interactive (2FA 등)

#### Step C-2: SSH.NET API 탐색

학습 대상:
- `SftpClient` — 메인 클라이언트
- `ConnectionInfo` — 연결 정보 + 인증 방법
- `PasswordAuthenticationMethod` / `PrivateKeyAuthenticationMethod` — 인증 객체
- `SftpClient.HostKeyReceived` 이벤트 — fingerprint 검증 hook
- 메서드: `Connect`, `Disconnect`, `UploadFile`, `DownloadFile`, `ListDirectory`, `Exists`, `DeleteFile`, `RenameFile`

#### Step C-3: 키 관리

- OpenSSH 형식 (`id_rsa`, `id_ed25519`) vs PuTTY 형식 (`.ppk`) — 후자는 변환 필요(`puttygen`).
- Passphrase 보호된 키 — `PrivateKeyFile(path, passphrase)`.
- 운영 환경에서 키 파일 저장 위치 — 평문 저장 금지, 권한 600.
- 학습 단계에선 ssh-keygen 으로 테스트 키 생성 → 컨테이너에 등록.

#### Step C-4: 함정 체크리스트

- [ ] **HostKeyReceived 이벤트에서 fingerprint 검증 했는가?** — 빠뜨리면 MITM 취약
- [ ] `Connect` 후 `using`/`Dispose` — 세션 누수
- [ ] `ListDirectory` 결과에 `.` , `..` 포함됨 — 필터링
- [ ] 디렉터리 vs 파일 구분 (`SftpFile.IsDirectory`)
- [ ] `OperationTimeout` 별도 설정 — 기본 무한 대기
- [ ] 한글 파일명 인코딩 — UTF-8 (서버 sshd 설정 의존)
- [ ] 키 파일 not found / 잘못된 passphrase 예외 처리

---

## 4. 테스트 가이드라인

### 4.1 테스트 전략 분기 — 무엇을 단위, 무엇을 통합?

| 프로토콜 | 단위 테스트 (Mock) | 통합 테스트 (실제 서버) |
|---------|-------------------|----------------------|
| HTTP    | **권장** (HttpMessageHandler Mock — 학습 완료) | 거의 불필요 |
| FTP     | 어려움 (FluentFTP 인터페이스 제한적) | **권장** (Testcontainers) |
| SFTP    | 어려움 (SSH.NET 인터페이스 부족) | **권장** (Testcontainers) |

**결론**: HTTP 는 기존 Mock 패턴 유지. FTP/SFTP 는 Mock 강제하지 말고 통합 테스트로 가는 것이 실무.

### 4.2 HTTP 테스트 — 기존 자산 활용

[Q1]~[Q16] 의 패턴 그대로. `SendAsync` 추가 시 새로 학습할 것:
- **요청 본문 검증 함정** — `ItExpr.Is<HttpRequestMessage>(r => ...)` 람다 안에서 `r.Content!.ReadAsStringAsync()` 를 동기로 호출해야 함 (`.Result`). 비동기 람다 함정.
- **HttpMethod 는 `[InlineData]` 불가** ([Q12] 함정 ① 의 변종) — `string` 으로 받아 `HttpMethod.Parse(...)`.

### 4.3 FTP/SFTP — Testcontainers 패턴 (실무 권장)

**개념**: 테스트 시작 시 Docker 컨테이너로 진짜 FTP/SFTP 서버 띄우고, 종료 시 자동 정리.

**도구**: `Testcontainers` NuGet (`Testcontainers.dll` + 프로토콜별 모듈).

**FTP 컨테이너 후보**:
- `delfer/alpine-ftp-server`
- `stilliard/pure-ftpd`

**SFTP 컨테이너 후보**:
- `linuxserver/openssh-server` (사실상 표준)
- `atmoz/sftp`

**테스트 클래스 라이프사이클**:
- xUnit `IAsyncLifetime` 인터페이스 사용 (단순 ctor/Dispose 로는 비동기 초기화 불가).
- `InitializeAsync()` 에서 컨테이너 시작.
- `DisposeAsync()` 에서 컨테이너 종료.
- 무거운 리소스이므로 **`IClassFixture<T>`** 로 클래스 내 테스트 공유.

**테스트 분류**:
```csharp
[Trait("Category", "Integration")]
public class TestFtpTransferClient : IClassFixture<FtpServerFixture> { ... }
```
→ CI 에서 `dotnet test --filter Category!=Integration` 으로 분리 실행 가능.

### 4.4 FTP/SFTP 단위 테스트가 정 필요하면 — Adapter 패턴

순수 단위 테스트가 필요한 비즈니스 로직(예: 재시도 정책, URL 변환)이 클라이언트 안에 있다면:
- **자체 좁은 인터페이스**(`IFtpAdapter`)로 FluentFTP 호출을 한 번 더 감싸기
- 비즈니스 로직 테스트 시 `IFtpAdapter` 만 Mock
- 트레이드오프: 추상화 레이어 1 단계 추가

→ **재시도/조합 로직이 있을 때만 도입**. 단순 위임이면 통합 테스트로 충분.

### 4.5 테스트 명명 규칙 (재확인)

- `대상_시나리오_기대결과` — [Q12] 의 명제 일반성 원칙 유지
- 통합 테스트는 `[Trait("Category", "Integration")]` 마킹
- `[Theory]` 와 `[Fact]` 의 적용 기준 — 본문에 `if (input == X) ...` 분기 생기면 `[Fact]` 두 개로 분리

### 4.6 학습 일지 컨벤션

`HttpTransferClient` 의 [Q1]~[Q16] 처럼 **함정 일지를 테스트 파일 상단 주석에 누적**:
- 새 클래스마다 [Q1] 부터 새로 시작 (클래스별 독립 일지)
- 한 함정당: 원인 분석 + 해결 방법 + (가능하면) 다른 곳에서 재발할 조건 명시
- 예: [Q9] 의 `disposeHandler: false` 같은 패턴은 다른 `IDisposable` 리소스에서 재현 가능 → 그 점 명시

---

## 5. 함정 종합 체크리스트

구현 시작 전 한 번 훑고, 구현 후 자가 점검용으로 한 번 더.

### 5.1 공통 (모든 프로토콜)

- [ ] `CancellationToken` 모든 비동기 메서드에 받음 + 실제로 흘려보냄 (`ItExpr.IsAny<CancellationToken>` 만으론 검증 안 됨)
- [ ] 옵션 검증 (Host/Port/Timeout 등) — ctor 또는 `IValidateOptions<T>`
- [ ] Timeout 동작 검증 (실제 cts 로 취소 → `OperationCanceledException`)
- [ ] 로깅 — `Feature.Logger/SerilogTest` 통합. 호출/응답/예외에 구조화 로그.
- [ ] 비밀 정보(패스워드, 키)는 **로그에 안 찍힘** (Serilog Destructuring 주의)
- [ ] 예외 정책 — 라이브러리 예외를 그대로 상위로? 자체 예외로 wrapping?

### 5.2 HTTP

- [ ] `HttpClient` 수명 (factory vs single instance vs new)
- [ ] `EnsureSuccessStatusCode` 호출 위치 (`GetStringAsync` 는 자동, `SendAsync` 는 수동)
- [ ] `HttpResponseMessage` 의 `Content` 도 `IDisposable` — `using`
- [ ] BaseAddress 끝 슬래시 vs resource 시작 슬래시 — `Uri` 결합 규칙
- [ ] `HttpRequestException.StatusCode` 는 .NET 5+ 부터 — 버전 확인

### 5.3 FTP

- [ ] Active vs Passive (Passive 권장)
- [ ] FTPS Explicit vs Implicit (서버 사양 확인)
- [ ] 인증서 검증 dev/prod 분기
- [ ] `Disconnect` 누락 → 연결 누수
- [ ] 큰 파일 — Stream 오버로드
- [ ] 한글 파일명 — UTF-8

### 5.4 SFTP

- [ ] **HostKey fingerprint 검증** (가장 중요 — 빠뜨리면 MITM)
- [ ] 키 파일 형식 (OpenSSH vs PuTTY)
- [ ] 키 passphrase 보호
- [ ] `OperationTimeout` 명시 설정
- [ ] `using`/`Disconnect`
- [ ] `.`/`..` 필터링

---

## 6. 학습 우선순위 권장 (개발자가 **7. 개발자가 직접 작성할 영역** 의 Phase 짤 때 참고)

| # | 작업 | 우선순위 | 난이도 | 비고 |
|---|-----|---------|-------|------|
| 1 | ✅ DapperTest 단위 테스트 (이전 plan 잔여) | 높음 | 낮음 | **완료** (2026-04-30, 커밋 `665ee74`/`8517e8c`) — Mock 패턴 일반화 검증, [Q1]~[Q6] 누적 |
| 2 | ✅ HttpTransferClient.SendAsync 구현 + 테스트 (**A-1. `SendAsync<TRequest, TResponse>`** 단계) | 높음 | 중간 | **완료** (2026-05-07, 커밋 `de9caba`) — 직렬화/역직렬화/비-2xx/GET 차단 검증, [Q17]~[Q20] 누적 |
| 3 | **`IHttpClientFactory` 마이그레이션 (A-2 단계)** | 높음 | 중간 | HTTP 학습 트랙의 후반부 — 표준 패턴 체화 |
| 4 | FtpTransferClient 구현 + Testcontainers 통합 테스트 | 중간 | 높음 | 새 학습 영역 — 통합 테스트 패턴 도입 |
| 5 | SftpTransferClient 구현 + Testcontainers 통합 테스트 | 중간 | 높음 | FTP 패턴 재사용 + SSH 특수성 |
| 6 | Polly 회복력 도입 (**A-3. Polly 회복력** 단계) | 후순위 | 중간 | DelegatingHandler 학습 후 자연스럽게 진입 |

**원칙**: 한 번에 한 프로토콜만. 학습 일지 [Q] 가 누적되는 동안은 다음 프로토콜로 넘어가지 말 것 — 직전 답변에서 짚은 "표류" 방지.

---

## 7. 개발자가 직접 작성할 영역 (Claude 작성 금지)

이 가이드라인을 바탕으로 개발자가 본 문서 또는 별도 섹션에 작성:

### 7.1 디자인 결정 기록

#### ✅ 결정 1: 인터페이스 통합 vs 분리 (**2. 실무 관점 사전 토픽 → 2.1 통합 추상화 vs 프로토콜별 추상화**) — **선택지 A: 현 구조 유지 (분리)**
- **결정자**: 개발자
- **결정일**: 2026-04-29
- **근거**: HTTP 는 Connect / Disconnect / Exists / Delete / Rename 등이 본질적으로 불가능 (stateless, 파일시스템 추상화 부재). 같은 인터페이스로 묶으면 HTTP 구현체에서 절반 이상이 `NotSupportedException` 이 됨 → 추상화 의미 상실. **stateless(`IDataTransferClient`) 와 stateful + 파일시스템(`IFileTransferClient`) 의 분리 유지가 옳음.**
- **결과 영향**: HttpTransferClient 는 `IDataTransferClient` 만 구현. FtpTransferClient / SftpTransferClient 는 `IFileTransferClient` 구현. (필요 시 IDataTransferClient 도 동시 구현은 가능 — 스트림 받기 같은 단순 케이스)

#### ✅ 결정 2: `IHttpClientFactory` 도입 (**2. 실무 관점 사전 토픽 → 2.2 HttpClient 수명 — `IHttpClientFactory`**) — **HTTP 학습 트랙에 포함 (단계적 마이그레이션)**
- **결정자**: 개발자
- **결정일**: 2026-04-29
- **결정 내용**: HTTP 학습은 두 단계로 진행.
  - **A-1 (먼저)**: 기본 `HttpClient` 사용법 — `SendAsync` 까지 직접 `new HttpClient()` 패턴으로 학습.
  - **A-2 (다음)**: `IHttpClientFactory` 로 마이그레이션 — 학습한 코드를 표준 패턴으로 전환하며 차이를 체감.
- **근거**: `IHttpClientFactory` 는 .NET 생태계의 *표준 도구*. 한 번은 만나야 함. "기본 → 마이그레이션" 순서로 학습하면 문제(socket exhaustion 등) 와 해결책의 인과를 직접 체험할 수 있음.
- **Pure DI 충돌 우려는 무효**: Pure DI 는 `Test.RunConsole` 의 학습 예시일 뿐 프로젝트 원칙 아님. `ServiceCollection` 도입은 자유.

#### ⏳ 결정 3: 예외 정책 — **미정**
- 옵션 A: 라이브러리 예외(`HttpRequestException`, `FtpException`, `SshException`) 를 그대로 노출
- 옵션 B: 자체 예외(`TransferException` 등) 로 wrapping
- 결정 시점: FTP 구현 시작 전. 첫 실제 라이브러리 예외를 마주칠 때 자연스럽게 결정 가능.

#### ✅ 결정 4: Dapper 테스트 우회 전략 (**3. 프로토콜별 학습 가이드 → 0. DapperTest 단위 테스트 → Step D-2**) — **선택지 B: 자체 좁은 어댑터 (`IDapperAdapter`)**
- **결정자**: 개발자
- **결정일**: 2026-04-29
- **근거**: `Dapper.Query<T>` 가 `IDbConnection` 의 *static 확장 메서드* 라 Moq(virtual override 메커니즘)으로 가로챌 수 없음 (학습 일지 [Q1]~[Q2]). 선택지 A(IDbConnection 직접 Mock)는 SQL 파라미터/리더 시뮬레이션 셋업이 비대해지고, 선택지 C(SQLite in-memory)는 SQL 문법 차이 학습이 본 단계 목적과 어긋남. 좁은 어댑터는 **HttpTransferClient 의 Mock 패턴을 다른 도메인에서 재현** 한다는 **0. DapperTest 단위 테스트** 의 학습 의도와 일치.
- **결과 영향**:
  - `IDapperAdapter` (인터페이스, `Query(string sql)` 만 노출) + `DapperAdapter` (구현체, `IDbConnection` 보유) + `DapperClient` (`IDapperAdapter` 의존) 3 계층 구조 ([Q4] 참조).
  - `DapperClient` 단위 테스트는 `Mock<IDapperAdapter>` 만 사용. Dapper 의 static API 는 `DapperAdapter` 한 곳에 격리됨.
  - `DapperAdapter` 자체는 thin wrapper 라 단위 테스트 안 함 (필요 시 SQLite/Testcontainers 통합 테스트 영역).
  - 학습 일지 [Q5]~[Q6] — Setup vs SetupGet, .CallBase() 적용 가능 케이스 함정 누적.

#### ✅ 결정 5: SEC-001 (SQL Injection 화이트리스트) (**3. 프로토콜별 학습 가이드 → 0. DapperTest 단위 테스트 → Step D-4**) — **처리**
- **결정자**: 개발자
- **결정일**: 2026-04-30
- **근거**: 선택지 B 채택 후 `DapperClient` 가 thin wrapper 가 되어 단위 테스트 가치가 낮아진 문제 ([Q3]) 발견. 화이트리스트 검증을 SUT 에 추가하면 *결정/검증 로직* 이 생겨 단위 테스트 명제(허용/거부 분기)가 명확해짐 — 보안 처리와 학습 명제 정렬이 동시에 해결됨.
- **결과 영향**:
  - `DapperClient._allowedTables` (HashSet) + 미허용 테이블명에 대해 `ArgumentException`.
  - 테스트 2종: `SelectTop10_WithAllowedTable_CallsQuery` (허용 + Verify Once), `SelectTop10_WithDisallowedTable__ThrowsAndDoesNotCallAdapter` (거부 + Verify Never).
  - 본격 운영 코드의 화이트리스트는 아니고 *학습용 임시 가드*. 운영 전환 시 검토 항목으로 남김.

### 7.2 Phase 분해
프로토콜별로 다음 단위로 분해해서 작성:
- 변경/추가 파일 목록
- 각 Phase 의 완료 기준 (구체적 산출물)
- Phase 간 의존성 (병렬 가능 / 순차 필수)
- 예상 소요 시간 (학습 분량 가늠용)

(직전 plan 의 Phase 1~4 표 형식 재사용 권장)

### 7.3 인터페이스 시그니처 초안
- `IFtpAdapter` (도입 결정 시) 시그니처
- `FtpTransferOptions`, `SftpTransferOptions` 항목 목록 (이름/타입/기본값)
- 자체 예외 타입 (도입 결정 시)

### 7.4 DI 등록 방안
- Pure DI 유지 시 — `Program.cs` 에서 어떻게 조립?
- ServiceCollection 도입 시 — Lifetime 결정 (FtpClient = Transient? Scoped?)

작성 후 Claude 에게 리뷰 요청 — Claude 는 **7. 개발자가 직접 작성할 영역** 본문은 작성하지 않고, 작성된 결과를 검토만 한다.

---

## 8. 진행 로그 (개발자가 직접 작성)

각 작업 완료 시 한 줄씩 추가.

- 2026-04-29 — `Feature.Dapper.Tests` 프로젝트 신설, `DapperClient` → `IDapperAdapter` 위임 구조로 전환 (선택지 B). 학습 일지 [Q1]~[Q6] 누적: static 확장 메서드 Mock 불가([Q1]/[Q2]), thin wrapper 테스트 가치 0 ([Q3]), 인터페이스 계층 구조 ([Q4]), Setup vs SetupGet ([Q5]), .CallBase() 적용 케이스 ([Q6]). (커밋 `665ee74`)
- 2026-04-30 — `DapperClient.SelectTop10` 화이트리스트 검증 추가 (SEC-001 처리). 테스트 2종(허용/거부) 통과, 거부 케이스에서 `Verify(... Times.Never)` 로 단락 검증. (커밋 `8517e8c`)
- 2026-05-06 — `HttpTransferClient.SendAsync<TRequest, TResponse>` 책임 범위 학습 및 1차 구현. `TRequest` 는 JSON request body DTO, `TResponse` 는 JSON response DTO 로 정리. raw GET(`GetStringAsync`/`GetStreamAsync`) 과 JSON API 호출(`SendAsync`) 책임 분리, `GET` 은 `SendAsync` 에서 비지원 처리. 테스트 작성은 다음 단계로 보류.
- 2026-05-07 — `HttpTransferClient.SendAsync<TRequest, TResponse>` 핵심 테스트 작성 완료. GET 차단(`Times.Never`), request JSON 직렬화(`Callback` 캡처), response JSON 역직렬화(DTO 프로퍼티 비교), 비-2xx `HttpRequestException.StatusCode` 검증을 추가. 학습 일지 [Q17]~[Q20] 누적. (커밋 `de9caba`)

---

## 9. 참고 자료

- HttpClient 가이드라인 — Microsoft Docs "Use IHttpClientFactory"
- FluentFTP — GitHub Wiki (Async API, FTPS 설정)
- SSH.NET — GitHub Wiki (HostKeyReceived, Authentication Methods)
- Testcontainers .NET — 공식 사이트
- Polly — 공식 사이트 (Retry / CircuitBreaker / Timeout)
- xUnit `IAsyncLifetime` / `IClassFixture` — xUnit 공식 docs
