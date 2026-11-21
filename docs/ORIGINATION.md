# Code origination policy

MetricBreak is developed as an **original codebase**. This policy applies to all changes merged into the repository.

## Requirements

1. **Authored for MetricBreak**  
   Application source (C#, TypeScript, tests, and project-specific configuration) must be written for this product or by contributors who assign or license it under the repository [LICENSE](../LICENSE).

2. **No pasted third-party source**  
   Do not copy source code from other repositories, gists, tutorials, Stack Overflow answers, generated code dumps, or vendor SDKs into the tree unless the copy is explicitly allowed and documented (see vendoring).

3. **Dependencies are not copies**  
   Normal package dependencies (NuGet, npm) are fine. Do not vendor their source into `src/` without following the vendoring rules below.

4. **Reuse inside this repo is encouraged**  
   Following existing patterns (`ProjectWorkspaceStore`, `InvestigationEndpoints`, API test helpers, MUI layout conventions) is required; that is not “external copying.”

5. **Contributor tooling**  
   Editors and assistants used while developing are fine; you are responsible for reviewing output before commit. **Shipped product code** must not add LLM features; see [PRODUCT.md](./PRODUCT.md).

## Vendoring (rare)

If you must include **files** from a third party (fonts, schemas, small utilities):

- Record them in [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md) (name, version, license, URL).
- Keep vendored files in a clearly named directory (e.g. `third_party/`).
- Prefer packages over copying source when possible.

## Pull requests

Every PR must confirm the [origination checklist](../.github/pull_request_template.md) in the template.

## Reviewer expectations

Reviewers should treat unexplained large pasted blocks, missing attribution for vendored files, or unfamiliar license headers as **blockers** until resolved.

## Questions

If you are unsure whether a dependency or snippet is acceptable, open a discussion before merging large changes.