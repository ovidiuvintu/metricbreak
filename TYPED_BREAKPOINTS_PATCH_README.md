# MetricBreak typed breakpoint patch

Apply files at repository root, preserving paths. This patch is a source-level implementation candidate, **not verified by a .NET build** (dotnet SDK unavailable in this environment).

Adds typed Boolean/categorical equality and inequality for single and AND/OR composite breakpoints; existing numeric thresholds and edge operators remain supported. Missing typed values do not match. Edge detection rejects nonadjacent sequences. API requests continue to accept `threshold` and add optional string `value` for typed comparisons. UI adds typed value controls.

**Known remaining work before production merge:** Investigation export/import schema currently stores only numeric `threshold` and does not serialize `ExpectedValue`; typed equality breakpoints will not round-trip through investigation export/import. Update its schema and migration/compatibility tests before using export/import with typed breakpoints. The patch does not introduce a polymorphic rule tree; it extends the existing nonnested AND/OR structure. Existing regression suite and frontend build must be run locally.

Suggested checks: `dotnet test`, `cd src/MetricBreak.Web && npm run build`. Test categorical case sensitivity, Boolean equality, numeric comparisons, forward/reverse edge boundaries, missing values, composite rules, API validation, and investigation export/import.
