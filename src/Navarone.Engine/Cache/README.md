# Cache

The facts cache. `Code.FactsAsync` is the one seam every command that reads the code goes through, so the cache sits there and serves them all. It keeps only facts the provider loaded completely: facts marked incomplete, such as those of an unrestored project, are loaded again by the next command. SPEC.md, "The facts cache", holds what it promises.

- `FactsKey.cs`: the version of the code as one SHA-256 hash: HEAD, the path and contents of every file that differs from it, navarone.json, the solutions loaded and the build of Navarone, the module version id of the engine's and the provider's assemblies. No key outside a git repository, in one with no commit yet, or when git status fails.
- `FactsCache.cs`: one entry per key in `.navarone/` at the repo root. A read that fails is a miss; a write goes through `AtomicFile` after the oldest entries are deleted, so at most 10 remain. The folder holds a `.gitignore` of its own that ignores everything in it, so an entry never shows in git status and never changes the key.
- `FactsJson.cs`: facts as an entry's JSON. Reading is strict, so an entry of another shape throws and is a miss. A control flow fact's `PartOf` is written as the place of its whole in the method's list and read back as that very fact, because the cyclomatic metric tells the parts of a fact by reference.
- `CacheIgnore.cs`: the `.navarone/` line `navarone init` adds to the repo's `.gitignore`.
