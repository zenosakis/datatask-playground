namespace Feature.Transfer.Interfaces
{
    public interface IDataTransferClient
    {
        Task<TResponse> SendAsync<TRequest, TResponse>(HttpMethod method, string resource, TRequest body, CancellationToken ct = default);
        Task<Stream> GetStreamAsync(string resource, CancellationToken ct = default);
        Task<string> GetStringAsync(string resource, CancellationToken ct = default);
    }
}
