# Contributing to MetricBreak

Thank you for helping improve MetricBreak. Please read this before opening a pull request.

## Code origination

All contributions must follow **[docs/ORIGINATION.md](docs/ORIGINATION.md)**. In short:

- Write code for MetricBreak; do not paste third-party source without vendoring and notice.
- Match existing project patterns and tests.
- Review AI-generated changes before submitting.

## Development setup

- **API:** `src/MetricBreak.Api` (.NET)
- **Web:** `src/MetricBreak.Web` (React + TypeScript + Vite)
- **Tests:** `tests/MetricBreak.Domain.Tests`, `tests/MetricBreak.Api.Tests`

Run API tests (example):

```bash
dotnet test tests/MetricBreak.Api.Tests/MetricBreak.Api.Tests.csproj
```

Run the web app:

```bash
cd src/MetricBreak.Web
npm install
npm run dev
```

Stop a running `MetricBreak.Api` process before rebuilding if Windows file locks block the build.

## Pull requests

Use the [pull request template](.github/pull_request_template.md). Include:

- What problem the change solves
- How you tested it
- Confirmation of the **origination checklist**

Keep PRs focused; prefer incremental changes that do not disrupt replay and breakpoint behavior.

## License

By contributing, you agree that your contributions are licensed under the same terms as the project ([Apache License 2.0](LICENSE)), unless you explicitly state otherwise and the maintainers accept it.