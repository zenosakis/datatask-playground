using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CSharp
{
    /// <summary>
    /// <para>단순 const 모음 클래스 -> 따로 생성자 없이 <c>GlobalConsts.Name</c> 식으로 사용</para>
    /// </summary>
    public static class GlobalConsts
    {
        public const string Name = "name";
        public const string Description = "description";
    }
    ///////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// <para>POCO 방식 클래스 (Plain Old CLR Object)</para>
    /// <para>외부 설정 파일 등에서 값 셋팅하여 다른데에서 사용하는 경우</para>
    /// <para>*** 사실상 일반 클래스랑 다를게 없음 (생성, 값 셋팅, 사용, 해제=GC에서 처리) ***</para>
    /// <para>PocoOptions1 은 기존 생성자 방식의 코드 (프로퍼티 정의 + 생성자 지정)</para>
    /// <para>PocoOptions2 는 기본 생성자(Primary Constructor) 방식의 코드 (C# 12 = .NET 8 이상 지원)</para>
    /// </summary>
    public class PocoOptions1 // 기존 생성자를 직접 구현하는 방식
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public PocoOptions1(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    public class PocoOptions2(string name, string description) // 기본 생성자 방식 (C# 12 = .NET 8 이상 지원)
    {
        public string Name { get; set; } = name;
        public string Description { get; set; } = description;
    }
    ///////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// <para>간단한 프로퍼티만 가진 Record 방식 (C# 9 = .NET 5 이상 지원)</para>
    /// <para>생성자로 전달받는 인자는 프로퍼티랑 동일함 (따라서, 명명규칙이 파스칼케이스를 따름 = 첫글자 대문자)</para>
    /// <para>"불변" 이라는 특성 때문에 set 이 없고 init 이 자동처리됨 ({ get; init; } 형식)</para>
    /// <para>사용할 때 생성 후 바로 사용 가능 </para>
    /// <para><code>
    /// var info = new RecordInfo("name", "description");
    /// info.name; 형식으로 사용
    /// </code>
    /// </para>
    /// <para>값을 바꾼 객체를 새로 만들고 싶을 때에도 <c>var updatedInfo = info with { Name = "NewName" };</c> 식으로 생성하면 값이 1개만 바뀐채로 생성됨</para>
    /// </summary>
    /// <param name="Name"></param>
    /// <param name="Description"></param>
    public record RecordInfo(string Name, string Description);

}
