namespace Feature.Dapper.Tests;

public class DbOptionsTests
{
    [Fact]
    public void DbOptions_GetConnectionString_ReturnsConnectionString()
    {
        var options = new DbOptions
        {
            Ip = "localhost",
            Port = "1433",
            Database = "Test",
            User = "test",
            Password = "password"
        };

        const string exceptedConnectionString = "Server=localhost,1433;Database=Test;"
                                                +"User Id=test;Password=password;TrustServerCertificate=True;";
        var connectionString = options.ToConnectionString();
        Assert.Equal(exceptedConnectionString, connectionString);
    }
}
