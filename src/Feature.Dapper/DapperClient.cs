using Feature.Dapper.Interfaces;

namespace Feature.Dapper
{
    public class DapperClient
    {
        private readonly IDapperAdapter _dbAdapter;
        private static readonly HashSet<string> _allowedTables = ["TBL_DIALEDLOG"];

        public DapperClient(IDapperAdapter dbAdapter)
        {
            _dbAdapter = dbAdapter;
        }

        public IEnumerable<dynamic> SelectTop10(string tableName)
        {
            //임시용 쿼리로, 실제 적용할 때는 이런 쿼리를 사용하지 않음 -> 만약 필요하다면, SQL 인젝션 방어기법 필요
            return !_allowedTables.Contains(tableName)
                ? throw new ArgumentException($"허용하지 않은 테이블: {tableName}")
                : _dbAdapter.Query($"SELECT top 10 * FROM {tableName}");
        }
    }
}
