# Implementation Plan: DI 구조 개선 (Pure DI)

> 작성일: 2026-04-16
> 근거: `/di-check` 감사 결과
> 방식: Pure DI (ServiceCollection 미사용)

---

## 목표

`LoadSettingsTest` 내부의 `new AesCbcEncryptor()` 를 제거하고,
모든 의존성 생성을 `Program.cs` 한 곳에서 조립하는 구조로 변경한다.
`ServiceCollection` 은 도입하지 않는다.

---

## 현재 상태 vs 목표 상태

```
[현재]
Program.cs
  ├── new LoadSettingsTest(config)         ← config만 넘김
  └── new DapperTest(ip, port, ...)

LoadSettingsTest.cs
  └── new AesCbcEncryptor(_Key, _Iv)       ← 내부에서 직접 생성 (문제)

[목표]
Program.cs
  ├── new AesCbcEncryptor(key, iv)         ← 딱 한 번 생성 (사실상 Singleton)
  ├── new LoadSettingsTest(config, encryptor)  ← 주입
  └── new DapperTest(connection)           ← IDbConnection 주입

LoadSettingsTest.cs
  └── 생성자에서 IEncryptor 주입받음 (내부 new 제거)
```

---

## 진행 상황

| Phase | 내용 | 상태 |
|-------|------|------|
| Phase 1 | `EncryptionOptions` 도입 | ✅ 완료 |
| Phase 2 | `LoadSettingsTest` 생성자 주입 | ✅ 완료 |
| Phase 3 | `DapperTest` 생성자 주입 + `private readonly` | ✅ 완료 |
| Phase 4 | `Program.cs` 조립 코드 정리 | ✅ 완료 |

---

## 단계별 구현 계획

### Phase 1 — Feature.Encryption: `EncryptionOptions` 도입 (난이도: 낮음)

**변경 파일**: `Feature.Encryption/EncryptionOptions.cs` 신규

**할 일:**
- Key, Iv 를 담는 `EncryptionOptions` 설정 클래스 추가
- `AesCbcEncryptor`, `AesEcbEncryptor` 생성자에 `EncryptionOptions` 오버로드 추가 (기존 `byte[]` 생성자 유지)

**변경 전/후:**
```csharp
// Before: Program.cs에서 byte[] 직접 계산 후 전달
var encryptor = new AesCbcEncryptor(key, iv);

// After: 설정 객체로 전달 (가독성 향상)
var options   = new EncryptionOptions { Key = config["Key"], Iv = config["Iv"] };
var encryptor = new AesCbcEncryptor(options);  // IEncryptor
```

> `IEncryptor` 인터페이스는 이미 존재하므로 추가 작업 없음.

---

### Phase 2 — Feature.LoadSettings: `IEncryptor` 생성자 주입 **(핵심)**

**변경 파일**: `Feature.LoadSettings/LoadSettingsTest.cs`

**할 일:**
- 생성자 파라미터에 `IEncryptor encryptor` 추가
- 내부 `new AesCbcEncryptor(...)` 제거
- `_Key`, `_Iv` 필드 제거 (생성자에서 직접 쓰던 것이므로 불필요해짐)

**변경 전/후:**
```csharp
// Before
public LoadSettingsTest(IConfiguration config)
{
    _config = config;
    var key = config["Key"];
    if (key != null) _Key = Encoding.UTF8.GetBytes(key);
    var iv  = config["Iv"];
    if (iv  != null) _Iv  = Encoding.UTF8.GetBytes(iv);
    _Encryptor = new AesCbcEncryptor(_Key, _Iv);  // ← 제거 대상
}

// After
public LoadSettingsTest(IConfiguration config, IEncryptor encryptor)
{
    _config    = config;
    _Encryptor = encryptor;  // 주입받음
}
```

---

### Phase 3 — Feature.Dapper: `IDbConnection` 생성자 주입 (난이도: 낮음)

**변경 파일**: `Feature.Dapper/DapperTest.cs`

**할 일:**
- 생성자를 `(string ip, port, ...)` → `(IDbConnection connection)` 으로 변경
- `_dbConnection` 필드를 `public` → `private readonly` 로 변경 **(DI-W1 해결)**
- `SqlConnection` 생성 책임을 `Program.cs` 로 이동
- `SelectTest` SQL Injection 방어: 테이블명 화이트리스트 검증 추가 **(SEC-001 해결)**

**변경 전/후:**
```csharp
// Before
public DapperTest(string ip, string port, string database, string user, string password)
{
    _connectionString = $"Server={ip},{port};...";
    _dbConnection = new SqlConnection(_connectionString);
}

// After
public DapperTest(IDbConnection connection)
{
    _dbConnection = connection;
}

// SQL Injection 방어
private static readonly HashSet<string> _allowedTables = ["TBL_DIALEDLOG"];

public IEnumerable<dynamic> SelectTest(string tableName)
{
    if (!_allowedTables.Contains(tableName))
        throw new ArgumentException($"허용되지 않은 테이블명: {tableName}");

    return _dbConnection.Query($"SELECT TOP 100 * FROM {tableName}");
}
```

---

### Phase 4 — Program.cs: 조립 위치 통일 (난이도: 낮음, Phase 1~3 완료 후)

**변경 파일**: `Test.RunConsole/Program.cs`

**할 일:**
- 모든 `new` 생성을 상단에 집중 (의존성 그래프를 한눈에 파악 가능하게)
- `AesCbcEncryptor` 를 가장 먼저 생성하고 이후 단계에 전달

**목표 구조:**
```csharp
// ── 1. 설정 로드 ──────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.local.json", optional: true)
    .Build();

// ── 2. 의존성 조립 (Pure DI) ──────────────────────────────
SerilogTest.Configure();

var options   = new EncryptionOptions { Key = configuration["Key"], Iv = configuration["Iv"] };
IEncryptor encryptor = new AesCbcEncryptor(options);        // 한 번 생성, 재사용 가능

var settings  = new LoadSettingsTest(configuration, encryptor); // IEncryptor 주입

var connection = new SqlConnection($"Server={settings.Get("DB:Ip")}, ...");
var db         = new DapperTest(connection);                 // IDbConnection 주입

// ── 3. 비즈니스 로직 ──────────────────────────────────────
Log.Information("=== 콘솔 프로그램 시작 ===");
var rows = db.SelectTest("TBL_DIALEDLOG");
// ...
```

---

## 구현 순서

```
Phase 1 (독립) — EncryptionOptions 추가
Phase 2 (1 의존) — LoadSettingsTest 생성자 수정
Phase 3 (독립) — DapperTest 생성자 수정 + SQL Injection 방어
      ↓
Phase 4 (1~3 의존) — Program.cs 조립 코드 정리
```

Phase 1, 3 은 서로 독립적이므로 병렬 작업 가능.

---

## 해결되는 항목

| 항목 | Phase | 처리 내용 |
|------|-------|-----------|
| DI-002 (`new AesCbcEncryptor` 내부 생성) | 2 | 생성자 주입으로 교체 |
| DI-004 (의존성 생성 위치 분산) | 4 | `Program.cs` 상단으로 집중 |
| DI-W1 (`public _dbConnection`) | 3 | `private readonly` 로 변경 |
| SEC-001 (SQL Injection) | 3 | 화이트리스트 검증 추가 |

---

## DI Auditor 검증 목록 (Phase 4 완료 후)

- [x] `LoadSettingsTest` 생성자에 `IEncryptor` 주입 확인
- [x] `LoadSettingsTest` 내부 `new AesCbcEncryptor` 완전 제거 확인
- [x] `DapperTest._dbConnection` `private readonly` 확인
- [x] `DapperTest` 생성자가 `IDbConnection` 받는지 확인
- [x] `Program.cs` 상단에 모든 의존성 조립 집중 확인
- [-] SQL Injection 화이트리스트 적용 — 임시 코드로 보류 (SEC-001 무시)

---

## Unit Tester 작업 목록 (Phase 완료 후)

```
/mock-gen LoadSettingsTest   → IEncryptor Mock 주입 테스트
/mock-gen DapperTest         → IDbConnection Mock 주입 테스트
```
