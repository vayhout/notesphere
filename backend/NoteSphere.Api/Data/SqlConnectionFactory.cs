using Microsoft.Data.SqlClient;

namespace NoteSphere.Api.Data;

public sealed class SqlConnectionFactory
{
    private readonly IConfiguration _config;

    public SqlConnectionFactory(IConfiguration config)
    {
        _config = config;
    }

    public SqlConnection Create()
    {
        var cs = _config.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'SqlServer' not configured.");

        return new SqlConnection(cs);
    }
}
