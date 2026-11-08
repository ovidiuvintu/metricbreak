# MetricBreak

**The Time-Series Debugger**

*Replay. Break. Inspect.*

MetricBreak is an offline-first, industry-independent debugger for recorded time-series telemetry. It aims to help engineers investigate what happened, identify when conditions occurred, compare operational runs, and preserve reproducible evidence.

> **Project status:** Early development. The capabilities below describe the intended product, not features already implemented.

## Why MetricBreak?

Charts show measurements over time. MetricBreak aims to let you **debug** those measurements: navigate backward and forward, stop on meaningful conditions, inspect the exact values and rule states, and compare one run with another.

Example investigation questions:

- Where did pressure fall below 4,000 psi?
- When did pressure exceed 9,000 psi while flow rate remained below 50 bpm for five seconds?
- How did an abnormal treatment differ from a known-good treatment?
- What happened immediately before a breakpoint triggered?

## Planned capabilities

- **Bidirectional replay:** Play, step, seek, and continue to breakpoints in either direction.
- **Conditional breakpoints:** Find measurements and time intervals that satisfy deterministic rules.
- **Explainable rule evaluation:** Inspect the values and rule state at a selected sample.
- **Telemetry search:** Locate matching samples and intervals, with a structured query interface.
- **Run comparison:** Align two recordings, inspect differences, and break on divergence.
- **Data-quality inspection:** Identify duplicate timestamps, gaps, and frozen measurements without modifying source data.
- **Reproducible investigations:** Save, reopen, and share evidence with recording and rule provenance.
- **Optional project workspaces:** Organize recordings, investigations, comparisons, breakpoints, annotations, and external issue references in portable projects, without requiring a project for quick debugging.
- **Industry profiles:** Configure channels, units, mappings, and events for different domains.
- **Offline-first operation:** Run on a workstation or customer-managed infrastructure without required cloud services.

## Quick analysis and project workspaces

MetricBreak is designed to work both **with and without a project**, much like a development environment that can open an individual source file without first creating a solution.

### Quick analysis — no project required

Open a telemetry recording (initially CSV) and start inspecting it immediately. Replay, seek, step backward or forward, search measurements, set temporary breakpoints, and compare recordings without creating a project. This is the lowest-friction path for one-off investigations.

### Project workspace — save and organize ongoing work

Create or open a MetricBreak project (`.mbproj`, proposed format) to keep related engineering work together. A project may reference:

- **Recordings:** One or more telemetry runs and their source identities.
- **Investigations:** Saved debugging sessions, cursor positions, findings, and annotations.
- **Breakpoints and rules:** Reusable conditions and their versioned definitions.
- **Comparisons:** Selected recordings, alignment settings, and saved divergence investigations.
- **Bookmarks and events:** Points and intervals of interest.
- **External issues:** Jira tickets or other issue-tracker references associated with investigations, including submission metadata; integrations are future work.
- **Reports and settings:** Exported evidence, industry profiles, channel mappings, and project configuration.

An engineer can begin in Quick Analysis and later choose **Save as Project** to preserve the investigation. Opening a project restores its saved context. A project is a **general-purpose workspace**, not an oil-and-gas-specific hierarchy; it may represent a well, test campaign, production line, or other related collection of telemetry.

### Architectural boundary

The replay, rule evaluation, and comparison engines **must not depend on projects**. A `DebugSession` can exist independently; a project stores references to recordings and persistent investigation state. Removing a recording from a project must not silently delete its original source data. Project files should remain portable and support both managed copies and externally referenced large recordings, with integrity checks where appropriate.

**Foundational requirement:** Users must be able to open, replay, inspect, and debug telemetry recordings without creating a project. Projects add persistence and organization, not a prerequisite for debugging.

## Design principles

1. **Deterministic by default.** Identical recordings, rules, and positions yield identical results regardless of navigation direction.
2. **Immutable source telemetry.** Imports preserve original data and provenance.
3. **Industry-neutral core.** Oil and gas, aviation, manufacturing, and other domains use profiles rather than hardcoded assumptions.
4. **Local-first.** Core workflows do not require network connectivity or external inference APIs.
5. **Projects are optional.** Opening and debugging a recording never requires creating a project; saved workspaces organize ongoing investigations.
6. **AI is optional.** Any future local AI assistant may interpret requests, but the engine remains responsible for calculations, rule evaluation, and evidence.

## Proposed technology

- C# / .NET 10
- ASP.NET Core API
- React / TypeScript UI
- SQLite for local metadata
- CSV as the initial import format
- Modular monolith with testable domain, ingestion, replay, rules, and comparison components

The implementation and repository layout will evolve as the first milestones are built.

## Initial roadmap

- [ ] Define recording, channel, sample, and independent debug-session domain models
- [ ] Define the project manifest and create/open/save project workflow
- [ ] Support opening and debugging a CSV without creating a project
- [ ] Import CSV telemetry and preserve source identity
- [ ] Implement bidirectional deterministic replay
- [ ] Add conditional breakpoints and rule inspection
- [ ] Add two-run comparison and divergence rules
- [ ] Save and reopen reproducible investigations within projects
- [ ] Support Save as Project from a quick-analysis session
- [ ] Build a browser-based debugging interface
- [ ] Evaluate optional integrations and local AI separately

## Contributing

The project is at an early stage. Contribution guidelines and development setup instructions will be added as the architecture stabilizes. Please use GitHub Issues to propose changes or report problems once issue tracking is enabled.

## License

Licensed under the [Apache License, Version 2.0](LICENSE).

Copyright © 2026 OPV Software (subject to confirmation of the legal rights holder).
