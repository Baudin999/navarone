# Loading

`SolutionLoader`: opens a `.sln` or `.slnx` with `MSBuildWorkspace`. MSBuild evaluates the projects, so Roslyn compiles them with the same references the real build uses, and every symbol resolves. Every failure MSBuild reports goes to stderr, since code it could not read may be missing from the facts, even when it names a project that loaded. Only a NuGet audit warning, NU1900 to NU1905, does not: it says a package has a known vulnerability, not that code is missing. MSBuild passes the warning on without its code, so its text is matched. Whether it reported a failure is returned with the solution, so the provider marks such facts incomplete.

Turning the solution into facts does not happen here. It happens in `Facts/`.
