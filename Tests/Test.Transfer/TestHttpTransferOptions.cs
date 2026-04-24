using Feature.Transfer;

namespace Test.Transfer
{
    public class TestHttpTransferOptions
    {
        [Fact]
        public void HttpTransferOptions_GetBaseAddress_ReturnsBaseAddress()
        {
            var options = new HttpTransferOptions();
            options.BaseAddress = "localhost:21005";

            Assert.Equal("localhost:21005", options.BaseAddress);
        }

        [Fact]
        public void HttpTransferOptions_GetTimeoutSeconds_ReturnsTimeoutSeconds()
        {
            var options = new HttpTransferOptions();
            options.TimeoutSeconds = 60;

            Assert.Equal(60, options.TimeoutSeconds);
        }

        [Fact]
        public void HttpTransferOptions_GettTimeoutSecond_WhenDefaultValue_ReturnsTimeoutSeconds_30()
        {
            var options = new HttpTransferOptions();

            Assert.Equal(30, options.TimeoutSeconds);
        }
    }
}
