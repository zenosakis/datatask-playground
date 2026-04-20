using Dapper;
using System.Data;
using Microsoft.Data.SqlClient; // SqlConnection 사용 위함(원래 System.Data.SqlClient 였으나 2019년 변경됨 => Nuget 패키지 "Microsoft.Data.SqlClient" 필요

namespace Feature.Dapper
{
    public class DapperTest
    {
        private readonly IDbConnection _dbConnection;

        public DapperTest(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public IEnumerable<dynamic> SelectTest(string tableName)
        {
            //임시용 쿼리로, 실제 적용할 때는 이런 쿼리를 사용하지 않음 -> 만약 필요하다면, SQL 인젝션 방어기법 필요
            return _dbConnection.Query($"select top 10 * from {tableName}");
        }
    }
}
