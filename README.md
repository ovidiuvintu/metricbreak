# MetricBreak

**The Time-Series Debugger**  
*Replay. Break. Inspect.*

MetricBreak is an offline-first, industry-independent debugger for recorded time-series telemetry. It helps engineers investigate what happened, find when conditions occurred, inspect the exact samples around an event, and preserve reproducible debugging context.

> **Project status (November 2026):** Active development. CSV ingestion with typed channel values, recording/session management, deterministic forward/reverse replay, simulation-style playback, conditional, missing-data, and composite breakpoints, a .NET API, and a React debugging interface are implemented. Investigation persistence, expanded analysis, and release hardening remain ongoing or planned. This README is not a release-readiness claim.

## Why MetricBreak?

Charts show measurements over time. MetricBreak is designed to let you **debug** those measurements: navigate backward and forward, stop on meaningful conditions, inspect exact values, and investigate relationships between signals.

Example questions:

- When did pressure first fall below 4,000 psi?
- At which sample were pressure above 9,000 psi **and** flow rate below 50 bpm?
- What happened immediately before and after a breakpoint triggered?
- Was a valve `ON` when pressure spiked?
- How did an abnormal recording differ from a known-good run? *(Planned comparison capability.)*

MetricBreak's core remains **industry-neutral**. Industrial telemetry, vehicle diagnostics, infrastructure/SRE recordings, and scientific measurements are use cases—not hardcoded assumptions.

## Current implementation

The following capabilities are implemented; full end-to-end validation and release hardening remain ongoing:

- **Typed CSV ingestion:** Import recordings with numeric, Boolean, and categorical values; preserve missing-value semantics and source sample order, including duplicate timestamps.
- **Recording and session management:** Create and inspect recording sessions through the ASP.NET Core API.
- **Deterministic replay:** Seek, step forward/backward, and navigate conditional breakpoint hits in either direction.
- **Simulation-style playback:** Play/pause, forward/reverse traversal, adjustable speed, and time-based or sample-based pacing. Breakpoint evaluation remains authoritative even when UI rendering is batched.
- **Breakpoints:** Conditional rules on numeric, Boolean, and categorical values, missing-data rules (a recording time gap or a channel that stays empty longer than a duration), and composite `AND`/`OR` rules. Each breakpoint shows how often it matches and how long the signal stays on that side, including the longest run, without moving the replay cursor.
- **Recording chart:** Numeric-only plotted series and legend, channel selection, paginated sample windows (up to 5,000 samples per page), zoom, and a replay-position indicator and scrub control. Missing values appear as gaps rather than invented zero readings.
- **Sample inspection:** Inspector tabs for Channels, Breakpoints, Values, and Timeline. Categorical and Boolean values remain inspectable even though they are not plotted as numeric chart series. In Values, the reading sits next to the channel name. When the value changed from the previous sample, that earlier reading is shown underneath. Stepping one sample does not blank the comparison.
- **Chart context menu:** Right-click the plot, the current-sample label, or the replay-cursor chip to move the cursor, inspect values without seeking, bookmark the sample, add a breakpoint, or copy the sequence or timestamp. The label and the chip use the replay sequence itself, so the breakpoint lands on that sample rather than the nearest chart pixel. The chart uses a standard arrow cursor.
- **Recording duration:** The dashboard duration counts every sample interval, including the last sample. A recording of 10,800 one-second samples lasts 3 hours. The clock from the first timestamp to the last is one interval shorter.
- **Local project workspace foundation:** Project create/open workflows and local persistence infrastructure.
- **Automated tests:** Core domain and API tests; comprehensive browser and end-to-end validation remains a release task.

## CSV-first MVP

**CSV is the initial and reference data source.** Complete the CSV debugging workflow, investigation persistence, and reliability work before evaluating MQTT, Kafka, databases, or live streams.

### Typed CSV channels — implemented foundation

| Type | Examples | Current behavior |
| --- | --- | --- |
| Numeric | `1200`, `45.2` | Numeric plotting, inspection, and numeric breakpoint conditions |
| Boolean | `true`, `false` | Typed inspection and equality, inequality, and edge breakpoints; excluded from numeric plots |
| Categorical | `ON`, `OFF`, `RUNNING` | Typed inspection and equality, inequality, and change breakpoints; excluded from numeric plots |
| Missing | Empty CSV cell | Explicit missing value; chart gaps instead of fabricated zeros |

Channel typing preserves the distinction between measurements and states. `ON`/`OFF` are categorical rather than silently converted to numeric values. Equality, inequality, and change breakpoints apply to Boolean and categorical channels. Numeric channels also support threshold and edge comparisons.

### Simulation-style playback — implemented foundation

- Play, pause, seek, step, and continue through recorded samples.
- Play forward or backward with adjustable speed.
- Use timestamp-based or sample-based playback.
- Stop at matching conditional, missing-data, or composite breakpoints in either direction.
- Keep breakpoint evaluation independent of throttled or batched chart updates.

**Playback never generates or predicts telemetry.** It traverses immutable recorded samples. The replay engine owns sample ordering and breakpoint results; playback controls handle direction, timing, and speed.

### Typed breakpoints

Boolean and categorical conditions use operators that fit the type. A composite rule can mix them with a numeric threshold:

```text
ValveState == ON
    AND
Pressure > 1400
```

The debugger does not impose numeric ordering on text values.

## Product areas

### Recording Dashboard — execute and inspect

The Recording Dashboard is the primary debugging surface. It provides recording/channel inspection, replay navigation, and breakpoint management. The interface includes playback controls, paginated numeric charts, a replay scrubber, sample-value inspection, and right-click actions on the plot and on the current-sample label. Imported CSV events and stages appear on the Timeline tab and can move the replay cursor. Breakpoint hits are marked on the chart sequence axis. A categorical state timeline derived from channel values is still planned.

### Investigations — preserve debugging context

An **investigation** is a named, resumable debugging effort associated with one or more recordings. The intended workflow is to save and reopen:

- Recording references and source provenance.
- Replay position and selected time window.
- Simple and composite breakpoint definitions and enabled state.
- Bookmarks, notes, observations, and findings.
- Selected channels, chart layout, and other relevant inspection context.

Investigation persistence and restoration are **implemented for the CSV workspace flow** (save, reopen, bookmarks, layout); project-scoped lifecycle and analysis integration remain incremental work.

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
CSV Ingestion & Type Inference
        |
        v
Recording + Typed Channel Metadata
        |
        v
Telemetry Reader / Replay Engine <---- Playback Controller
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
Recording      Investigations    Analysis
Dashboard                        (planned)
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
8. **No LLM in the product.** Replay, breakpoints, and investigations are deterministic; the app does not call language models or expose assistant chat. See [docs/PRODUCT.md](docs/PRODUCT.md).
9. **Independent implementation.** MetricBreak code is designed and written independently, not copied or adapted from external codebases.

## Technology

- C# / .NET 10
- ASP.NET Core API
- React / TypeScript with Material UI and ECharts
- Local filesystem persistence; SQLite remains a possible local metadata option, not a confirmed requirement
- CSV as the initial import format
- Modular, testable domain, ingestion, replay, breakpoint, API, and UI components

## Roadmap

### Implemented foundation

- [x] Establish recording, channel, sample, and replay domain foundations.
- [x] Implement typed CSV ingestion and recording sessions, including missing values.
- [x] Implement replay seek and step operations.
- [x] Implement forward and backward breakpoint traversal in the replay engine.
- [x] Implement numeric conditional breakpoints.
- [x] Implement composite `AND`/`OR` breakpoints and API integration.
- [x] Add composite breakpoint management to the React UI.
- [x] Establish project workspace creation/opening and local persistence foundations.
- [x] Build the initial browser-based recording/debugging interface.
- [x] Implement forward/reverse playback with adjustable speed and time/sample pacing.
- [x] Expose typed channel metadata and sample values for inspection.
- [x] Add paginated numeric-only chart plotting, missing-data gaps, replay scrubbing, and context-menu sample actions.
- [x] Add missing-data breakpoints, breakpoint coverage, and right-click breakpoint actions on the current sample.
- [x] Show imported CSV events and stages on the recording timeline.
- [x] Count every sample interval in the dashboard duration.

### Complete the CSV MVP

- [x] Extend simple/composite breakpoint evaluation and UI to Boolean and categorical conditions.
- [x] Investigation persistence, restoration, bookmarks, chart layout in workspace (notes/description supported; project-scoped delete pending).
- [ ] Add initial Analysis functionality: statistics, time-window inspection, and multi-channel comparison.
- [ ] Add categorical state timelines derived from channel values.
- [ ] Validate and complete project-free quick analysis and Save as Project.
- [ ] Test the full CSV workflow end to end, including large files, malformed data, recovery, and performance.

### After the CSV MVP

- [ ] Add two-recording comparison, alignment, and divergence rules.
- [ ] Expand telemetry search and data-quality diagnostics.
- [ ] Evaluate other recording and streaming data sources behind stable ingestion abstractions.
- [ ] Evaluate optional external integrations (excluding LLM inference; see [docs/PRODUCT.md](docs/PRODUCT.md)).

## Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)** for setup and pull request expectations.

All changes must follow **[docs/ORIGINATION.md](docs/ORIGINATION.md)** (original authorship, no pasted third-party source, vendored files documented in [docs/THIRD_PARTY_NOTICES.md](docs/THIRD_PARTY_NOTICES.md)). Pull requests use the checklist in [.github/pull_request_template.md](.github/pull_request_template.md).

## License

Licensed under the [Apache License, Version 2.0](LICENSE).

Copyright © 2026 OPV Software (subject to confirmation of the legal rights holder).
