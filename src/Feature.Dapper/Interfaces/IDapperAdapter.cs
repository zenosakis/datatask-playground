namespace Feature.Dapper.Interfaces;

public interface IDapperAdapter
{
    public IEnumerable<dynamic> Query(string sql);
}
