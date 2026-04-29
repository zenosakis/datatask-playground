using System.Data;
using Dapper;
using Feature.Dapper.Interfaces;

namespace Feature.Dapper;

public class DapperAdapter(IDbConnection connection): IDapperAdapter
{
    public IEnumerable<dynamic> Query(string sql)
    {
        return connection.Query(sql);
    }
}
