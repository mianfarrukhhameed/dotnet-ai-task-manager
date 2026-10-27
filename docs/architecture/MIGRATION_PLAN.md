# Event-Driven Microservices Migration Plan

> Demo / portfolio evolution of the Task Manager monolith.  
> **Rule:** design all phases here first; implement only via small PRs, one phase (or phase slice) at a time.  
> **Do not** extract microservices before Phases 1–3 land in the monolith.

**Status:** Phase 2 done (`arch/phase-2-outbox-events`); Phase 1 done on `arch/phase-1-cqrs-db-split`  
**Solution:** `src/TaskManager.sln`  
**Current shape:** Logical CQRS (MediatR) + dual EF contexts + MassTransit EF outbox → RabbitMQ; classify/embed consumers in WebApi

---

## Goals

1. Evolve toward **event-driven microservices** without a big-bang rewrite.
2. Keep API contracts, Auth0, AI guardrails, and WebApp UX stable where possible.
3. Prefer **working demos** over production HA; still teach real consistency / outbox / replica patterns.

## Non-goals (for now)

- Multi-region active-active
- Full saga orchestration product
- Splitting WebApp into micro-frontends
- Changing public ID model (`ExternalId` stays)

---

## Phase overview

| Phase | Name | Outcome | Implement when |
|-------|------|---------|----------------|
| **1** | Read/Write DB separation | Dual EF contexts; write vs read routing; replica optional via fallback | First implementation PRs |
| **2** | Transactional outbox + domain events | MassTransit + RabbitMQ; EF outbox on write DB; classify/embed consumers | After Phase 1 |
| **3** | Projections / read-model sync | **Replica enough** (same schema); custom projections deferred / revise later | After Phase 2 |
| **4** | Bounded-context extraction | YARP gateway + microservices (DB/workers); optional thin Web BFF; Notification service | After Phase 3 |
| **5** | Cross-service choreography | Choreography by default; **optional sprint-approve MassTransit saga** demo | Alongside / after Phase 4 |

Each phase below has: **decisions**, **design notes**, **PR slices**, **success criteria**, **open questions**.

---

## Phase 1 — Read/Write database separation

### Intent

Split **physical** read vs write access inside the monolith while keeping one schema and one product API.

```text
Commands / jobs / authz-before-write / profile lookup
  →  WriteEfContext  →  Postgres PRIMARY (MainDb)

Queries / search / RAG retrieval
  →  ReadEfContext   →  Postgres REPLICA (MainDbRead)
                      or PRIMARY when MainDbRead unset (fallback)

PRIMARY ── streaming replication ──► REPLICA   (recipe TBD; not required for first PRs)
```

### Locked decisions

| # | Decision |
|---|----------|
| D1 | Two EF contexts: `WriteEfContext` (tracking, migrations) and `ReadEfContext` (no-tracking; `SaveChanges` throws). |
| D2 | Connection strings: `MainDb` (write), `MainDbRead` (read). If `MainDbRead` unset → **fallback to `MainDb`**. |
| D3 | Migrations / `ApplyMigrations` / design-time factory → **write only**. |
| D4 | Create & update todo responses: map `TodoTaskDto` from **in-memory** entity after `SaveChanges` — **no post-mutate `Get`**. |
| D5 | Loads that **gate a write** (ownership / status machine) use **write** DB. |
| D6 | Target infra: Postgres **primary + streaming replica** (Option A). **Replica recipe TBD**; ship code with **fallback first** (`MainDbRead` → `MainDb`). |
| D7 | **AI batch + sprint optimizer jobs: all I/O on write DB** (queries, commands, background workers). |
| D8 | Controllers stay thin; create/update still return DTO (mapped in-memory). |
| D9 | Do **not** introduce denormalized read schemas or an event bus in this phase. |
| D10 | **`CurrentUserService` / `UserProfile` lookup → write DB** (authz-sensitive; low QPS). |
| D11 | **Hybrid search stays sequential** (vector then lexical). One scoped `ReadEfContext` is not thread-safe; do not `Task.WhenAll` on the same context. Parallelism (e.g. `IDbContextFactory`) is out of scope for Phase 1. |

### Routing matrix

| Path | DB |
|------|-----|
| Todo/Knowledge/Sprint **commands** (mutate) | Write |
| Authz / status load before mutate | Write |
| Create/Update response mapping | In-memory (after write) |
| `UserProfile` / `CurrentUserService` lookup | Write |
| `Get*` todo/sprint/knowledge **queries** | Read |
| Semantic / lexical search, RAG retrieval | Read (sequential hybrid) |
| Embedding / knowledge **ingest** processors | Write |
| AI batch + sprint optimizer (all) | Write |

### Authz-on-write (why)

Commands often: `Get` → `TodoAccessGuard` / ownership check → `Update`.  
If `Get` hits a lagging replica: false 404/deny, or stale status transitions (jobs).  
**Rule:** any load that gates a write uses primary.

### Profile lookup (why write)

`CurrentUserService` resolves `UserProfile` by email via `IRepositoryFactory` on every authenticated request. Today this is lookup-only (seeded profiles; missing → empty profile → forbid). The lookup gates `RequireCurrentUserId` / `HasAdminProfile` for almost all commands. Keep it on **write** so admin flags and profile presence are strongly consistent. If a future create-on-first-login path is added, insert + immediate read stay on write.

### Hybrid search (why sequential)

EF Core `DbContext` is not thread-safe. Hybrid paths today run vector then lexical deliberately (`SemanticSearchPipeline`, `KnowledgeSemanticSearchPipeline`). Splitting write/read contexts does **not** unlock parallelism by itself: one request still has one `ReadEfContext`. Keep sequential in Phase 1.

### Infrastructure (Aspire)

1. Keep primary: `AddPostgres` + `AddDatabase("MainDb")` (existing).
2. **First PRs:** only `MainDb`; set `MainDbRead` empty → fallback to `MainDb` so dual-context routing is testable on one node.
3. **Later:** document and wire a concrete primary + streaming replica recipe (`ConnectionStrings__MainDbRead`). Recipe (custom `pgvector` replica vs Bitnami-style, ports, slots) remains **TBD**.
4. Migrations **only** on primary. Replica gets schema via replication when present, never via EF migrate.
5. Both connections need **pgvector** when a real replica is used (search/RAG on read).

### PR slices (implement later)

| PR | Scope |
|----|--------|
| 1.1 | Config `MainDbRead` + fallback; note in this doc / overview |
| 1.2 | `WriteEfContext` / `ReadEfContext` + dual DI (both → primary via fallback) |
| 1.3 | Create/Update: in-memory DTO mapping; remove post-mutate `Get`s |
| 1.4 | Split todo/knowledge/search repos; route queries → read, commands → write; profile → write |
| 1.5 | Jobs remain on write; health checks for write (+ read when configured) |
| 1.6 | Tests + update `SOLUTION_OVERVIEW.md`; this phase status → Done |
| 1.7 | *(Optional, later)* Aspire/Compose primary + replica recipe when TBD is resolved |

### Success criteria

- [x] Commands never use `ReadEfContext` (mutations / authz loads on `WriteEfContext`)
- [x] List/search/RAG use `ReadEfContext` (falls back to primary when `MainDbRead` unset)
- [x] Jobs never use read DB (batch / sprint optimizer repos on write)
- [x] Profile lookup uses write DB
- [x] Create/Update return DTOs without re-query
- [x] Hybrid search remains sequential
- [x] Migrations apply only to primary (`WriteEfContext`)
- [x] App works with `MainDbRead` unset (fallback via `ResolveReadConnectionString`)

### Open questions (Phase 1)

- [ ] Exact Aspire/Compose **replica recipe** (container flags / image) for local demo — **deferred**; fallback-first unblocks coding

---

## Phase 2 — Transactional outbox + domain events

### Intent

Make “state changed” **reliable** on the write side so later projections and services can subscribe without dual-write bugs. Drive classify/embed from durable messages instead of in-memory channels.

```text
Command handler (WriteEfContext, one transaction)
  ├─ aggregate change
  └─ IPublishEndpoint.Publish(TodoCreated / TodoUpdated / …)
       → MassTransit EF transactional outbox rows
            ↓ (after commit)
       MassTransit outbox delivery → RabbitMQ
            ↓
       MassTransit consumers (idempotent)
         → ClassifyTodoConsumer
         → EmbedTodoConsumer
```

### Locked decisions

| # | Decision |
|---|----------|
| D20 | **Transport:** RabbitMQ (Aspire-hosted for local). |
| D21 | **Messaging library:** MassTransit. |
| D22 | **Outbox:** MassTransit **EF Core transactional outbox** on `WriteEfContext`. Handlers **explicitly** `Publish` in the same unit of work as the business write (no MediatR-only outbox magic; no publish-to-broker without outbox). |
| D23 | **Classify / embed:** MassTransit **consumers**, **idempotent** (safe under at-least-once / redelivery). Respect `Ai:Features` inside consumers. |
| D24 | **Retire** in-memory `IClassificationQueue` / `IEmbeddingQueue` (`Channel<Guid>`) once consumers own the work. |
| D25 | MediatR remains for HTTP commands/queries; MassTransit owns async integration. |
| D26 | Jobs (batch / sprint optimizer) stay write-local for Phase 2 (no requirement to bus them yet). |
| D27 | **Message contracts:** new assembly (e.g. `src/Contracts` / `Messaging.Contracts`). Not ViewModel or ServiceLayer. Initial events: `TodoCreated`, `TodoUpdated`, `TodoDeleted` (payload uses `ExternalId`). |
| D28 | **Consumers host:** **WebApi in-process** for Phase 2. **Extract dedicated worker in Phase 4** (same contracts; move classify/embed consumers out of WebApi). |
| D29 | **Update publish rule:** publish classify/embed side effects **only when text changes** (title and/or description). Due-date / priority-only updates do not enqueue AI refresh. Create still publishes as today (classify + embed when features enabled). |

### Planned direction (implementation detail)

| Topic | Direction | Notes |
|-------|-----------|-------|
| Contracts project | New `src/Contracts` (name flexible) | Referenced by WebApi / ServiceLayer publishers and consumers; later by Phase 4 worker |
| Event catalog (initial) | `TodoCreated`, `TodoUpdated`, `TodoDeleted` | `TodoUpdated` may include flags or consumers compare text; publish AI work only if text changed |
| Who publishes | Command handlers via `IPublishEndpoint` | Same scoped `WriteEfContext` + outbox |
| Consumers (Phase 2) | Classify + Embed **in WebApi** | Replace background channel readers; upsert / re-run safe |
| Consumers (Phase 4) | Same types hosted in worker project | Drop consumer registration from WebApi when extracted |
| Later events (optional in Phase 2) | Embedding ready, knowledge ingested, sprint created | Can follow after Todo path works |
| Failure / poison | MassTransit retry + skipped/fault queues | Align with OTel where useful |
| Aspire | `AddRabbitMQ` + WebApi `AddMassTransit` + EF outbox on write context | Phase 4 adds worker project reference |

### Design checklist (fill before / during PRs)

- [x] Add Contracts project to `TaskManager.sln`
- [x] MassTransit EF outbox migration / schema on write DB
- [ ] Correlation / causation IDs for OTel
- [x] Consumer idempotency rules (classify re-apply; embedding upsert)
- [x] Update handler: detect title/description change before publishing AI-related messages
- [x] Remove `ClassificationQueue` / `EmbeddingQueue` registration after cutover
- [x] Never bus-publish outside transactional outbox on the request path

### PR slices (draft)

| PR | Scope |
|----|--------|
| 2.1 | Aspire RabbitMQ + MassTransit DI; EF transactional outbox on `WriteEfContext` |
| 2.2 | New Contracts assembly + Todo messages; publish from create/update/delete (update: text-change gate) |
| 2.3 | Idempotent classify/embed consumers **hosted in WebApi**; honor `Ai:Features` |
| 2.4 | Retire in-memory classification/embedding queues + old background channel loops |
| 2.5 | Tests (outbox commit/rollback; consumer idempotency; text-change gate) + docs |

### Success criteria

- [x] Todo write + outbox row commit atomically; rollback publishes nothing
- [x] Process kill after commit still results in RabbitMQ delivery via outbox *(MassTransit bus outbox delivery service)*
- [x] Classify/embed run from MassTransit consumers in WebApi, not `Channel` queues
- [x] Re-delivery does not corrupt AI metadata / embeddings *(processor early-outs / upsert)*
- [x] Create/update HTTP path does not call broker without outbox
- [x] Priority/due-date-only update does not trigger classify/embed
- [x] Contracts live in a dedicated assembly (not ViewModel)

### Open questions (Phase 2)

_None — Phase 2 decisions locked. Remaining naming polish (exact csproj name) can be chosen at PR 2.2._

---

## Phase 3 — Projections / read-model sync

### Intent

Decide how read models stay fresh after Phase 1 (read connection) and Phase 2 (outbox events). For this portfolio path, **streaming replica of the same schema is enough**; custom event-sourced projections are **deferred** (see **Decisions to revise later**).

```text
PRIMARY (write)  ──streaming replication──►  REPLICA (read)
     same tables                                 same tables
     (TodoTask, embeddings, knowledge, …)

MassTransit events still drive classify/embed (Phase 2), not list/search projections.
```

### Locked decisions

| # | Decision |
|---|----------|
| D30 | **Streaming replica is enough** for Phase 3. Read queries (list, get, semantic/lexical search, RAG retrieval) keep using the **same schema** via `ReadEfContext` / `MainDbRead`. |
| D31 | **No custom projection tables** in Phase 3 (no denormalized Todo list/search docs fed by outbox). |
| D32 | **Revise later:** introduce first custom projector when needed for demos or Phase 4 service extract (independent read DB shaped by events). Tracked under **Decisions to revise later**. |
| D33 | Create/update in-memory DTOs (Phase 1) + acceptable list/search replica lag cover UX without projectors. |
| D34 | Embeddings / knowledge chunks remain on the shared write schema (replicated); not dual-written via projection in Phase 3. |

### Planned direction (slim Phase 3)

| Topic | Direction | Notes |
|-------|-----------|-------|
| Sync mechanism | Postgres replication only | Outbox/MassTransit not used for read-model sync in Phase 3 |
| Lag UX | Lists/search may lag; mutations return in-memory DTOs | Already mitigated for create/update |
| Phase 3 PRs | Mostly docs + verify read routing against replica when recipe exists | Little or no new projection code |
| Custom projections | Deferred | Reopen at Phase 4 or when revising D30–D32 |

### Design checklist

- [ ] Document read = replica same-schema in `SOLUTION_OVERVIEW.md` when Phase 1 replica is live
- [ ] Confirm search/RAG/knowledge reads use `ReadEfContext` (Phase 1) — no projector required
- [ ] *(Revise later)* If adding a projector: idempotency keys, rebuild/catch-up, where tables live

### PR slices (draft)

| PR | Scope |
|----|--------|
| 3.1 | Docs: Phase 3 = replica-only; no custom projections; link to revise-later |
| 3.2 | *(Optional)* When Aspire replica recipe lands: smoke-test list/search against `MainDbRead` |

### Success criteria

- [ ] No denormalized projection tables added for Phase 3
- [ ] Read path remains same-schema via read connection / replica
- [ ] Custom projection explicitly deferred in decision log + revise-later section

### Open questions (Phase 3)

- [ ] Knowledge Lab: stay write-owned (replicated) until Phase 4 extract — **default yes with D34**; confirm when planning Phase 4 Knowledge service

---

## Phase 4 — Bounded-context extraction

### Intent

Strangle the monolith behind a **YARP API Gateway**, move **endpoints + workers + DBs** into microservices, keep cross-service work **event-driven**, and use an **optional thin Web BFF** only when the UI needs server-side aggregation.

```text
WebApp / MCP
    │  user JWT
    ▼
YARP API Gateway          ← JWT validation, routing, rate limits / CORS as needed
    │
    ├── /api/todos/**       → TodoService        (API + DB + outbox)
    ├── /api/knowledge/**   → KnowledgeService   (API + DB + ingest)
    ├── /api/ai/**          → AiJobsService      (API + DB + job runners)
    ├── /hubs/**            → NotificationService (SignalR only → UI)
    └── /bff/web/**         → Web BFF (optional)  (compose only; no domain DB)
              │
              └──► calls services over HTTP (forward user JWT)

Services / AI Worker ──MassTransit/RabbitMQ──► each other + NotificationService
```

### Locked decisions

| # | Decision |
|---|----------|
| D40 | **Edge:** **YARP** as API Gateway — public entry for WebApp and MCP. |
| D41 | **End state:** domain **HTTP endpoints + workers + DB** live in **microservices**. Gateway has **no** domain DB. |
| D42 | **Sync path:** Client → YARP → service (or optional Web BFF → services). |
| D43 | **Async path:** service ↔ service via **MassTransit + RabbitMQ** (Phase 2). Not via the gateway. |
| D44 | **Per-service DB + migrations** when a service owns data. |
| D45 | **JWT:** validate at **gateway + each service** (defense in depth). MCP still uses **user JWT** → gateway. |
| D46 | **Async identity:** events carry `UserExternalId` / owner ids — **no** user JWT on the bus. |
| D47 | **SignalR:** **one Notification service** owns all hubs; other services/workers publish notify events (or equivalent) for UI push. |
| D48 | **YARP + optional thin Web BFF:** default routing is YARP → microservices. Add/keep a **thin Web BFF** only for UI-shaped aggregation; BFF has **no** domain DB and does not own outbox/domain writes. |
| D49 | **Strangler:** put YARP in front of existing WebApi first; peel services off incrementally. |
| D50 | **Extract order:** (1) YARP strangler → (2) Notification service → (3) **AI Worker** (Phase 2 D28) → (4) **Knowledge** → (5) **AI Jobs** → (6) **Todo** last (highest coupling). |
| D51 | **MediatR:** keep inside **HTTP** microservices (match current style). AI Worker: MassTransit consumers → app/AiLayer services (**no** MediatR required). Gateway/BFF: no domain MediatR handlers. |

### Candidate boundaries (end state)

| Service | Owns | Notes |
|---------|------|-------|
| **YARP Gateway** | Routing, edge JWT, CORS/rate limits | No domain DB |
| **Web BFF** (optional) | Aggregated web-only endpoints | No domain DB; calls Todo/Knowledge/Jobs |
| **Todo** | Todos, AI metadata, todo embeddings (as owned) | Publishes Todo*; peeled last |
| **Knowledge** | Documents, chunks, embeddings, ingest | First domain HTTP+DB after worker |
| **AI Jobs** | Batch + sprint optimizer job state | Progress → Notification service |
| **AI Worker** | Classify/embed consumers | No public HTTP (typical); uses `Contracts` |
| **Notification** | All SignalR hubs → UI | Consumes notify/progress events |
| **WebApp / MCP** | UI / tools | Gateway base URL only |

### Design checklist

- [ ] Aspire: YARP project + per-service projects + RabbitMQ + per-service Postgres (as extracted)
- [ ] Strangler routes: `/**` → legacy WebApi, then carve path prefixes to new services
- [ ] Move hubs from WebApi → Notification; wire progress via bus
- [ ] Extract AI Worker; remove classify/embed consumers from WebApi
- [ ] Knowledge service + DB + migrations; update WebApp paths via gateway
- [ ] AiJobs service; Todo remains until later
- [ ] Introduce `/bff/web` routes only when a screen needs aggregation
- [ ] Shared `Contracts` (+ ViewModel strategy per service or shared package)

### PR slices (draft)

| PR | Scope |
|----|--------|
| 4.1 | YARP gateway in front of existing WebApi (strangler); WebApp/MCP → gateway URL |
| 4.2 | Notification service; route `/hubs/**`; migrate hubs + notifiers to events |
| 4.3 | AI Worker host; move classify/embed consumers off WebApi |
| 4.4 | Knowledge microservice + DB; YARP `/api/knowledge/**` |
| 4.5 | AI Jobs microservice + DB; YARP `/api/ai/**` (batch/optimizer) |
| 4.6 | Todo microservice + DB; shrink/remove legacy WebApi domain surface |
| 4.7 | Optional thin Web BFF when first composed UI endpoint is needed |

### Success criteria

- [ ] Clients reach the system only via YARP (plus optional `/bff/web`)
- [ ] Domain data owned by service DBs; gateway/BFF have no domain DB
- [ ] JWT enforced at gateway and on each HTTP service
- [ ] All SignalR traffic served by Notification service
- [ ] Cross-service side effects use the bus, not sync chains through the gateway
- [ ] AI Worker runs classify/embed outside WebApi

### Open questions (Phase 4)

- [ ] Exact YARP cluster names / Aspire resource layout
- [ ] Notification ingest: MassTransit-only vs small internal HTTP notify API
- [ ] When first `/bff/web` endpoint is justified (defer until a real multi-service screen needs it)

---

## Phase 5 — Cross-service choreography

### Intent

Wire multi-service async flows on **MassTransit + RabbitMQ** (Phase 2). Default pattern is **choreography** (idempotent consumers). One **optional** teaching/demo path: a **MassTransit saga/state machine** for **sprint-approve** once AiJobs and Todo are separate services.

```text
Choreography (default)
  TodoCreated → AI Worker (classify, embed) → Notification (optional)

In-service job state machines (not distributed sagas)
  Knowledge ingest steps · AI batch · sprint optimizer run (until approve)

Optional distributed saga (demo)
  Approve → SprintApproveSaga → Todo CreateSprint → Notification → finalize job
```

### Locked decisions

| # | Decision |
|---|----------|
| D60 | **Broker / library:** RabbitMQ + MassTransit (Phase 2). |
| D61 | **Default cross-service pattern:** **choreography** — pub/sub + idempotent consumers. |
| D62 | **Classify / embed / notify / delete-cleanup:** choreography only — **no** saga. |
| D63 | **Knowledge ingest, AI batch, sprint-optimizer run:** **in-service** job state machines (job row + worker), not MassTransit sagas. |
| D64 | **Sprint-approve:** **optional MassTransit saga demo** when AiJobs and Todo are split — correlate by `JobExternalId`; Todo creates sprint; Notification pushes UI; saga handles waiting/timeout/failure. Until services are split, keep today’s in-process approve. |
| D65 | Do **not** saga-ify other flows for the portfolio unless a new requirement appears. |
| D66 | Observability: trace publish/consume on `TaskManager.Ai` / new sources. |
| D67 | **Bus ordering:** no required partition keys. Correctness via **idempotent consumers**: message carries ids (e.g. `TodoExternalId`) as a hint; handler **re-reads current state from DB**, then classify/embed/upsert. **No `ContentVersion`** required. Optional partition-by-id later only if measured races/cost warrant it. |
| D68 | **Notification contracts:** **typed events** in `Contracts` (not a generic `UiNotify` bag). Initial catalog below. |
| D69 | **Sprint-approve saga store:** **Postgres via EF** (MassTransit EF saga repository; typically AiJobs DB). Not Redis / in-memory for the demo. |
| D70 | **Custom projections:** **remain deferred** (Phase 3 stands). No outbox-fed read models in the planned implementation path; reopen only under **Decisions to revise later**. |

### Typed notification catalog (D68)

| Contract (name flexible at PR) | Typical publisher | UI / hub |
|--------------------------------|-------------------|----------|
| `TodoClassificationUpdated` | AI Worker | Classification / todo row |
| `TodoEmbeddingUpdated` (optional) | AI Worker | Optional refresh |
| `AiBatchJobUpdated` | AI Jobs | AI batch hub |
| `SprintOptimizerJobUpdated` | AI Jobs | Sprint optimizer hub |
| `KnowledgeIngestUpdated` | Knowledge | Knowledge ingest hub |
| `SprintApprovalNotify` (optional) | Saga / AiJobs | Approve/complete toast |

Payloads: public ids + status + short message (align with today’s hub DTOs). Notification service: one consumer (or handler) per type → SignalR.

### Flow matrix

| Flow | Pattern |
|------|---------|
| TodoCreated / text-changed → classify | Choreography |
| TodoCreated / text-changed → embed | Choreography |
| Progress / completed → Notification (SignalR) | Choreography |
| Knowledge ingest (chunk → embed → ready) | In-service job SM |
| AI batch pipeline | In-service job SM |
| Sprint optimizer analyze → awaiting approval | In-service job SM |
| **Sprint approve → create sprint → notify → complete job** | **Optional MassTransit saga** (demo) |
| TodoDeleted → cleanup | Choreography |

### Sprint-approve saga (optional demo sketch)

| Step | Actor |
|------|--------|
| User approves via YARP → AiJobs | HTTP; job → `Persisting`; start saga (`CorrelationId` = job `ExternalId`) |
| Saga → Todo | `CreateSprintFromProposal` (selected todo ids, reasoning) |
| Todo → saga | `SprintCreated` / `SprintCreateFailed` |
| Saga → Notification | UI push |
| Saga finalize | AiJobs job `Completed` / `Failed`; optional timeout → fail |

Compensation policy (delete sprint vs fail-forward) can be chosen at implementation; fail-forward is enough for the demo.

### Design checklist

- [ ] Add typed notify events to `Contracts` (catalog above; exact names at PR)
- [ ] Notification consumers: typed event → SignalR hub/method
- [ ] Classify/embed consumers: id-only messages; re-read DB; upsert (idempotent)
- [ ] Implement sprint-approve saga only after AiJobs + Todo extract (Phase 4)
- [ ] MassTransit EF saga repository on Postgres (AiJobs DB)
- [ ] Correlation + idempotent Todo create / Notification
- [ ] Feature flag or doc note: saga optional vs sync approve fallback
- [ ] Do **not** add custom projection tables unless revise-later is reopened

### PR slices (draft)

| PR | Scope |
|----|--------|
| 5.1 | Typed notify/progress contracts + Notification consumers |
| 5.2 | Classify/embed/delete choreography: idempotent re-read DB handlers |
| 5.3 | *(Optional demo)* SprintApprove MassTransit state machine + EF saga store on Postgres + Todo create consumer |
| 5.4 | Tests + OTel for publish/consume (+ saga transitions if 5.3) |

### Success criteria

- [ ] Cross-service side effects use choreography by default
- [ ] No sagas except optional sprint-approve
- [ ] In-service jobs remain the source of truth for ingest/batch/optimizer run
- [ ] Notify path uses typed contracts → Notification → SignalR
- [ ] AI consumers safe under duplicate/out-of-order delivery without `ContentVersion`
- [ ] If saga demo ships: EF/Postgres saga state; approve survives wait for `SprintCreated` / timeout
- [ ] No custom projection tables unless explicitly reopened

### Open questions (Phase 5)

_None — Phase 5 detail decisions locked. Exact C# type names can be finalized at PR time._

---

## Implementation policy

1. **Plan in this doc** → agree decisions → then code.
2. **One PR ≈ one slice** from the phase tables; keep reversible.
3. No secrets in repo; Aspire/user-secrets for connection strings.
4. Respect existing conventions: thin controllers, `ExternalId`, `TodoAccessGuard`, AI sanitizers/validators, `Ai:Features`.
5. Update this file’s checkboxes / **Status** when a phase completes.
6. After Phase 1, update `SOLUTION_OVERVIEW.md` request-flow diagram (write vs read; fallback).

---

## Decisions to revise later

Interim choices that **must be revisited** later. Do not treat these locks as final forever.

| Topic | Current interim decision | Why revise later | What to decide then |
|-------|--------------------------|------------------|---------------------|
| **Aspire replica recipe** | Recipe **TBD**; `MainDbRead` falls back to `MainDb` so coding can proceed on one node | Target remains primary + streaming replica (Option A); fallback is not physical separation | Concrete local recipe: image (`pgvector` vs Bitnami-style), AppHost/Compose wiring, ports, replication slot/`primary_conninfo`, healthchecks, how WebApi gets `ConnectionStrings__MainDbRead` |
| **Hybrid parallelism** | **Stays sequential** (vector then lexical on one scoped `ReadEfContext`) | EF Core context is not thread-safe; dual write/read contexts alone do not unlock `Task.WhenAll` | Whether to parallelize via `IDbContextFactory<ReadEfContext>` (two short-lived contexts), keep sequential, or another approach; measure latency on todo + knowledge hybrid paths first |
| **Custom read projections** | **Deferred** (Phase 3 + D70): replica / same-schema reads only; **no** outbox-fed projection tables in the planned path | Only if a service must hold another service’s data locally, read shape must diverge, or you want an explicit projector demo | First projector (e.g. Todo list/search from `Todo*` events): tables, idempotency, rebuild/catch-up, host |

**When to reopen:**

- Replica recipe / hybrid: after Phase 1 PRs 1.1–1.6, or PR **1.7** / small follow-up.
- Custom projections: **not** on the default Phase 4/5 path — only by explicit revisit of this table. Update the decision log when revised.

---

## Decision log

| Date | Phase | Decision |
|------|-------|----------|
| 2026-10-14 | 1 | Primary + streaming replica (Option A) as target; **replica recipe TBD / fallback first** |
| 2026-10-14 | 1 | Create/Update return DTO mapped from in-memory entity (no re-Get) |
| 2026-10-14 | 1 | Jobs (batch/optimizer) fully on write DB |
| 2026-10-14 | 1 | Authz-before-write loads use write DB |
| 2026-10-14 | 1 | **Profile lookup → write DB** |
| 2026-10-14 | 1 | **Hybrid search stays sequential** |
| 2026-10-14 | 2 | **Transport: RabbitMQ** |
| 2026-10-14 | 2 | **Messaging library: MassTransit** |
| 2026-10-14 | 2 | **Outbox: MassTransit EF Core transactional outbox on `WriteEfContext`** (explicit publish + same UoW) |
| 2026-10-14 | 2 | **Classify/embed: MassTransit consumers, idempotent; retire in-memory queues** |
| 2026-10-14 | 2 | **Contracts: new assembly** (e.g. `src/Contracts`) |
| 2026-10-14 | 2 | **Consumers: WebApi in-process now; extract worker in Phase 4** |
| 2026-10-14 | 2 | **Update publishes classify/embed only when text (title/description) changes** |
| 2026-10-14 | 3 | **Streaming replica enough**; no custom projection tables in Phase 3 |
| 2026-10-14 | 3 | **Custom projections deferred — revise later** (Phase 4 or explicit projector demo) |
| 2026-10-14 | 4 | **YARP API Gateway** as public edge (WebApp + MCP) |
| 2026-10-14 | 4 | **Microservices own endpoints + workers + DB**; gateway has no domain DB |
| 2026-10-14 | 4 | **Async** between services (MassTransit/RabbitMQ); sync via gateway |
| 2026-10-14 | 4 | **Per-service DB + migrations** |
| 2026-10-14 | 4 | **JWT: gateway + each service** |
| 2026-10-14 | 4 | **Notification service** owns all SignalR hubs |
| 2026-10-14 | 4 | **YARP + optional thin Web BFF** (BFF = compose only; no domain DB) |
| 2026-10-14 | 4 | **Extract order:** YARP → Notification → AI Worker → Knowledge → AI Jobs → Todo last |
| 2026-10-14 | 4 | **MediatR** in HTTP services; not required on AI Worker |
| 2026-10-14 | 5 | **Choreography default** for cross-service flows |
| 2026-10-14 | 5 | **Sprint-approve = optional MassTransit saga demo**; everything else choreography (or in-service job SM) |
| 2026-10-27 | 5 | **Bus: idempotent re-read DB**; no required partition keys; no `ContentVersion` |
| 2026-10-27 | 5 | **Notification catalog: typed events** |
| 2026-10-27 | 5 | **Sprint-approve saga store: Postgres via EF** |
| 2026-10-27 | 5 | **Custom projections remain deferred** |
| 2026-10-14 | — | Design all phases before bulk implementation; ship phase-by-phase PRs |

---

## Next: implementation

Architecture planning for Phases 1–5 is complete (remaining items are revise-later: Aspire replica recipe, hybrid parallelism, custom projections).

**Start implementation with Phase 1 PR slices** (config + dual DbContext fallback, then in-memory create/update DTOs, then read/write routing). Do not implement Phase 4/5 until Phases 1–2 land unless explicitly scoped.
