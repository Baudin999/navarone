---
kind: information
subjects: [architecture, signal]
named-by: user
born: 2026-10-28
session: 50a7b169-0316-4b4c-8f22-a3350ebb76ba
---
# An engine, language providers and metrics

Navarone has three parts. The engine is language agnostic: it runs the metrics, holds the reasons and reports. A language provider knows one language: it parses it and hands the engine the facts the metrics need. A metric computes over those facts and never sees a language. Adding a language means implementing a provider; the engine and the metrics do not change.

## Why
So we can implement a language provider, parse the language and get the metrics out, without the engine learning any language.

## Edges
- part-of -> navarone
