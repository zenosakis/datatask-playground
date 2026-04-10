# DataTask.Orchestrator C# 마이그레이션 아키텍처 설계 (v2)

기존 Delphi 기반의 레코드 전송 응용 프로그램을 C# 생태계 환경(.NET 8 권장)에 맞춰 계층과 역할을 분리한 재구성 아키텍처 설계입니다. 사용자 UI를 프론트엔드로 완전 분리하고 백엔드는 API 및 Worker 서비스 중심으로 구성합니다.

## 1. 솔루션 프로젝트 구성 (Project Structure)

 UI(폼 화면) 기능은 외부 프론트엔드 애플리케이션에 위임하므로 솔루션 내 C# 프로젝트에서 제외됩니다. C# 솔루션은 비즈니스 로직, 데이터 계층, WebAPI, 백그라운드 Worker 서비스로 분리 구성합니다.

### 1-1. `DataTask.Orchestrator.Core` (Class Library)
**역할:** 애플리케이션의 핵심 비즈니스 로직, 데이터 모델, 전송/조작을 위한 공통 인터페이스가 위치합니다.
*   **Models:** 작업(Task), 설정(Config) 모델, SQLite 데이터 모델 등
*   **LayoutAssemble:** 핵심이 되는 데이터/파일명 레이아웃 조립(파싱 및 매핑) 로직. 구분자(Delimiter) 조합, 고정 길이(Length) 조합, JSON/XML 파싱 모듈.
*   **Pipelines:** 복호화, 코덱 변환, 암호화, 전송 등을 단계별(Filter)로 처리할 수 있는 파이프라인(Pipeline) 인터페이스.
*   **Interfaces:** 추후 구현체(DB 연결, DLL 로드 컨트롤러 등)의 의존성 역전을 위한 `IRepository`, `ITaskService` 등.

### 1-2. `DataTask.Orchestrator.Data` (Class Library)
**역할:** 저장소(SQLite) 및 외부 매체와의 데이터 통신을 담당합니다.
*   **Data Access Layer (Dapper):** Entity Framework Core 대신 경량 마이크로 ORM인 **Dapper**를 적용하여 SQLite와 통신합니다. 빠른 조회와 매핑 성능을 확보합니다.
*   **Query Manager:** 외부 SSQL 파일 (암호화된 쿼리 파일)을 런타임에 읽어 복호화하고, 동적 쿼리 문자열로 변환하여 Dapper를 통해 실행할 수 있는 서비스 제공.

### 1-3. `DataTask.Orchestrator.WorkerService` (ASP.NET Core Worker Service)
**역할:** 스케줄링 및 실제 멀티 스레드 작업을 수행하는 서비스 (Windows 서비스 구동 가능). WebAPI와 분리되어 묵묵히 백그라운드 작업을 실행합니다.
*   **Scheduler & Thread Manager:** 지정된 간격에 맞추어 작업을 스케줄링하는 스레드를 구동합니다 (내장 `BackgroundService` 또는 Quartz.NET 기반).
*   **Task Executor:** 작업의 종류(전송 vs 수집)에 따라 분기하여 실제 처리 전략을 수행합니다.

### 1-4. `DataTask.Orchestrator.WebAPI` (ASP.NET Core Web API & SignalR)
**역할:** 프론트엔드 UI로부터 명령을 수신하고, WorkerService와 통신(제어/상태 전파)하는 역할입니다.
*   **Web API (REST):** 작업(Task) 리스트, 로깅, 설정 정보 제공 API 엔드포인트 제공.
*   **SignalR 통신망:** WorkerService와의 통신, 그리고 프론트엔드의 실시간 작업 상태 모니터링을 위해 **SignalR**을 활용합니다.
    *   API 서버 내에 `SignalR Hub`를 띄워, WorkerService가 SignalR 클라이언트로 접속해 현재 진행 상태나 로그 정보를 실시간 스트리밍합니다.
    *   프론트엔드에서 수동 작업 시작 명령을 API로 보내면, API 서버는 SignalR을 통해 WorkerService로 실행 명령을 즉각 브로드캐스팅합니다.

### 1-5. `프론트엔드 (C# 제외 / 외부 React, Vue 등 적용)`
**역할:** 사용자가 조작하는 프론트엔드 애플리케이션 (React, Vue, 혹은 Blazor WebAssembly 등 분리 기술).
*   WebAPI 컴포넌트를 통해 데이터(작업 목록, 설정 등)를 가져와 그려줍니다.
*   그리드에서 특정 작업 우클릭(스케줄러 시작 / 수동 날짜 작업 시작) 이벤트를 발생시키고, SignalR을 통해 실시간 스레드 실행 상황을 모니터링합니다.

---

## 2. 작업 별 라이프사이클 흐름 설계

프론트엔드 명령에 기반한 사용자 요구사항의 동작 시나리오는 다음과 같이 구현됩니다.

### 2-1. 프론트엔드 통신 및 스레드 준비
1. **[Front <-> API]** 프론트엔드 화면에서 API 호출을 통해 SQLite에서 읽어온 등록된 작업 정보 리스트를 표출합니다.
2. **[Front -> API -> SignalR -> Worker]** 특정 작업을 우클릭/시작 할 경우 다음 두 방향으로 SignalR 메시지가 전송됩니다:
    *   **(자동) 스케줄러 시작 명령:** WorkerService에서 해당 설정을 로드하고 타이머 등록. 실행 주기가 도래하면 **[작업 스레드 생성 단계]** 진입.
    *   **(수동) 수동 날짜 작업:** 날짜를 파생 데이터로 받아 즉시 1회성 Job 스레드를 활성화하여 **[작업 스레드 생성 단계]** 진입.

### 2-2. 작업 스레드 구조 및 동작 시퀀스 (WorkerService)
실제 처리를 담당할 스레드가 시작되며, **수집(Collect)** 설정을 분기 처리하고 특히 **전송(Transfer) 파트에는 파이프라인 패턴을 적용**합니다.

**A. 전송 작업 (Transfer Task)**
*   **[4번: DB 조회]** Dapper를 이용해 `.ssql` (복호화) 쿼리로 대상 건 `INSERT` 후 `SELECT` 실행.
*   **[5번: 레이아웃 조립]** `Core.LayoutAssemble` 호출.
    *   결과값 + 수동 입력 값 등을 조합하여 지정 규칙(구분자, 길이, 복합, JSON, XML)대로 데이터를 직렬화.
*   **[6~8번: 파일 및 데이터 전송 파이프라인 패턴 (Pipeline & Filters)]**
    *   JSON 기반의 설정 정보에 맞추어 `IFilter` 단계들을 동적으로 조립합니다.
    *   **예시 흐름 1:** `[복호화 파이프] -> [포맷 변환 파이프] -> [암호화 파이프] -> [네트워크 전송 파이프]`
    *   **예시 흐름 2:** `[복호화 파이프] -> [네트워크 전송 파이프]`
    *   *메모리/파일 누수 방지:* 각 파이프라인(특히 복호화/암호화/전송)에서 파일 생성 및 접근 시 `FileStream`이나 `NetworkStream` 인스턴스를 무조건 `using` 블록 또는 `await using` 구문으로 철저하게 감싸, 후처리나 다음 스케줄 가동 시 파일 Lock (접근 거부) 에러가 나지 않도록 엄격히 통제합니다.
*   **[9번: 후처리]** 전송 파이프라인의 최종 결과를 받아 송출 성공 여부 플래그 처리, 전송 완료/실패 디렉토리로 임시 파일 이동.
*   **[10번: 종료]** 파이프라인과 스레드에서 생성된 `using` 외의 임시 요소들 파기 및 스레드 자원 반납.

**B. 수집 작업 (Collect Task)**
*   **[4번: 레거시 통신]** legacy `DLL`을 P/Invoke로 C#에서 호출 (작업 파라미터 매핑하여 포인터 전달). 
*   **[5번: 종료]** DLL 수행 결과에 따라 작업 스레드 즉각 반환 성공/실패 여부를 SignalR 로 API 쪽에 통지.

---

## 3. 핵심 아키텍처 반영 포인트 요약
1. **SignalR 통신망:** WorkerService가 독단적으로 도는 백그라운드 앱이 되지만 API서버를 통해 실시간으로 상태를 쏘아주어 프론트엔드가 언제든 화면을 실시간 업데이트할 수 있습니다.
2. **프론트엔드의 격리:** 더 이상 무거운 Windows Form 라이브러리나 OS 제약에 묶이지 않고 SPA 생태계로 통합이 가능해졌습니다.
3. **Dapper의 강력한 성능:** 복잡하고 원시적인 SSQL 포맷의 동적 쿼리를 실행하는데 있어 오히려 무거운 ORM(EF Core)보다 Dapper가 강력하고 유연한 성능을 발휘합니다.
4. **파이프라인 패턴 확장성:** 설정값 하나로 작업 단계를 자유자재로 떼거나 붙일 수 있어(플러그인화) 추후 **코덱이 추가되거나, 새로운 암호화 방식**이 들어오더라도 해당 필터 클래스 하나만 추가하면 유지보수 끝입니다. 스트림(IO) 사용 시 철저한 Lock 누수 방지도 파이프라인 설계에서 체계적으로 관리할 수 있습니다.

---

## 4. 백엔드 솔루션 파일/디렉터리 구조도

명세된 아키텍처를 바탕으로 한 C# Visual Studio 솔루션 모델 및 디렉토리 구조도입니다.

```text
DataTask.Orchestrator.sln
│
├── src/
│   │
│   ├── DataTask.Orchestrator.Core/                  # [Class Library] 비즈니스 로직 및 모델
│   │   ├── Models/
│   │   │   ├── TaskConfig.cs                        # 작업 옵션 설정(JSON 매핑용)
│   │   │   └── RecordItem.cs                        # DB 조회 및 레이아웃 조립 과정의 중간 처리 객체
│   │   ├── Layouts/                                 # 레이아웃 조립 전략 엔진
│   │   │   ├── ILayoutBuilder.cs                    
│   │   │   ├── DelimiterLayoutBuilder.cs            # 구분자 방식 조회/조립 모듈
│   │   │   ├── FixedLengthLayoutBuilder.cs          # 고정 길이 방식 조립 모듈
│   │   │   └── JsonXmlLayoutParser.cs               # JSON/XML 방식 매핑 파서
│   │   ├── Pipelines/                               # 파일/데이터 전송 파이프라인 (Filter 패턴)
│   │   │   ├── ITransferFilter.cs                   # 파이프라인 패턴 필터 인터페이스
│   │   │   ├── PipelineContext.cs                   # 파이프라인 간 상태/데이터 전달 객체 (Stream 등 포함)
│   │   │   ├── DecryptFilter.cs                     # 복호화 블록
│   │   │   ├── CodecTransformFilter.cs              # 인코딩/암호화/코덱 변환 블록
│   │   │   └── NetworkSendFilter.cs                 # 파일/데이터 전송 블록 (Lock 해제 주의 구문)
│   │   └── Legacy/
│   │       └── DllInvokeWrapper.cs                  # [수집 작업용] Legacy DLL P/Invoke C# 래퍼 클래스
│   │
│   ├── DataTask.Orchestrator.Data/                  # [Class Library] Dapper 기반 저장소
│   │   ├── Repositories/
│   │   │   ├── ITaskRepository.cs                   # 의존성 역전용 Repository 인터페이스
│   │   │   └── TaskRepository.cs                    # Dapper 기반 SQLite CRUD 구현체
│   │   ├── QueryManager/
│   │   │   └── SsqlQueryManager.cs                  # SSQL 파일(암호화된 쿼리) 동적 로드/암호 해제 처리기
│   │   └── SQLiteConnectionFactory.cs               # IDbConnection 초기화 Factory
│   │
│   ├── DataTask.Orchestrator.WorkerService/         # [Worker Service] 스레드/스케줄러 중앙 제어소
│   │   ├── Schedulers/                              
│   │   │   └── TaskBackgroundScheduler.cs           # 설정값 기반 작업 루프/타이머 스레드 구동기
│   │   ├── Executors/
│   │   │   ├── ITaskExecutor.cs                     
│   │   │   ├── TransferTaskExecutor.cs              # 전송 작업 전략 런타임 (DB조회 -> 파이프라인 가동)
│   │   │   └── CollectTaskExecutor.cs               # 수집 작업 전략 런타임 (DLL 연동)
│   │   ├── HubClients/
│   │   │   └── SignalRStateReporter.cs              # API의 SignalR Hub로 실시간 작업 로그 및 진행도 브로드캐스팅
│   │   ├── Program.cs                               # 빌더, IHostedService 등록 구성
│   │   └── appsettings.json                         
│   │
│   └── DataTask.Orchestrator.WebAPI/                # [ASP.NET Web API] 웹 구동 및 SignalR 중앙 Hub
│       ├── Controllers/
│       │   ├── TaskController.cs                    # 수동 작업 실행 명령 및 리스트 조회 엔드포인트
│       │   └── ConfigController.cs                  # 프론트엔드가 작업 옵션을 수정/조회할 때 쓰이는 엔드포인트
│       ├── Hubs/
│       │   └── UIHub.cs                             # [중심부] Worker와 프론트엔드를 양방향으로 묶는 SignalR 라우터
│       ├── Program.cs                               # 미들웨어 파이프라인 구성, CORS(프론트엔드 연동), SignalR 등록
│       └── appsettings.json                         
│
└── tests/                                           # [Unit Test] 단위/통합 테스트 (xUnit 등 권장)
    ├── DataTask.Orchestrator.Core.Tests/            # 레이아웃 문자열 치환 및 알고리즘 검증
    └── DataTask.Orchestrator.Data.Tests/            # SSQL 복호화 및 쿼리 파라미터 매핑 검증
```

---

## 5. 단계별 학습 및 PoC(Proof of Concept) 진행 계획

거대한 아키텍처를 한 번에 구축하기보다, C#의 주요 핵심 기술들을 작은 단위(독립된 콘솔 또는 기본 템플릿) 프로젝트로 쪼개어 하나씩 구현하고 테스트하는 것을 권장합니다.

각 단계마다 새로운 솔루션을 만들거나, 하나의 통합 솔루션 안에 여러 개의 콘솔 앱(`PoC.Step1.Data`, `PoC.Step2.Pipeline` 등)을 만들어 독립적으로 테스트할 수 있습니다.

### Step 1: Dapper & SSQL 데이터 접근 (Console App)
* **목표:** SQLite 연결 및 외부 암호화 쿼리 로드/실행 테스트
* **학습 내용:**
  1. `Microsoft.Data.Sqlite` 및 `Dapper` 패키지 설치.
  2. 단순한 `.ssql` 파일 (또는 텍스트 파일)을 읽어 문자열로 된 동적 쿼리 준비.
  3. Dapper의 `.QueryAsync<T>()`와 파라미터 매핑(`DynamicParameters`)을 이용한 DB 통신 확인.

### Step 2: 레이아웃 조립 Strategy/Parser 구현 (Console App)
* **목표:** DB 결과값과 설정값을 조합하여 문자열(데이터 파일 내용, 파일명) 만들기
* **학습 내용:**
  1. 객체 지향의 **전략 패턴(Strategy Pattern)** 학습 (`ILayoutBuilder` 인터페이스).
  2. `DelimiterLayout` (구분자 기준 분할/병합) 방식 알고리즘 구현.
  3. `FixedLengthLayout` (고정 길이 패딩) 방식 알고리즘 구현 및 단위 테스트.

### Step 3: 데이터 및 파일 가공 (인코딩, 코덱, 암호화) 동작 검증 (Console App)
* **목표:** 일반 텍스트나 파일에 대해 인코딩 변환, 녹취 파일 등의 코덱 변환, 그리고 암복호화 적용하기
* **학습 내용:**
  1. **데이터 처리:** `System.Text.Encoding`을 통해 UTF-8 ↔ EUC-KR 등 문자열 인코딩을 변환하는 모듈 작성.
  2. **파일 처리 (녹취/미디어 파일 등):** NAudio 등의 C# 라이브러리 (또는 기존 FFMPEG/커스텀 코덱 래퍼)를 활용해 파일의 오디오 포맷 및 코덱 변환 테스트.
  3. **암호화/복호화:** `System.Security.Cryptography`를 이용하여 AES, RSA 등 지정된 알고리즘 규격으로 파일 또는 단순 문자열을 암·복호화하는 로직 단독 테스트.

### Step 4: Stream 파이프라인 패턴 동작 검증 (Console App)
* **목표:** 파일/네트워크 처리에서 발생할 수 있는 Lock 방지 및 Filter 체이닝 테스팅
* **학습 내용:**
  1. `IFilter` 인터페이스와 단계별(Step 3에서 만든 복호화, 암호화 등) 필터 클래스 작성.
  2. `FileStream` 생성 후 작업 완료 시 반드시 자원이 환원되도록 `using` 구문 설계 및 폴더 내 Lock 잡힘 여부 반복 테스트.
  3. 간단한 파일을 읽어 [복호화 -> 암호화 -> 전송] 혹은 [복호화 -> 코덱 변환 -> 전송] 설정에 맞춰 Filter를 자유자재로 끼웠다 뺐다 했을 때 끊김 없이 스트림이 타고 나가는지 테스트.

### Step 5: Legacy DLL 연동 분리 테스트 (Console App)
* **목표:** 기존 C/C++(또는 델파이) DLL을 C# 환경에서 정상 호출하기
* **학습 내용:**
  1. `[DllImport("legacy.dll")]` (P/Invoke) 마샬링 기법 학습.
  2. 포인터나 구조체 파라미터를 C# `struct` 또는 `ref`, `IntPtr`로 매핑 후 호출 테스트.

### Step 6: Worker Service 와 스케줄링 (ASP.NET Core Worker Service)
* **목표:** 데몬(백그라운드) 형태로 돌아가는 Worker 서비스 구축 및 타이머/다중 스레드 제어
* **학습 내용:**
  1. Visual Studio의 'Worker Service' 템플릿으로 프로젝트 생성.
  2. `BackgroundService` 클래스를 상속받아 무한 루프(`ExecuteAsync`) 안에서 `Task.Delay`나 `Timer`를 이용한 간격 스케줄링 구현.
  3. 특정 간격 도래 시 새로운 `Task.Run()`을 호출하여 비동기로 개별 작업 스레드(Thread)를 독립 파생시키는 법 숙지.

### Step 7: SignalR 실시간 통신망 구축 (Web API + C# Client + JS Client)
* **목표:** 프론트엔드 - Web API - Worker 간 실시간 로그 및 상태 중계
* **학습 내용:**
  1. ASP.NET Core Web API 템플릿에 `SignalR Hub` 추가 (`builder.Services.AddSignalR()`).
  2. 별도 콘솔 앱 (Worker 역할)에서 `Microsoft.AspNetCore.SignalR.Client` 패키지를 설치해 Hub에 연결 후 상태 진행도(%) 쏘아주기.
  3. HTML/JS 간단한 페이지(프론트엔드 역할)를 띄워 Hub에서 날라오는 로그 실시간 수신 및 표시 확인.

### Step 8: 최종 아키텍처 조립 (솔루션 스캐폴딩)
* **목표:** 1~7단계에서 성공적으로 구축/검증된 조각들을 본 설계 안인 멀티 프로젝트(Core, Data, Worker, API)로 이관하여 최종 융합.
* **학습 내용:**
  프로젝트 간 참조 관계 설정 (`Worker -> Core, Data` / `WebAPI -> Worker 등`), DI (Dependency Injection) 컨테이너에 각 서비스들 일괄 등록 및 최종 빌드 확인.
