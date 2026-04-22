namespace Feature.Transfer.Interfaces
{
    public interface IFileTransferClient
    {
        Task ConnectAsync(CancellationToken ct = default);
        Task DisconnectAsync();
        Task UploadAsync(string localPath, string remotePath, CancellationToken ct = default);
        Task DownloadAsync(string remotePath, string localPath, CancellationToken ct = default);
        Task<bool> ExistsAsync(string remotePath, CancellationToken ct = default);
        Task DeleteAsync(string remotePath, CancellationToken ct = default);
        Task<IReadOnlyList<string>> ListAsync(string remoteDir, CancellationToken ct = default);
        Task RenameAsync(string remotePath, string newRemotePath, CancellationToken ct = default);
    }
}
