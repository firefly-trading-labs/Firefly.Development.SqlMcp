# Firefly.Development.SqlMcp

A read-only SQL MCP server for querying UAT-RAIDDB from Claude Code.

## What it does

- Connects to **UAT-RAIDDB only** (hard-coded — can't accidentally hit prod)
- Uses **Windows Auth** (runs as you, your permissions)
- Validates all SQL via **ScriptDom T-SQL AST parser** — only `SELECT` statements allowed
- Exposes 4 MCP tools: `query`, `list_databases`, `list_tables`, `describe_table`

## Setup

### Option 1: Run from source

```bash
claude mcp add sql-uat -- dotnet run --project C:\path\to\src\Firefly.Development.SqlMcp\Firefly.Development.SqlMcp.csproj
```

### Option 2: Install as dotnet tool

> **Note:** This approach is not yet tested — use Option 1 for now.

```bash
dotnet pack src/Firefly.Development.SqlMcp
dotnet tool install --global --add-source src/Firefly.Development.SqlMcp/nupkg Firefly.Development.SqlMcp
claude mcp add sql-uat -- Firefly.Development.SqlMcp
```

## Tools

| Tool | Description |
|------|-------------|
| `query` | Execute a read-only SELECT query against any database on UAT-RAIDDB |
| `list_databases` | List all databases you have access to |
| `list_tables` | List tables/views in a database (with optional filter) |
| `describe_table` | Show column definitions for a table or view |

## Safety

The ScriptDom AST parser catches everything string matching can't:

- `SELECT 1; DROP TABLE Users` — **rejected** (second statement is DropTableStatement)
- `DELETE FROM ...` — **rejected** (DeleteStatement)
- `SELECT * INTO #temp FROM ...` — **rejected** (SELECT INTO)
- `EXEC sp_something` — **rejected** (ExecuteStatement)
- `INSERT INTO ... SELECT ...` — **rejected** (InsertStatement)
- `SELECT * FROM vwBook` — **allowed**
- `WITH cte AS (...) SELECT ...` — **allowed**
