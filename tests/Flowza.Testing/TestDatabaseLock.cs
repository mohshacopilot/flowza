using Microsoft.Data.SqlClient;

namespace Flowza.Testing;

/// <summary>
/// A SQL Server application lock on a test database, held for as long as this object lives. Test projects run in
/// parallel processes (<c>dotnet test --solution</c>), and several of them reset the same <c>flowza_test_*</c>
/// tables; whoever holds the lock has the database to itself.
/// </summary>
public sealed class TestDatabaseLock : IAsyncDisposable
{
    private const string Resource = "flowza-test-database";
    private readonly SqlConnection _connection;

    private TestDatabaseLock(SqlConnection connection) => _connection = connection;

    public static async Task<TestDatabaseLock> AcquireAsync(string database, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(TestSqlServer.ConnectionString(database));
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource, 'Exclusive', 'Session', @Timeout; SELECT @r;",
                connection);
            command.Parameters.AddWithValue("@Resource", Resource);
            command.Parameters.AddWithValue("@Timeout", (int)TimeSpan.FromMinutes(15).TotalMilliseconds);
            command.CommandTimeout = 0;
            var result = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
            if (result < 0)
            {
                throw new InvalidOperationException($"Could not lock the test database '{database}' (sp_getapplock returned {result}).");
            }

            return new TestDatabaseLock(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    // Closing the session releases a session-owned application lock.
    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
