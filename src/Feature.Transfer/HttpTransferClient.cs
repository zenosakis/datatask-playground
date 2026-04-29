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

        public Task<Stream> GetStreamAsync(string resource, CancellationToken ct = default)
        {
            return _httpClient.GetStreamAsync(resource, ct);
        }

        public Task<string> GetStringAsync(string resource, CancellationToken ct = default)
        {
            return _httpClient.GetStringAsync(resource, ct);
        }

        public Task<TResponse> SendAsync<TRequest, TResponse>(HttpMethod method, string resource, TRequest body, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
