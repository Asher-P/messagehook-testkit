# MessageHook.Playbook.Service + UI

A browser board for authoring and running [MessageHook.Playbook](../MessageHook.Playbook) scenarios — packaged
with the React UI in **one docker container**. Create a test suite, configure its Kafka once, upload message
JSONs into a payload stack, build test cases as steps, and run them against your Kafka with **live per-step
results**.

It's a thin shell over the library's `PlaybookRunner`: `RunAsync` executes a run, `Validate` does broker-free
checks, `IProgress<StepResult>` drives the live stream. The service **connects to an existing Kafka; it never
provisions one.**

## Run it (docker)

```sh
docker compose -f docker-compose.ui.yml up --build
# open http://localhost:8081
```

Suites and uploaded payloads persist in the `messagehook_playbook_data` volume.

**Kafka reachability:** enter the broker's bootstrap servers in each suite's Kafka form. A Kafka client follows
the broker's *advertised* listeners after bootstrap, so:

- Broker on your host that advertises a host address → `host.docker.internal:9092` (the compose file maps it).
- Broker in docker that advertises only an internal name (e.g. `INTERNAL://kafka:29092`) → attach this container
  to the broker's docker network and use that internal bootstrap (`kafka:29092`). See the commented `networks:`
  blocks in `docker-compose.ui.yml`. Verified working this way against a broker on `create-level-flow_default`.
- Cloud broker → its usual host:port + credentials (SASL/TLS in the Kafka form).

## Run it (local dev)

```sh
# terminal 1 — service (serves API; also serves wwwroot if you've built the UI into it)
dotnet run --project playbook/MessageHook.Playbook.Service   # http://localhost:5xxx (or set ASPNETCORE_URLS)

# terminal 2 — UI dev server with hot reload (proxies /api to http://localhost:8099)
cd playbook/messagehook-ui && npm install && npm run dev     # http://localhost:5173
```

For a production-like local check, `npm run build` in `playbook/messagehook-ui` and copy `dist/*` into
`playbook/MessageHook.Playbook.Service/wwwroot`, then just run the service — it serves the SPA and API same-origin.
Or just run `bake-ui.sh` / `bake-ui.bat` from the repo root, which does the build-and-copy for you.

Set the data directory with `DataDir` (defaults to `./data` locally, `/data` in the container).

## API

| Method | Route | Purpose |
|---|---|---|
| GET/POST | `/api/suites` | list / create suites |
| GET/PUT/DELETE | `/api/suites/{id}` | read / save / delete a suite (with its test cases) |
| POST | `/api/suites/{id}/payloads` | upload a payload JSON (multipart) into the stack |
| GET/DELETE | `/api/suites/{id}/payloads/{name}` | preview / delete a payload |
| POST | `/api/suites/{id}/testcases/{caseId}/validate` | broker-free validation → `{ valid, errors }` |
| POST | `/api/suites/{id}/testcases/{caseId}/run` | run one case → NDJSON stream of `step` / `result` / `done` |
| POST | `/api/suites/{id}/run` | run all cases in the suite |

Storage is plain files under the data dir: `suites/<id>/suite.json` + `suites/<id>/payloads/*.json` — inspectable
and hand-editable. A suite's Kafka config + declared topics + a test case's steps assemble directly into a
`PlaybookDefinition`, so what runs is exactly the playbook format.
