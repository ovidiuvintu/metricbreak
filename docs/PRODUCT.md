# Product boundaries

MetricBreak is an offline-first recording debugger. The product does **not** include large language models (LLMs), chat assistants, cloud inference, embeddings, or semantic search over telemetry.

## In scope

- Deterministic replay, breakpoints, investigations, bookmarks, and chart inspection
- Local filesystem persistence (recordings, investigations, optional projects)
- Typed CSV ingestion and channel visualization

## Explicitly out of scope

- LLM or generative-AI features in the API, web UI, or desktop shell
- MCP or similar agent protocols exposed by the running application
- Sending recording data to external inference services

Engineering tools used while developing MetricBreak (editors, linters, etc.) are outside this document. **Shipped artifacts** must remain LLM-free unless this policy is revised in a deliberate product change.