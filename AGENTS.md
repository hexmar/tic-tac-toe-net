# AGENTS.md — TicTacToeNet Monorepo

Guidelines to keep AI coding agents productive here. Keep this minimal and current.

## Layout & Solution Format
- Multi-root monorepo using the XML `.slnx` format (`TicTacToeNet.slnx`). Two SDK-style projects at `HttpRequestLogger/` and `TicTacToeNet.TelegramBot/`.
- Both target a single framework: `.NET 10.0` (`net10.0`), with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`. No multi-targeting anywhere.
- Folder names are `PascalCase`; file & class names are `PascalCase`; namespaces mirror the folder path from repo root (e.g. `TicTacToeNet.TelegramBot.Clients`). Infrastructure classes are `internal sealed` / `partial`. Style config lives in a single root-level `.editorconfig` (`root = true`) with `.NET Code` style/naming rules that apply to both projects; there are no per-project `.editorconfig`, `.analysissettings.json`, or `Directory.Build.*` files.

## Build, Run & Test
- No test projects exist yet — there is nothing to run for tests. If asked to add tests, create a separate xUnit project first (do not wire one into the `.slnx` without confirming).
- Build: `dotnet build TicTacToeNet.slnx`
- Run web host: `dotnet run --project HttpRequestLogger/HttpRequestLogger.csproj`
- Run bot worker: `dotnet run --project TicTacToeNet.TelegramBot/TicTacToeNet.TelegramBot.csproj` (also debuggable via the Azure Container Tools Docker target).

## Project Boundaries
- `HttpRequestLogger/` — Minimal-API web host (ASP.NET Core) that runs a logging middleware lambda on every request. No `Startup`, no OWIN, entry point is one static `Program.Main`.
- `TicTacToeNet.TelegramBot/` — Hosted background worker (`Microsoft.NET.Sdk.Worker`). All work happens in `[HostedService].ExecuteAsync`; there are no HTTP routes or `Map*` endpoints.

## Coding Conventions
- **Interfaces vs implementation**: name the impl as `TelegramXxx` for interface `IXxx` (e.g. `ITelegramBotApiClient` ↔ `TelegramBotApiClient`, `ITelegramUpdateQueue` ↔ `TelegramUpdateQueue`). Keep both `internal`.
- **Async contract is sacred on the queue**: `ITelegramUpdateQueue.QueueUpdateItemAsync` returns `ValueTask`; the other two return `ValueTask<string>`. Preserve the `ValueTask` contract (backed by a bounded `System.Threading.Channels.Channel<string>` in `FullMode.Wait`, single-writer, multi-reader). Do **not** silently change these to plain `Task`.
- **Typed client factory**: register with `AddHttpClient<Interface, Impl>` and read config from `IOptions<T>` inside the factory lambda (base address + timeout). DI uses only `Configure<>`, `AddSingleton<>`/factory singletons, and `AddHostedService<>`; no bare `AddTransient`/`AddScoped`.
- **Structured logging**: background services use a private `partial void LogWorkerData(...)` method decorated with `[LoggerMessage(...)]` to generate extension methods. Keep that partial-method signature in sync if the log shape changes.
- **Short type names via static using**: files reference DTOs by bare name (e.g. `new TextMessage() { ... }`) thanks to `using static TicTacToeNet.TelegramBot.Clients.TelegramBotApiTypes;`. Mirror this shorthand when adding new types in that namespace, and keep the import declaration where it belongs.
- **Configuration**: prefer typed options over raw key access — a config class exposes an internal static `ConfigurationKey` (section name) plus public properties bound via `Configure<T>` from `builder.Configuration.GetSection(...)`.

## Pitfalls & Gotchas ⚠️
- `HttpRequestLogger/Program.cs` has dead code: the real `/weatherforecast` route and `UseAuthorization()` live in an unused `Program.NotMain(...)`; `Main` only installs a logging middleware that always returns `"OK"`. Any change touching routes must move them into `Main`, not leave them in `NotMain`.
- Two distinct timeouts are hardcoded and should not be conflated: (1) the client factory sets `client.Timeout = TimeSpan.FromSeconds(60)` (no TODO comment), and (2) `TelegramBotApiClient.GetUpdateDataAsync` builds the polling URL `getUpdates?limit=1&timeout=30...` and carries the `// TODO: @hexmar Move timeout to configuration` comment. Only add a config property when intentionally removing one of these hardcoded values — don't remove it silently.
- Background services suppress `CA1812` (`#pragma warning disable CA1812`) because the runtime flags them as obj-lifetime warnings despite being long-lived singletons/hosted services. Keep these pragmas in place.
- `PrimesBackgroundService` computes primes up to 1,000,000 at startup and never consumes the result — it is placeholder/no-op content; don't treat unused computed values as a bug unless you're deliberately removing that behavior.
- TelegramBot uses `<AnalysisMode>All</AnalysisMode>` + `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, so its code must satisfy the strict analyzer set (HttpRequestLogger has none).
