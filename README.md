# MetricBreak

**The Time-Series Debugger**  
*Replay. Break. Inspect.*

MetricBreak is an offline-first, industry-independent debugger for recorded time-series telemetry. It helps engineers investigate what happened, find when conditions occurred, inspect the exact samples around an event, and preserve reproducible debugging context.

> **Project status (November 2026):** Active development. Numeric CSV ingestion, recording/session management, deterministic replay navigation, conditional and composite breakpoints, a .NET API, and a React debugging interface have been implemented. Other capabilities described below are in progress or planned. This README distinguishes the existing foundation from the intended MVP; it is not a release-readiness claim.

## Why MetricBreak?

Charts show measurements over time. MetricBreak is designed to let you **debug** those measurements: navigate backward and forward, stop on meaningful conditions, inspect exact values, and investigate relationships between signals.

Example questions:

- When did pressure first fall below 4,000 psi?
- At which sample were pressure above 9,000 psi **and** flow rate below 50 bpm?
- What happened immediately before and after a breakpoint triggered?
- Was a valve `ON` when pressure spiked? *(Requires planned typed-channel support.)*
- How did an abnormal recording differ from a known-good run? *(Planned comparison capability.)*

MetricBreak's core remains **industry-neutral**. Industrial telemetry, vehicle diagnostics, infrastructure/SRE recordings, and scientific measurements are use cases—not hardcoded assumptions.

## Current implementation

The following capabilities have been developed; their end-to-end integration and release hardening remain ongoing:

- **Numeric CSV ingestion:** Import recorded numeric telemetry and expose recording/channel metadata.
- **Recording and session management:** Create and inspect replay sessions through the API.
- **Deterministic replay navigation:** Seek, step forward/backward, and continue toward breakpoints, including reverse breakpoint traversal in the replay engine.
- **Conditional breakpoints:** Evaluate numeric channel conditions.
- **Composite breakpoints:** Combine numeric conditions using `AND` or `OR`, with API operations and React UI for creation and management.
- **Project workspace foundation:** Create/open project workflows and local persistence infrastructure.
- **Browser-based UI:** React/TypeScript interface for recordings, channels, replay controls, and breakpoints.
- **Automated tests:** Domain and API tests cover core behaviors; comprehensive end-to-end validation remains a release task.

Implementation details and completeness may change as development continues.

## CSV-first MVP

**CSV is the initial and reference data source.** We will finish ingestion, processing, replay, breakpoints, inspection, persistence, and end-to-end reliability for CSV **before** evaluating other sources such as MQTT, Kafka, databases, or live streams.

### Typed CSV channels — next milestone

CSV ingestion will recognize three channel value types:

| Type | Examples | Intended behavior |
| --- | --- | --- |
| Numeric | `1200`, `45.2` | Numeric charts and threshold conditions |
| Boolean | `true`, `false` | Boolean inspection and equality conditions |
| Categorical | `ON`, `OFF`, `RUNNING`, `STOPPED` | State inspection, transitions, and equality conditions |

Type inference will inspect column values and preserve their meaning. `ON`/`OFF` will initially be treated as **categorical**, not silently converted to `1`/`0`. Empty cells need explicit missing-value handling. The original categorical spelling should be retained. Type overrides may be introduced where inference is ambiguous.

Typed channel metadata and values are **planned**, not yet part of the completed numeric ingestion pipeline.

### Simulation-style playback — planned MVP capability

MetricBreak will evolve the existing replay engine into a simulation-style playback experience for **recorded** data:

- Play, pause, seek, single-step, and continue.
- Play forward **and backward**.
- Fast-forward and reverse at adjustable speeds.
- Support timestamp-based pacing and, where useful, sample-based playback.
- Stop precisely at enabled simple or composite breakpoints in either direction.
- Keep breakpoint evaluation accurate even when chart/UI updates are throttled at high playback speeds.
- Handle end-of-recording, cancellation, and resume without repeatedly stopping on the same sample.

**Playback does not generate or predict telemetry.** It controls traversal of immutable recorded samples. The replay engine determines sample order and breakpoint results; a separate playback controller handles direction, timing, and speed.

### Typed breakpoints — planned extension

Numeric conditions will be extended to typed channels. For example:

```text
ValveState == ON
    AND
Pressure > 1400
```

Categorical and Boolean comparisons will use operators appropriate to their types. The debugger must not impose numeric ordering on text values.

## Product areas

### Recording Dashboard — execute and inspect

The Recording Dashboard is the primary debugging surface. It provides recording/channel inspection, replay navigation, and breakpoint management. Planned additions include simulation-style playback controls, categorical state timelines, and richer breakpoint-hit inspection.

### Investigations — preserve debugging context

An **investigation** is a named, resumable debugging effort associated with one or more recordings. The intended workflow is to save and reopen:

- Recording references and source provenance.
- Replay position and selected time window.
- Simple and composite breakpoint definitions and enabled state.
- Bookmarks, notes, observations, and findings.
- Selected channels, chart layout, and other relevant inspection context.

Investigation persistence and restoration are **planned MVP work**, building on the existing project/workspace foundation.

### Analysis — examine relationships and evidence

**Analysis** examines the data rather than controlling playback. Initial planned features include:

- Per-channel statistics (minimum, maximum, average, and related measures).
- Selected time-window analysis.
- Multi-channel comparison within a recording.
- Inspection of values before and after breakpoint hits.
- Boolean/categorical state transitions.
- Navigation from an analysis finding back to the recording and timestamp.

Derived channels, automated anomaly detection, and cross-recording alignment/divergence are later capabilities. Analysis should use deterministic calculations and the same underlying recording services as the debugger, not a separate telemetry engine.

## Quick analysis and project workspaces

MetricBreak is designed to work **with or without a project**, like a development environment that can open an individual source file without creating a solution.

### Quick analysis — no project required

The intended quick-analysis workflow opens a CSV recording and immediately allows replay, inspection, and temporary breakpoints. **Project-free opening and its full UI workflow still require validation/completion**; project creation must not become a domain-engine dependency.

### Project workspace — organize ongoing work

A project can group related recordings and debugging work. Its persisted content may include:

- **Recordings:** Source references, identities, and integrity metadata.
- **Investigations:** Saved debugging state, notes, and findings.
- **Breakpoints:** Reusable conditional and composite definitions.
- **Bookmarks and events:** Points and intervals of interest.
- **Comparisons:** Recording selections and alignment settings *(planned)*.
- **External issues:** References to Jira or other trackers *(future integration)*.
- **Reports and settings:** Exported evidence, channel mappings, and workspace configuration *(planned)*.

A portable `.mbproj` manifest is a **proposed format**, not a promise that it is already implemented. A future **Save as Project** action should allow quick-analysis work to become a persistent workspace.

### Architectural boundary

The recording reader, replay engine, breakpoint evaluation, and analysis logic **must not depend on projects**. A debug session can exist independently; a project organizes references and saved investigation state. Removing a recording reference from a project must not silently delete the original source file.

## Architecture

```text
CSV Source (initially)
        |
        v
Ingestion & Type Inference
        |
        v
Recording + Typed Channel Metadata
        |
        v
Telemetry Reader / Replay Engine <---- Playback Controller (planned)
        |                                     |
        v                                     v
Breakpoint Evaluation                 Direction / Clock / Speed
        |
        v
ASP.NET Core API
        |
        v
React / TypeScript UI
   |           |           |
Recording  Investigations   Analysis
Dashboard   (planned)      (planned)
```

The diagram shows the **target separation of responsibilities**, not a claim that every component is already implemented. Future data sources should adapt to the same recording and telemetry abstractions without changing the replay and breakpoint engines.

## Design principles

1. **Deterministic by default.** Identical recordings, rules, and sample positions produce identical evaluations regardless of navigation direction.
2. **Immutable source telemetry.** Preserve original recordings and their provenance; debugging operations do not alter source data.
3. **Industry-neutral core.** Domain-specific units, mappings, and terminology belong in configuration/profiles rather than core replay logic.
4. **Offline-first.** Core workflows run without required cloud services or external inference APIs.
5. **Projects are optional.** Debugging a recording should not require a project; workspaces add persistence and organization.
6. **Typed data remains typed.** Numeric, Boolean, and categorical values retain their semantics throughout ingestion, inspection, and rule evaluation.
7. **Breakpoint correctness over rendering speed.** High-speed playback may throttle UI updates but must not skip breakpoint evaluation.
8. **AI is optional.** Any future assistant must run locally; deterministic engines remain authoritative for calculations, rules, and evidence. No cloud inference fallback.
9. **Independent implementation.** MetricBreak code is designed and written independently, not copied or adapted from external codebases.

## Technology

- C# / .NET 10
- ASP.NET Core API
- React / TypeScript with Material UI
- Local filesystem persistence; SQLite remains a possible local metadata option, not a confirmed requirement
- CSV as the initial import format
- Modular, testable domain, ingestion, replay, breakpoint, API, and UI components

## Roadmap

### Implemented foundation

- [x] Establish recording, channel, sample, and replay domain foundations.
- [x] Implement numeric CSV ingestion and recording sessions.
- [x] Implement replay seek and step operations.
- [x] Implement forward and backward breakpoint traversal in the replay engine.
- [x] Implement numeric conditional breakpoints.
- [x] Implement composite `AND`/`OR` breakpoints and API integration.
- [x] Add composite breakpoint management to the React UI.
- [x] Establish project workspace creation/opening and local persistence foundations.
- [x] Build the initial browser-based recording/debugging interface.

### Complete the CSV MVP

- [ ] Infer numeric, Boolean, and categorical CSV channels; preserve typed values and missing-value semantics.
- [ ] Expose typed channel metadata and sample values through the API and UI.
- [ ] Add simulation-style play/pause, reverse, fast-forward, and adjustable-speed playback.
- [ ] Extend simple/composite breakpoint evaluation and UI to typed values.
- [ ] Complete investigation persistence, restoration, bookmarks, and notes.
- [ ] Add initial Analysis functionality: statistics, time-window inspection, and multi-channel comparison.
- [ ] Add categorical state timelines and richer breakpoint-hit inspection.
- [ ] Validate and complete project-free quick analysis and Save as Project.
- [ ] Test the full CSV workflow end to end, including large files, malformed data, recovery, and performance.

### After the CSV MVP

- [ ] Add two-recording comparison, alignment, and divergence rules.
- [ ] Expand telemetry search and data-quality diagnostics.
- [ ] Evaluate other recording and streaming data sources behind stable ingestion abstractions.
- [ ] Evaluate optional external integrations and local-only AI separately.

## Contributing

MetricBreak is under active development. Development setup and contribution guidelines will be expanded as the architecture stabilizes. Use GitHub Issues to propose changes or report problems once issue tracking is enabled.

## License

Licensed under the [Apache License, Version 2.0](LICENSE).

Copyright © 2026 OPV Software (subject to confirmation of the legal rights holder).
