# Hacker News Best Stories API

An ASP.NET Core (.NET 10) API that periodically fetches the best stories from the
[Hacker News API](https://github.com/HackerNews/API) and serves the top N by score.

## Endpoints

| Method | Path                | Description                                   |
| ------ | ------------------- | --------------------------------------------- |
| GET    | `/stories/{count}`  | The top `count` stories, ordered by score.    |

In the `Development` environment the OpenAPI document and the Scalar UI are also available.

## Configuration

Settings live in `appsettings.json`:

| Key                       | Description                                          | Default                                |
| ------------------------- | ---------------------------------------------------- | -------------------------------------- |
| `HackerNews:BaseAddress`  | Hacker News API origin (absolute, no path).          | `https://hacker-news.firebaseio.com/`  |
| `HackerNews:RefreshPeriod`| How often the best stories are refreshed.            | `00:01:00`                             |
| `Kestrel:Endpoints:Http:Url` | Address the API listens on.                       | `http://localhost:5195`                |
| `Serilog:*`               | Logging. Files are written to `C:\logs\HackerNews.BestStories.API\`. | |

## Running locally

```powershell
dotnet run --project src/HackerNews.BestStories.Api
```

Then open `http://localhost:5195/stories/10`.

## Running tests

```powershell
dotnet test
```

## Installing as a Windows service

The application can run as a Windows service. When started from a console, it behaves as a normal
console application.

### Prerequisites

- Windows.
- An **elevated** PowerShell (Run as administrator) to create and manage the service.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download) on the build machine. The target machine
  needs no .NET installation when the app is published self-contained (as below).

### 1. Publish

From the repository root:

```powershell
dotnet publish src\HackerNews.BestStories.Api -c Release -r win-x64 --self-contained -o C:\Services\HackerNews.BestStories.API
```

### 2. Install and start the service

```powershell
sc.exe create "HackerNews.BestStories.API" binPath= "C:\Services\HackerNews.BestStories.API\HackerNews.BestStories.Api.exe" start= auto DisplayName= "Hacker News Best Stories API"
sc.exe description "HackerNews.BestStories.API" "Serves the best Hacker News stories and refreshes them periodically."
sc.exe start "HackerNews.BestStories.API"
```

> - In PowerShell use `sc.exe`. Plain `sc` is an alias for `Set-Content`.
> - `sc.exe` requires a space **after** each `=` (`binPath= "..."`) and none before it.
> - The service name must match the `ServiceName` set in `Program.cs` (`HackerNews.BestStories.API`).

### 3. Verify

```powershell
sc.exe query "HackerNews.BestStories.API"
Invoke-RestMethod http://localhost:5195/stories/5
```

`sc.exe query` should report `STATE : 4 RUNNING`. Logs are written to
`C:\logs\HackerNews.BestStories.API\app-<date>.log`; look for
`Refreshed N of M best stories`.

The account needs write access to `C:\logs\HackerNews.BestStories.API`.

### Stopping and uninstalling

```powershell
sc.exe stop "HackerNews.BestStories.API"
sc.exe delete "HackerNews.BestStories.API"
```

### Troubleshooting

- **No log file appears.** Logging errors are swallowed silently. Check that the service account can
  write to `C:\logs\HackerNews.BestStories.API` (see the `LocalService` note above).
- **Service fails to start.** Check the Windows Event Viewer (Windows Logs > Application) and the
  log file. A common cause is an invalid `HackerNews:BaseAddress`, which is validated on startup.
- **Port already in use.** Change `Kestrel:Endpoints:Http:Url` in the published `appsettings.json`
  and restart the service.
- **`sc.exe create` says the service already exists.** Delete it first (see above).

## Operational assumptions

These describe how the service is expected to be deployed and used, and are the reasoning behind
the periodic-refresh design. They are the first things to revisit if the service is scaled up.

1. **A single instance is running.** Each instance keeps its own in-memory copy of the stories and
   runs its own refresh loop, so the load on the Hacker News API is one list request plus one
   request per story, per `HackerNews:RefreshPeriod`, *per instance*. Running *n* instances
   multiplies that by *n*. Scaling out would mean sharing the stories through an external store
   (for example Redis) with a single instance performing the refresh.

2. **Stale-but-available is preferred to fresh-or-failed.** A failed refresh leaves the previously
   fetched stories in place rather than emptying them or failing requests, so the service degrades
   in freshness rather than in availability.

3. **Callers tolerate data up to one refresh period old** (`00:01:00` by default). Scores change
   slowly relative to this, and serving from a periodically refreshed copy is what keeps load on
   the Hacker News API constant regardless of how much traffic this API receives.

4. **Requests are served from memory, never by calling Hacker News on demand.** Incoming traffic
   therefore has no effect on how often Hacker News is called, which is what allows the API to
   absorb large numbers of requests without risking overloading it.

5. **The host has ample memory for the working set.** The stories held in memory amount to a few
   hundred kilobytes, so there is deliberately no eviction policy or cache size limit.

6. **The service account can write to the log directory.** Logs are written to
   `C:\logs\HackerNews.BestStories.API\`, and logging errors are silent, so a service account
   without write access produces a running service and no logs. See *Troubleshooting*.

7. **Configuration is supplied per environment.** `appsettings.json` holds the defaults above;
   environment variables or a published `appsettings.json` override them per deployment. The value
   most likely to differ between environments is `Kestrel:Endpoints:Http:Url`.

## Improvements

Given more time, the following would be worth doing, roughly in order of value.

### Investigate push notifications (SSE)

Hacker News runs on a Firebase Realtime Database, so any `v0` path requested with an
`Accept: text/event-stream` header becomes a live subscription rather than a one-off read. A full
push-based design would need **two** subscriptions, because neither feed carries story data - both
deliver bare item IDs, so every detail still comes from `item/{id}.json`. The two feeds only say
*what to fetch*.

**1. `beststories.json` - which stories are in the list.** The first event carries the complete ID
array, and each later event carries the whole array again. Measured over a 150-second window it
produced 5 `put` events (no incremental `patch`) and 4 keep-alives, so the list changes roughly
every 35 seconds. On each event the array is diffed against the IDs already held: an added ID costs
one fetch, a removed ID costs nothing. This feed alone replaces just one of the ~200 requests per
refresh - under 1% of upstream traffic - so on its own it buys faster awareness of membership
changes rather than any meaningful reduction in load.

**2. `updates.json` - which items changed.** This is the only part that could replace the ~200 item
fetches with a handful of targeted ones. Its IDs are filtered against the best-story IDs already
held: an ID in that set is re-fetched to pick up its new score and comment count, and **an ID
outside it is discarded without any request**. An unknown ID never needs a call, because whether a
story belongs in the list is Hacker News's decision, delivered through `beststories.json` - not
something this API infers from an item's score. That also means this feed is unusable on its own:
without the first subscription there is no set to filter against, and every bare ID would have to
be fetched just to discover what it is.

Both feeds share the same operational cost: a long-lived connection, reconnect-with-backoff, a
keep-alive watchdog, and - because Firebase's SSE has no event replay - a full resynchronisation
after every reconnect. A periodic full refresh would therefore have to be retained as a backstop,
at a longer interval, to recover anything missed while a connection was down.

The open question is confined to the second feed: if a score-only change (an upvote) does not count
as an item update, `updates.json` would report comment activity but never scores, and the periodic
refresh would still be doing the real work. That is worth measuring - listening for a few minutes
and checking how many changed IDs fall inside the best-story set, and whether their scores actually
moved - before any of this is designed.

### Authentication

The API is currently open, which is appropriate for public data on a trusted network but not for
public exposure. API-key authentication would be the lightest option, with JWT bearer tokens if it
were to sit behind an existing identity provider. The main benefit is not secrecy but
accountability: knowing which client is responsible for which traffic makes quotas, throttling and
abuse investigation possible, none of which work well when every caller is anonymous.

### Resilience for the HttpClient, and its effect on the refresh loop

Transient failures currently cost a story its refresh for that cycle. Adding
`Microsoft.Extensions.Http.Resilience` to the typed client would bring retry with exponential
backoff and jitter, a per-attempt timeout, and a circuit breaker. The subtlety is that this
interacts with the `Parallel.ForEachAsync` that fetches the stories: the degree of parallelism caps
how many requests are in flight, but retries multiply how long each item occupies one of those
slots, so worst-case cycle time becomes roughly (stories x attempts x attempt-timeout) / degree of
parallelism. That figure must stay comfortably below the refresh period, otherwise cycles overlap
or the data silently ages, so the retry count, timeouts and parallelism have to be chosen together
rather than tuned in isolation. A circuit breaker helps here: once Hacker News is clearly failing,
the remaining items fail fast instead of each burning its full retry budget, and the previously
fetched stories continue to be served.

### Health check endpoint, including Hacker News connectivity

A liveness endpoint for "the process is up" and a readiness endpoint for "this instance can serve
useful data" would let a load balancer or service manager avoid routing to an instance that has not
yet completed its first refresh. Readiness should be based on the age of the last successful
refresh rather than on calling Hacker News during the probe: probes are frequent, and a check that
makes an upstream call would add load and make our health depend on theirs. Reporting degraded when
the data is older than a small multiple of the refresh period surfaces a silently failing refresh
loop, which is otherwise invisible because the API keeps serving the stale copy quite happily.

### Continuous delivery for tests and deployment

A pipeline building the solution and running the tests on every push and pull request would make it
visible whether the code is green without cloning and running it locally, and would stop a broken
change reaching the main branch. Beyond that, the publish and the service installation described
above are manual, repeatable steps that are easy to get subtly wrong. Automating publish, artifact
versioning and deployment to the target machine removes that risk and makes rollbacks a matter of
redeploying a previous artifact.

### Test coverage gate

Coverage is currently unmeasured, so there is nothing to stop it drifting downwards. Collecting it
during the test run and failing the build below an agreed threshold keeps that honest. The
threshold matters less than the direction of travel, and it is worth excluding code where coverage
is misleading, such as generated files and startup wiring, so that the number reflects the logic
that actually warrants tests.

### Startup logging, to surface logging configuration errors

Logging is configured from `appsettings.json`, and a mistake there - a bad path, an unwritable
directory, a malformed sink configuration - currently produces a service that starts and then
writes nothing, which is the hardest kind of fault to diagnose because the usual diagnostic channel
is the thing that failed. Creating a minimal bootstrap logger before configuration is read, then
swapping in the configured one, means failures during that swap are themselves logged. Wrapping
startup in a try/catch that logs and rethrows, and enabling the logging library's own internal
error output, turns a silent no-logs service into an explicit error at the point of failure.

### Rate limiting

Nothing currently stops one client consuming the whole service's capacity. A per-client limit,
keyed on API key once authentication exists and on remote IP address until then, would reject
excess requests with `429 Too Many Requests` and a `Retry-After` header rather than degrading for
everyone. Two caveats: behind a reverse proxy the limiter needs forwarded-headers configuration or
it will see the proxy's address and throttle all clients as one, and the limit must be set well
above realistic use, since this API is specifically intended to absorb high request volumes and an
over-tight limit would defeat its purpose.

### Response caching

Every request currently produces a fresh serialisation of the same in-memory data. Because the
underlying data only changes once per refresh period, responses can advertise their freshness with
`Cache-Control` headers so that browsers, proxies and CDNs can serve repeat requests without
reaching the service at all - the largest available saving, since it removes the request entirely.
Server-side output caching would additionally reuse the serialised bytes for identical requests,
and an `ETag` with support for conditional requests would let unchanged data be answered with a
`304 Not Modified`. The cache lifetime should be tied to the refresh period so clients are never
told data is fresher than it is.
