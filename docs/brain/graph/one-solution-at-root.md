---
kind: information
subjects: [architecture, csharp]
named-by: claude
born: 2026-10-29
session: 196fa3f9-4b7f-4eb9-94f7-c4d2bbc75573
---
# Navarone analyses per solution, each with all its projects, wherever the solution sits in the repo

navarone analyses every .sln or .slnx anywhere under the repo root, skipping bin, obj, .git and other hidden directories. Each solution is analysed on its own, with all the projects within it. A project that two solutions share is reported once. A repo with no solution anywhere is a usage error and exits 2.

## Why
A solution is the unit a C# developer works in: it holds the projects that belong together, so Navarone reads the code the way the solution defines it. Real repos keep their solution in a subfolder, such as CkLang-Bootstrap's src/CkLang.slnx, and a rule that only looked at the root left Navarone unable to run there.

## Edges
