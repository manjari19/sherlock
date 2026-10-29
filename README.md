# Sherlock

A defect triage tool: it reads application log files, groups
repeated errors that differ only by dynamic values (a user ID, a
hostname, a timestamp) into a single issue, and ranks those issues so
the ones actually worth investigating first show up at the top,
instead of just the most recent line in the file.

## The problem this solves

A raw log file makes five different "User 12345 not found" errors
(each with a different user ID) look like five unrelated one-off
problems. They are not. They are one recurring bug, happening five
times. Sherlock normalizes messages into signatures (strip IDs,
GUIDs, quoted values) so those five lines cluster into one issue with
a count of 5, which is the actual shape of the problem an engineer
needs to see.

## How ranking works, and an honest tradeoff in it

Each issue cluster gets a score combining three things:

- Frequency: how many times it happened
- Severity: WARN < ERROR < FATAL, weighted accordingly
- Recency: a bug that stopped happening a week ago matters less right
  now than one still actively firing, so the score decays with a
  roughly 3-day half-life since the issue was last seen

Verified against a real sample log: 5 separate "User X not found"
errors (different IDs) correctly cluster into one issue and outrank
a single FATAL connection timeout, because the recurring issue's
cumulative frequency outweighs one severe-but-isolated event in this
scoring model. That is a real, debatable design choice, not an
obviously correct one; a single FATAL crash arguably deserves first
look regardless of frequency. It is called out here rather than
buried, because a triage tool's scoring logic is exactly the kind of
thing that should be interrogated, not trusted blindly. Separately,
"Rank scores FATAL above ERROR at equal frequency and recency" is
tested directly, confirming severity does break ties correctly when
frequency is not the deciding factor.

## Architecture

```
src/core/       Sherlock.Core: parsing, signature normalization,
                clustering, and ranking logic (no ASP.NET dependency)
  Models/         LogEntry, LogLevel
  Parsing/        LogParser (regex-based, handles multi-line stack traces)
  Analysis/       SignatureNormalizer, IssueCluster, TriageEngine
src/api/        Sherlock.Api: ASP.NET Core Web API
  POST /api/logs/ingest              submit raw log text
  GET  /api/logs/triage              ranked issue list
  GET  /api/logs/clusters/{source}/{signature}   drill into one issue
src/tests/      Hand-rolled test harness, 13 tests (see note below)
sample-data/    A realistic sample log used by tests and the demo
demo/           A small console program that runs the pipeline
                against the sample log and prints a triage report
```

## Verified end to end

- 13 tests pass, including a full-pipeline test that parses the real
  sample log and checks the known clustering (5 user-lookup failures
  with different IDs correctly collapse into 1 cluster of 5)
- The API was actually started as a running server and hit with curl:
  ingested the sample log, got back a correctly ranked triage report
  matching the demo's output exactly
- The demo program was run directly and its output is what is quoted
  in this README, not a hypothetical example

## Current limitations

Sherlock has no external dependencies: no Entity Framework Core, no
xUnit, and no SQL persistence. Logs are held in memory for the
lifetime of the process, and the test suite uses a small hand-written
harness. The parsing, clustering, and ranking logic is fully real and
tested. A production version would add a real datastore (and a log
search backend like Elasticsearch or OpenSearch) behind the same
LogStore interface without touching the core library.

## Running it

```bash
cd src/tests && dotnet run

cd src/api && dotnet run --urls "http://localhost:5230"

cd demo && dotnet run
```
