using System.ComponentModel;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using ModelContextProtocol.Server;

namespace Firefly.Development.SqlMcp;

[McpServerToolType]
public class SqlTools
{
    private static readonly TSql160Parser Parser = new(false);
    private const string Server = "UAT-RAIDDB";

    private const int TimeoutSeconds = 10;

    private static string ConnectionString(string database) =>
        $"Server={Server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout={TimeoutSeconds};";

    private static void ValidateSelectOnly(string sql)
    {
        var reader = new StringReader(sql);
        var fragment = Parser.Parse(reader, out var errors);

        if (errors.Count > 0)
        {
            var msgs = string.Join("; ", errors.Select(e => e.Message));
            throw new ArgumentException($"SQL parse error: {msgs}");
        }

        if (fragment is not TSqlScript script || script.Batches.Count == 0)
            throw new ArgumentException("No SQL statements found.");

        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                if (statement is not SelectStatement select)
                    throw new ArgumentException(
                        $"Only SELECT statements are allowed. Found: {statement.GetType().Name}");

                if (select.Into != null)
                    throw new ArgumentException("SELECT INTO is not allowed.");
            }
        }
    }

    private static string ToMarkdownTable(SqlDataReader reader)
    {
        var sb = new StringBuilder();
        var cols = reader.FieldCount;

        // Header
        sb.Append('|');
        for (var i = 0; i < cols; i++)
            sb.Append($" {reader.GetName(i)} |");
        sb.AppendLine();

        // Separator
        sb.Append('|');
        for (var i = 0; i < cols; i++)
            sb.Append(" --- |");
        sb.AppendLine();

        // Rows
        var rowCount = 0;
        while (reader.Read())
        {
            rowCount++;
            if (rowCount > 500)
            {
                sb.AppendLine($"\n*... results truncated at 500 rows*");
                break;
            }

            sb.Append('|');
            for (var i = 0; i < cols; i++)
            {
                var val = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                sb.Append($" {val} |");
            }
            sb.AppendLine();
        }

        if (rowCount == 0)
            sb.AppendLine("*(no rows returned)*");

        return sb.ToString();
    }

    [McpServerTool(Name = "query"), Description("Execute a read-only SQL query against UAT-RAIDDB. Only SELECT statements are allowed.")]
    public static async Task<string> Query(
        [Description("The SQL query to execute. Must be a SELECT statement.")] string sql,
        [Description("Database name (default: Research)")] string database = "Research")
    {
        try
        {
            ValidateSelectOnly(sql);
        }
        catch (ArgumentException ex)
        {
            return $"**Rejected:** {ex.Message}";
        }

        await using var conn = new SqlConnection(ConnectionString(database));
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = TimeoutSeconds };
        await using var reader = await cmd.ExecuteReaderAsync();
        return ToMarkdownTable(reader);
    }

    [McpServerTool(Name = "list_databases"), Description("List all databases on UAT-RAIDDB that you have access to.")]
    public static async Task<string> ListDatabases()
    {
        const string sql = "SELECT name FROM sys.databases ORDER BY name";

        await using var conn = new SqlConnection(ConnectionString("master"));
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = TimeoutSeconds };
        await using var reader = await cmd.ExecuteReaderAsync();
        return ToMarkdownTable(reader);
    }

    [McpServerTool(Name = "list_tables"), Description("List all tables and views in a database on UAT-RAIDDB.")]
    public static async Task<string> ListTables(
        [Description("Database name (default: Research)")] string database = "Research",
        [Description("Optional filter: schema or table name contains this string")] string? filter = null)
    {
        var sql = "SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES";
        if (!string.IsNullOrWhiteSpace(filter))
            sql += $" WHERE TABLE_SCHEMA LIKE '%{filter.Replace("'", "''")}%' OR TABLE_NAME LIKE '%{filter.Replace("'", "''")}%'";
        sql += " ORDER BY TABLE_SCHEMA, TABLE_NAME";

        await using var conn = new SqlConnection(ConnectionString(database));
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = TimeoutSeconds };
        await using var reader = await cmd.ExecuteReaderAsync();
        return ToMarkdownTable(reader);
    }

    [McpServerTool(Name = "describe_table"), Description("Describe the columns of a table or view on UAT-RAIDDB.")]
    public static async Task<string> DescribeTable(
        [Description("Table or view name (e.g. dbo.vwBook or just vwBook)")] string table,
        [Description("Database name (default: Research)")] string database = "Research")
    {
        string schema;
        string tableName;

        if (table.Contains('.'))
        {
            var parts = table.Split('.', 2);
            schema = parts[0];
            tableName = parts[1];
        }
        else
        {
            schema = "%";
            tableName = table;
        }

        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, COLUMN_DEFAULT
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA LIKE @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """;

        await using var conn = new SqlConnection(ConnectionString(database));
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = TimeoutSeconds };
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", tableName);
        await using var reader = await cmd.ExecuteReaderAsync();
        return ToMarkdownTable(reader);
    }
}
