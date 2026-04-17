// 로그용 참조
using Feature.Logger;
using Serilog;
using Playground.CSharp;
using System.Text;

// 로그
SerilogTest.Configure(); // Configure 를 호출해줘야 로그 기록이 시작 됨
Log.Information("=== 콘솔 프로그램 시작 ===");


//////////////// playground 확인용 콘솔 로그 시작 ////////////////////////

// 클래스 생성
var createOverload1 = new CreatorOverload(); // 생성자에 아무것도 넘기지 않았으므로 공백(초기값)
Log.Information("오버로드1 생성값 확인: {name}", createOverload1.TestName);
var createOverload2 = new CreatorOverload // 객체 초기화 방식 (객체를 생성한 후 초기화하므로, TestName 이 public 에 set 이 있어야 함)
{
    TestName = Encoding.UTF8.GetBytes("NEW NAME")
};
Log.Information("오버로드2 생성값 확인: {name}", createOverload2.TestName);
var createOverload3 = new CreatorOverload(1); // int 오버로드
Log.Information("오버로드3 생성값 확인: {name}", createOverload3.TestName);
var createOverload4 = new CreatorOverload(1.1m); // decimal 오버로드
Log.Information("오버로드4 생성값 확인: {name}", createOverload4.TestName);
var createOverload5 = new CreatorOverload("NAME"); // string 오버로드
Log.Information("오버로드5 생성값 확인: {name}", createOverload5.TestName);


// 글로벌 consts 테스트 (생성자 없이 바로 사용)
Log.Information("글로벌 consts 값 확인: {name}, {description}", GlobalConsts.Name, GlobalConsts.Description);



// POCO 옵션 테스트 (생성자 필요. 일반 클래스와 같음, 값 셋팅 가능)
var pocoOptions1 = new PocoOptions1("NAME", "DESCRIPTION");
Log.Information("PocoOptions1 원본값 확인: {name}, {description}", pocoOptions1.Name, pocoOptions1.Description);
pocoOptions1.Description = "NEW Description";
Log.Information("PocoOptions1 신규값 확인: {name}, {description}", pocoOptions1.Name, pocoOptions1.Description);

var pocoOptions2 = new PocoOptions2("NAME", "DESCRIPTION");
Log.Information("PocoOptions2 원본값 확인: {name}, {description}", pocoOptions2.Name, pocoOptions2.Description);
pocoOptions2.Description = "NEW Description";
Log.Information("PocoOptions2 신규값 확인: {name}, {description}", pocoOptions2.Name, pocoOptions2.Description);


// Record 테스트 (생성자 필요, 값 변경 불가)
var info = new RecordInfo("NAME", "DESCRIPTION");
//var info2 = new RecordInfo // Record 는 객체 초기화 방식이 불가능 (프로퍼티 set 이 없기 때문에 객체 생성 후 값 셋팅하는 객체 초기화 방식이 안 됨)
//{
//    Name = "NAME",
//    Description = "DECRIPTION"
//};
Log.Information("Record 값 확인: {name}, {description}", info.Name, info.Description);
//info.Name = "NEW NAME"; // 불가능 (불변 속성때문에 변경 할 수 없음 -> set 이 없기 때문)
var updatedInfo = info with { Description = "NEW Description" };
Log.Information("UpdatedRecord 값 확인: {name}, {description}", updatedInfo.Name, updatedInfo.Description);


//////////////// playground 확인용 콘솔 로그 종료 ////////////////////////

Log.Information("=== 콘솔 프로그램 종료 ===");

Log.CloseAndFlush(); // Serilog 버퍼 플러시 및 종료 (마지막 로그 유실 방지)
