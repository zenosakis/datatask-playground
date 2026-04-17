using System.Text;

namespace Playground.CSharp
{
    public class CreatorOverload
    {
        public byte[] TestName { get; set; }

        /// <summary>
        /// <para>생성자 오버로드 개념</para>
        /// </summary>
        public CreatorOverload()
        {
            TestName = Encoding.UTF8.GetBytes(string.Empty);
        }

        public CreatorOverload(string testName) // 생성자
        {
            TestName = Encoding.UTF8.GetBytes(testName);
        }

        public CreatorOverload(int testName) // 오버로드 방식1 (키워드 없이 자동)
        {
            TestName = Encoding.UTF8.GetBytes(testName.ToString());
        }

        public CreatorOverload(decimal testName): this(testName.ToString()) // 오버로드 방식2 (this 로 간단히 처리)
        {
            // 특정 로직이 없으면 이 부분은 생략
        }
        ////////////////////////////////////////////////////////////////////
    }
}
