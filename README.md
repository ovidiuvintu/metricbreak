# MetricBreak

**A debugger for recorded time-series telemetry**  
*Find the event. Replay the evidence. Understand what changed before it.*

MetricBreak helps engineers investigate long, multichannel recordings. Instead of only viewing a chart, you can define conditions, jump to matching samples, replay forward or backward, inspect related channels, and preserve the investigation.

> **Status:** Active development. MetricBreak is a strong technical-alpha / near-MVP foundation for CSV-based telemetry investigation. The core replay and breakpoint workflow exists, but end-to-end validation, large-file testing, packaging, and release hardening remain in progress. This is not yet a production-readiness claim.

## The problem

When a failure appears in recorded telemetry, engineers often need to answer questions such as:

- When did pressure first cross its limit?
- What changed immediately before the failure?
- Was a valve `ON` when pressure spiked?
- Did the abnormal condition persist, or was it a single sample?
- Was a missing-data period part of the incident?
- Can the exact evidence be saved and shared with another engineer?

Traditional charts are useful for seeing trends, but they make these questions slow to answer when recordings are long and many channels interact.

## The core workflow

1. Import a CSV recording.
2. Select the channels to inspect.
3. Define a conditional, missing-data, or composite breakpoint.
4. Run or debug the recording.
5. Stop at a matching event.
6. Traverse forward or backward through the recorded samples.
7. Inspect values before and after the event.
8. Bookmark the finding and save the investigation.

Reverse traversal is not just reverse playback. Its purpose is to help answer **“when did this condition begin?”** and **“what changed before this event?”**

## Why MetricBreak?

MetricBreak is designed for **post-event investigation**, not live monitoring or dashboard construction.

Its differentiating idea is breakpoint-driven replay:

- Find a meaningful event instead of manually searching a chart.
- Move backward to locate the beginning of a condition.
- Evaluate breakpoints deterministically in either direction.
- Inspect typed values without converting states into misleading numbers.
- Preserve the recording context and evidence for later review.

The initial product is deliberately offline-first and does not require cloud services, telemetry streaming, or AI inference.

## Current capabilities

### Recording and ingestion

- Typed CSV ingestion for numeric, Boolean, categorical, and missing values.
- Timestamp selection and format handling for ambiguous CSV files.
- Preservation of source sample order, including duplicate timestamps.
- Explicit missing-value semantics; missing readings are not fabricated as zeroes.
- Recording/session management through an ASP.NET Core API.
- Sample recordings covering industrial, scientific, infrastructure, health, finance, and other telemetry-shaped data.

### Replay and breakpoints

- Deterministic seek, step, and replay operations.
- Forward and reverse traversal.
- Play/pause with adjustable speed.
- Timestamp-based and sample-based playback pacing.
- Numeric threshold and edge conditions.
- Boolean and categorical equality, inequality, and change conditions.
- Missing-data conditions for recording gaps and prolonged empty channels.
- Composite `AND` and `OR` breakpoints.
- Breakpoint hit navigation and coverage information.
- Breakpoint evaluation remains authoritative even when chart rendering is throttled.

### Inspection and visualization

- Numeric channel plotting with channel selection and series colors.
- Paginated sample windows and chart zooming.
- Replay cursor and scrubber.
- Values, channels, breakpoint, and timeline inspection tabs.
- Before/after value inspection around the current sample.
- Imported events and stages on the recording timeline.
- Context-menu actions for inspecting, bookmarking, copying, and adding breakpoints.
- Boolean and categorical values remain inspectable even when they are not plotted as numeric series.

### Investigations and projects

- Quick CSV analysis without creating a project.
- Explicit Save as Project workflow for work that should be resumed.
- Local project manifests under `.metricbreak`.
- Named investigations with bookmarks, notes, findings, replay position, selected channels, and chart layout.
- Investigation restoration and URL deep links for the CSV workspace flow.
- Local filesystem persistence with no required cloud account.

### Analysis

The initial analysis surface currently includes:

- Window-based channel statistics.
- Sampling-health and cadence checks.
- Numeric channel comparisons and correlations.
- Navigation from the analysis surface back to a sample.

Analysis remains an area for expansion. The most valuable next steps are event-centered before/after analysis, state-transition timelines, and comparison of a failing recording with a known-good recording.

## MVP assessment

MetricBreak is closest to a **CSV-based technical MVP**, not a finished production product.

| Area | Current assessment |
| --- | --- |
| Replay and breakpoint engine | Strong technical foundation |
| CSV investigation workflow | Mostly implemented |
| Investigation persistence | Implemented foundation; further hardening needed |
| Analysis | Initial functionality implemented; event-centered analysis remains |
| Browser and end-to-end validation | Incomplete |
| Large-file and performance validation | Incomplete |
| Packaging and installation | Not yet a polished release experience |

Before calling the CSV MVP complete, the project needs to prove the full workflow with browser tests, malformed-input recovery, large-file behavior, and investigation save/reopen scenarios.

## Intended first users

MetricBreak is intentionally domain-neutral in its architecture, but the first product audience should be narrower:

- Test and validation engineers.
- Industrial automation and controls engineers.
- Instrumentation and laboratory engineers.
- Robotics and vehicle telemetry teams.
- SRE and observability engineers investigating recorded incidents.

The strongest initial use case is a long, multichannel recording where the failure is known but the beginning and preceding cause are not.

## Positioning

Recommended product description:

> **MetricBreak helps engineers find what happened immediately before a failure in recorded telemetry. Set a breakpoint, replay forward or backward, inspect related channels, and save the evidence.**

MetricBreak should not initially position itself as a replacement for Grafana, Datadog, MATLAB, or full measurement-analysis suites. Those products solve broader or different problems. MetricBreak should focus on the investigation workflow those tools do not make central: breakpoint-driven replay of recorded data.

## Similar and complementary products

MetricBreak overlaps with several categories, but is not a direct clone of any one product:

- **Grafana, Datadog, and Splunk:** strong observability, dashboards, search, and incident workflows; generally not centered on deterministic sample-level forward/backward replay.
- **MATLAB/Simulink Data Inspector:** strong signal inspection and run comparison; typically more powerful but heavier for a simple CSV investigation workflow.
- **NI DIAdem and measurement-analysis tools:** strong engineering test-data capabilities; MetricBreak aims to be lighter-weight and debugger-oriented.
- **Jupyter, pandas, Polars, and DuckDB:** flexible and powerful analytical alternatives; MetricBreak provides a visual, no-code starting point with replay and breakpoint semantics.

These products validate that the problem exists. MetricBreak needs to win on investigation speed, simplicity, and reproducible evidence for a focused audience.

## Roadmap

### Complete the CSV MVP

- [x] Typed CSV ingestion and recording sessions.
- [x] Deterministic forward and reverse replay.
- [x] Numeric, Boolean, categorical, composite, and missing-data breakpoints.
- [x] Recording dashboard with chart and sample inspection.
- [x] Quick CSV workflow without requiring a project.
- [x] Investigation persistence foundation, bookmarks, notes, and layout restoration.
- [x] Initial statistics, sampling-health, and channel comparison analysis.
- [ ] Browser end-to-end coverage for the complete CSV workflow.
- [ ] Malformed CSV, encoding, mixed-type, recovery, and cancellation handling.
- [ ] Large-file performance and memory validation with explicit targets.
- [ ] Versioned investigation schema and migration handling.
- [ ] Polished local packaging and one-step startup.

### Make investigations shareable

- [ ] Export a human-readable Markdown or HTML investigation report.
- [ ] Include breakpoint definitions, hit timestamps, surrounding samples, notes, and source integrity metadata.
- [ ] Add event-centered “what changed before this?” analysis.
- [ ] Add categorical and Boolean state-transition timelines.
- [ ] Improve portability of projects between machines.

### After the CSV MVP

- [ ] Compare two recordings and align their timelines.
- [ ] Detect divergence from a known-good recording.
- [ ] Expand data-quality diagnostics and telemetry search.
- [ ] Evaluate Parquet, database, and other recording formats.
- [ ] Evaluate streaming sources only after the recorded-data workflow is validated.
- [ ] Add optional issue-tracker references and integrations.

## Validation and launch plan

The next validation goal is not broad advertising. It is to work with 5–10 engineers from one initial segment and measure whether MetricBreak is faster than their current workflow.

A useful evaluation asks whether a new user can:

1. Import a recording without assistance.
2. Define a useful condition.
3. Find an event and traverse backward.
4. Identify a plausible preceding change.
5. Save and explain the evidence.

Potential launch channels after this workflow is stable:

- GitHub with sample recordings and a short demo.
- Hacker News through a technical Show HN post.
- Targeted LinkedIn outreach to test, controls, instrumentation, robotics, and observability engineers.
- Relevant engineering communities and meetups.
- Technical articles about recorded-telemetry investigation and deterministic replay.
- Product Hunt after packaging and onboarding are ready.

Avoid broad paid advertising until a specific audience repeatedly finds value in the product.

## Architecture

```text
CSV Source
    |
    v
CSV Ingestion & Type Inference
    |
    v
Recording + Typed Channel Metadata
    |
    v
Telemetry Reader / Replay Engine <---- Playback Controller
    |                                      |
    v                                      v
Breakpoint Evaluation                Direction / Clock / Speed
    |
    v
ASP.NET Core API
    |
    v
React / TypeScript UI
    |              |              |
Recording     Investigations   Analysis
Dashboard
```

The replay engine, recording model, breakpoint evaluation, and analysis logic are independent of projects. Projects organize references and saved investigation state; they do not own or silently delete the original recording source.

## Design principles

1. **Deterministic by default.** Identical recordings, rules, and sample positions produce identical evaluations regardless of navigation direction.
2. **Immutable source telemetry.** Debugging operations do not alter the original recording.
3. **Typed data remains typed.** Numeric, Boolean, categorical, and missing values retain their semantics.
4. **Breakpoint correctness over rendering speed.** UI batching must not skip breakpoint evaluation.
5. **Offline-first.** Core workflows do not require cloud services or external inference APIs.
6. **Projects are optional.** A one-off CSV can be opened without creating a project.
7. **Industry-neutral core, focused initial market.** The architecture is general, but the first user segment must be specific.
8. **No LLM in the product.** Replay, breakpoints, and investigations are deterministic; shipped artifacts do not call language models or expose assistant chat.

## Technology

- C# / .NET 10
- ASP.NET Core API
- React / TypeScript with Material UI and ECharts
- Local filesystem persistence
- CSV as the initial import format
- Modular domain, ingestion, replay, API, and UI components

## Development setup

### API

The API project is `src/MetricBreak.Api`.

### Web application

The web application is `src/MetricBreak.Web`.

```text
cd src/MetricBreak.Web
npm install
npm run dev
```

### Tests

Test projects are located under `tests/`. The repository includes domain and API tests covering ingestion, replay, breakpoints, investigations, and project persistence. Full browser and end-to-end validation remains a release task.

See [CONTRIBUTING.md](CONTRIBUTING.md) for development and contribution guidance.

## Contributing

All changes must follow [docs/ORIGINATION.md](docs/ORIGINATION.md). Pull requests should explain the problem solved, testing performed, and any impact on replay or breakpoint behavior. See [.github/pull_request_template.md](.github/pull_request_template.md).

## License

Licensed under the [Apache License, Version 2.0](LICENSE).

Copyright © 2026 OPV Software (subject to confirmation of the legal rights holder).
