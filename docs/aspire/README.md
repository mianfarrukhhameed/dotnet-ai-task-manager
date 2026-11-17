# Task Manager Aspire AppHost

Orchestrates local development for Task Manager:

- PostgreSQL with **pgvector** (`pgvector/pgvector:pg16`)
- **YARP gateway** on **http://localhost:5000** / **https://localhost:5001** (WebApp `Api:Url` must match; Blazor WASM cannot use Aspire env injection). Requires Aspire **≥ 13.1.1** so `WithHostHttpsPort` sticks (13.1.0 randomized HTTPS).
- Microservices (todo / aijobs / knowledge / notifications / webapi voice / aiworker) behind the gateway
- **WebApp** (Blazor WASM) on port 5002 (HTTPS)
- Optional **pgAdmin** on host port 5050
- Local **Whisper / Speaches** STT sidecar (`ghcr.io/speaches-ai/speaches:latest-cpu`) for hold-to-talk voice commands

## Run

```bash
dotnet run --project src/AppHost/AppHost.csproj
```

The Aspire dashboard opens automatically. WebApi receives `ConnectionStrings__MainDb` from the `MainDb` database resource and `Ai__SpeechToText__Endpoint` from the Whisper container.

## Dashboard URLs (resource view)

| Resource | Link label | URL | Notes |
|----------|------------|-----|--------|
| **gateway** | Gateway | https://localhost:5001 | Public API edge |
| **gateway** | Swagger | https://localhost:5001/swagger | Aggregated OpenAPI UI — **use this** |
| **webapi** | Voice hub | https://localhost:5142 | Residual STT hub only — **not** API docs |
| **webapp** | (HTTPS) | https://localhost:5002 | Blazor UI |

HTTP gateway (`:5000`) and WebApi HTTP are details-only so the grid stays focused on HTTPS + Swagger.

## Swagger / OpenAPI (gateway aggregate)

Approach: **generate OpenAPI per API host**; **browse via one Swagger UI on the gateway**.

1. **TodoService**, **KnowledgeService**, **AiJobsService** each expose `/swagger/{ApiVersion}/swagger.json` in Development (`ApiHost.Shared` → `AddDevelopmentSwagger` / `UseDevelopmentSwagger`).
2. YARP proxies specs:
   - `/openapi/todo/{**}` → Todo `/swagger/{**}`
   - `/openapi/knowledge/{**}` → Knowledge `/swagger/{**}`
   - `/openapi/aijobs/{**}` → AiJobs `/swagger/{**}`
3. Residual **WebApi** hosts Swagger UI only (catch-all). UI loads the three docs above. Try-it-out hits `:5001` paths (same as WebApp/MCP).

Do **not** use `https://localhost:5142/swagger` for API exploration — that port has no controllers after the microservice split, and `/openapi/…` routes exist only on the gateway.

Shared helper: `src/ApiHost.Shared/Extensions/SwaggerHostExtensions.cs`. Wire new HTTP API hosts the same way and add a matching `/openapi/{name}/…` YARP route + SwaggerEndpoint on WebApi.

## Notes

- Set AI keys via user-secrets on an AI host (e.g. TodoService) or process environment (same as non-Aspire runs).
- MCP Server is not started by AppHost (stdio / Claude Desktop process).
- Prefer this over `docker compose` for day-to-day API + UI work; avoid running both Postgres stacks unless you know you need two databases.
- First Whisper start downloads the tiny model into a named Docker volume (`taskmanager-aspire-whisper-cache`); later Aspire restarts reuse that cache and should log `already installed — skipping download`.
- Without AppHost, set `Ai:SpeechToText:Endpoint` to your local Speaches URL (e.g. `http://localhost:8000`).
- Voice commands need `Ai:Features:EnableVoiceTranscription` (and `EnableFunctionCalling` to map speech to tools).
- STT HttpClient clears Aspire’s default 30s resilience pipeline (`RemoveAllResilienceHandlers`) and uses `Ai:SpeechToText:WarmupTimeoutSeconds` (default 600s) so first-run downloads are not canceled.
