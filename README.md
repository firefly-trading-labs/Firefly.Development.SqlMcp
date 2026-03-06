# Firefly.Development.SqlMcp

A read-only SQL MCP server for querying UAT-RAIDDB from Claude Code.

## What it does

- Connects to **UAT-RAIDDB only** (hard-coded — can't accidentally hit prod)
- Uses **Windows Auth** (runs as you, your permissions)
- Validates all SQL via **ScriptDom T-SQL AST parser** — only `SELECT` statements allowed
- Exposes 4 MCP tools: `query`, `list_databases`, `list_tables`, `describe_table`

## Setup

The server runs as a local HTTP server on `localhost:3001`. You need to start it before Claude Code can connect.

### 1. Start the server

```bash
dotnet run --project src/Firefly.Development.SqlMcp
```

The server will listen on `http://localhost:3001`. Keep this terminal open.

### 2. Register in Claude Code

Add to your `~/.claude.json` under `mcpServers`:

```json
"sql-uat": {
  "type": "http",
  "url": "http://localhost:3001"
}
```

Or via CLI:

```bash
claude mcp add -s user -t http sql-uat http://localhost:3001
```

Then restart Claude Code or run `/mcp` to connect.

## Tools

| Tool | Description |
|------|-------------|
| `query` | Execute a read-only SELECT query against any database on UAT-RAIDDB |
| `list_databases` | List all databases you have access to |
| `list_tables` | List tables/views in a database (with optional filter) |
| `describe_table` | Show column definitions for a table or view |

## Why build a custom MCP server?

No official SQL Server MCP server exists that meets all our requirements. Here's why alternatives were rejected:

| Approach | Why not |
|----------|---------|
| `sqlcmd` via Bash | Accepts any SQL — no way to enforce SELECT-only at the Claude Code permission level |
| Community MCP servers | All random GitHub projects, not from trusted companies. Inconsistent Windows Auth support |
| Microsoft DAB (Data API Builder) | Requires explicit entity registration per table — maintenance burden. Complex RBAC config |
| npx-based SQL packages | Even less trustworthy than community GitHub projects |

This custom server is ~100 lines of code, zero config, native Windows Auth, and uses Microsoft's official ScriptDom AST parser for robust SELECT-only enforcement.

## Safety

The ScriptDom AST parser catches everything string matching can't:

- `SELECT 1; DROP TABLE Users` — **rejected** (second statement is DropTableStatement)
- `DELETE FROM ...` — **rejected** (DeleteStatement)
- `SELECT * INTO #temp FROM ...` — **rejected** (SELECT INTO)
- `EXEC sp_something` — **rejected** (ExecuteStatement)
- `INSERT INTO ... SELECT ...` — **rejected** (InsertStatement)
- `SELECT * FROM vwBook` — **allowed**
- `WITH cte AS (...) SELECT ...` — **allowed**
