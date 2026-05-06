using System.Text;
using System.Text.Json;
using Feature.Transfer.Interfaces;

namespace Feature.Transfer
{
    public class HttpTransferClient : IDataTransferClient
    {
        private readonly HttpClient _httpClient;

        public HttpTransferClient(HttpClient httpClient, HttpTransferOptions options)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(options.BaseAddress);
            _httpClient.Timeout = new TimeSpan(0, 0, options.TimeoutSeconds);
        }

        /// <summary>
        /// 단순 GET 요청을 보내고 응답 본문을 raw stream으로 반환한다.
        /// 파일 다운로드나 큰 payload처럼 클라이언트가 응답 내용을 해석하지 않아야 하는 경우에 사용한다.
        /// </summary>
        public Task<Stream> GetStreamAsync(string resource, CancellationToken ct = default)
        {
            return _httpClient.GetStreamAsync(resource, ct);
        }

        /// <summary>
        /// 단순 GET 요청을 보내고 응답 본문을 raw string으로 반환한다.
        /// 반환된 문자열을 어떻게 해석할지는 호출자가 직접 결정해야 하는 경우에 사용한다.
        /// </summary>
        public Task<string> GetStringAsync(string resource, CancellationToken ct = default)
        {
            return _httpClient.GetStringAsync(resource, ct);
        }

        /// <summary>
        /// JSON 기반 API 요청을 보내고 JSON 응답을 지정한 응답 타입으로 역직렬화한다.
        /// raw string/stream GET 헬퍼와 분리된, 구조화된 API 호출용 고수준 메서드다.
        /// </summary>
        public async Task<TResponse> SendAsync<TRequest, TResponse>(HttpMethod method, string resource, TRequest body, CancellationToken ct = default)
        {
            if (method == HttpMethod.Get)
            {
                throw new NotSupportedException(
                    "SendAsync 는 JSON body가 있는 API 호출용입니다. 단순 GET은 GetStringAsync 또는 GetStreamAsync를 사용하세요.");
            }

            var requestJson = JsonSerializer.Serialize(body);
            using var requestMessage = new HttpRequestMessage(method, resource);
            requestMessage.Content = new StringContent(requestJson,
                Encoding.UTF8,
                "application/json"
            );

            using var response = await _httpClient.SendAsync(requestMessage, ct);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<TResponse>(responseJson);

            if (result is null)
            {
                throw new InvalidOperationException("응답 JSON을 TResponse로 역직렬화 할 수 없습니다.");
            }

            return result;
        }
    }
}
