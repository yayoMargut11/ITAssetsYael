using System.Data;
using Microsoft.Data.SqlClient;

namespace ITAssets.Api.Data;

public interface ISqlExecutor
{
    Task<List<T>> QueryAsync<T>(string procedure, Func<SqlDataReader, T> map, SqlParameter[]? parameters = null, CancellationToken ct = default);
    Task<int> ScalarIntAsync(string procedure, SqlParameter[]? parameters = null, CancellationToken ct = default);
    Task ExecuteAsync(string procedure, SqlParameter[]? parameters = null, CancellationToken ct = default);
}

/// <summary>
/// Único punto de acceso a SQL Server. Solo ejecuta Stored Procedures con parámetros
/// (nunca concatena SQL), por lo que no hay superficie de SQL injection.
/// </summary>
public sealed class SqlExecutor : ISqlExecutor
{
    private readonly string _connectionString;
    public SqlExecutor(string connectionString) => _connectionString = connectionString;

    public async Task<List<T>> QueryAsync<T>(string procedure, Func<SqlDataReader, T> map, SqlParameter[]? parameters = null, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = Build(conn, procedure, parameters);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<T>();
        while (await reader.ReadAsync(ct)) list.Add(map(reader));
        return list;
    }

    public async Task<int> ScalarIntAsync(string procedure, SqlParameter[]? parameters = null, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = Build(conn, procedure, parameters);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task ExecuteAsync(string procedure, SqlParameter[]? parameters = null, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = Build(conn, procedure, parameters);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static SqlCommand Build(SqlConnection conn, string procedure, SqlParameter[]? parameters)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = procedure;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        if (parameters is { Length: > 0 }) cmd.Parameters.AddRange(parameters);
        return cmd;
    }
}

/// <summary>Helpers para construir parámetros tipados y leer columnas por nombre.</summary>
public static class Sql
{
    public static SqlParameter P(string name, object? value) =>
        new SqlParameter { ParameterName = name, Value = value ?? DBNull.Value };

    public static SqlParameter Text(string name, string? value, int size) =>
        new SqlParameter(name, SqlDbType.NVarChar, size) { Value = (object?)value ?? DBNull.Value };

    public static SqlParameter Date(string name, DateTime? value) =>
        new SqlParameter(name, SqlDbType.Date) { Value = value.HasValue ? value.Value.Date : DBNull.Value };

    public static SqlParameter Binary8(string name, byte[]? value) =>
        new SqlParameter(name, SqlDbType.VarBinary, 8) { Value = (object?)value ?? DBNull.Value };

    /// <summary>Recorta y convierte vacío en null.</summary>
    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string Str(this SqlDataReader r, string c) => r.GetString(r.GetOrdinal(c));
    public static string? StrN(this SqlDataReader r, string c) { var i = r.GetOrdinal(c); return r.IsDBNull(i) ? null : r.GetString(i); }
    public static int Int(this SqlDataReader r, string c) => r.GetInt32(r.GetOrdinal(c));
    public static int? IntN(this SqlDataReader r, string c) { var i = r.GetOrdinal(c); return r.IsDBNull(i) ? null : r.GetInt32(i); }
    public static long Long(this SqlDataReader r, string c) => r.GetInt64(r.GetOrdinal(c));
    public static bool Bool(this SqlDataReader r, string c) => r.GetBoolean(r.GetOrdinal(c));
    public static DateTime Dt(this SqlDataReader r, string c) => r.GetDateTime(r.GetOrdinal(c));
    public static DateTime? DtN(this SqlDataReader r, string c) { var i = r.GetOrdinal(c); return r.IsDBNull(i) ? null : r.GetDateTime(i); }
}
