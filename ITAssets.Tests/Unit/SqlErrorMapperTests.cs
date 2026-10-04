using ITAssets.Api.Middleware;
using Xunit;

namespace ITAssets.Tests.Unit;

public class SqlErrorMapperTests
{
    [Theory]
    [InlineData(50001, 404, "ASSET_NOT_FOUND")]
    [InlineData(50002, 409, "ASSET_NOT_AVAILABLE")]
    [InlineData(50003, 404, "EMPLOYEE_NOT_FOUND")]
    [InlineData(50004, 422, "EMPLOYEE_INACTIVE")]
    [InlineData(50030, 409, "NO_ACTIVE_ASSIGNMENT")]
    [InlineData(2601, 409, "CONFLICT")]
    [InlineData(2627, 409, "CONFLICT")]
    [InlineData(1205, 409, "CONCURRENCY_CONFLICT")]
    public void Maps_known_sql_errors_to_controlled_http_errors(int number, int status, string code)
    {
        Assert.True(SqlErrorMapper.TryMap(number, out var info));
        Assert.Equal(status, info.Status);
        Assert.Equal(code, info.Code);
    }

    [Theory]
    [InlineData(208)]   // invalid object name: nunca debe exponerse
    [InlineData(547)]   // violación de FK
    [InlineData(0)]
    public void Unknown_sql_errors_are_not_mapped(int number) =>
        Assert.False(SqlErrorMapper.TryMap(number, out _));

    [Fact]
    public void Mapped_messages_never_contain_sql_internals()
    {
        foreach (var n in new[] { 50001, 50002, 50004, 2601, 2627, 1205 })
        {
            SqlErrorMapper.TryMap(n, out var info);
            Assert.DoesNotContain("SQL", info.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("dbo.", info.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
