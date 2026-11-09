using Navarone.Engine.Classification;
using Navarone.Engine.Clashes;
using Navarone.Engine.Events;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Engine.Lenses;
using Navarone.Engine.Reasons;

namespace Navarone.Engine;

/// <summary>
/// Every problem a check found in the code as it is now, the facts and each subject in them, a raised for each id
/// granted, and reasons.json with every problem's id, fingerprint and value and the problems whose code is gone
/// removed. It owns that file: a command saves reasons.json only through it, so no save brings a gone problem back.
/// </summary>
internal sealed class CheckedRepo
{
    private readonly string repoRoot;
    private readonly CodebaseFacts facts;
    private readonly Dictionary<SymbolId, Subject> subjects;
    private readonly List<CheckedProblem> problems;
    private ReasonsFile reasons;

    private CheckedRepo(string repoRoot, CodebaseFacts facts, Dictionary<SymbolId, Subject> subjects, List<CheckedProblem> problems, ReasonsFile reasons, List<RaisedEvent> granted)
    {
        this.repoRoot = repoRoot;
        this.facts = facts;
        this.subjects = subjects;
        this.problems = problems;
        this.reasons = reasons;
        Granted = granted;
    }

    public IReadOnlyList<CheckedProblem> Problems => problems;

    /// <summary>Every problem in reasons.json as this check holds it.</summary>
    public IReadOnlyList<RecordedProblem> RecordedProblems => reasons.Problems;

    public IReadOnlyList<RaisedEvent> Granted { get; }

    /// <summary>
    /// Every problem in the facts, matched against reasons.json at the setup's repo root, with the ids written at each
    /// verdict's code, and a raised for each id it grants. A problem the user kept, by its id, takes the given
    /// resolution for its code as it is now, so it turns green. The problems whose code is gone are removed after the
    /// match, because a renamed problem is found by its gone symbol. It writes nothing.
    /// </summary>
    public static CheckedRepo Of(EngineSetup setup, CodebaseFacts facts, Dictionary<SymbolId, Subject> subjects, IReadOnlyDictionary<string, string> kept)
    {
        var reasons = ReasonsFile.Load(setup.RepoRoot);
        var problems = new List<CheckedProblem>();
        var granted = new List<RaisedEvent>();
        foreach (var (finding, subject, fingerprint) in Measured(setup, facts, subjects))
        {
            var known = reasons.For(finding) ?? reasons.Renamed(finding, subject.Ids, facts);
            RecordedProblem problem;
            if (known is null)
            {
                problem = new RecordedProblem(reasons.NextId(), finding.Check, finding.Subject, fingerprint, finding.Value, null);
                granted.Add(new RaisedEvent(problem.Id.Value, problem.Check, problem.Symbol.Value));
            }
            else
            {
                problem = known with { Symbol = finding.Subject, Fingerprint = fingerprint, Value = finding.Value };
            }

            if (kept.TryGetValue(problem.Id.Value, out var keptBy))
            {
                problem = problem with { Resolution = new Resolution(keptBy, fingerprint, finding.Value) };
            }

            reasons = reasons.With(problem);
            var falsePositive = setup.ProblemClassifier.FalsePositive(new ProblemCase(finding, facts));
            var places = PlacesOf(finding, subject, subjects);
            var verdict = VerdictOf(finding, problem, subject.Ids, falsePositive);
            var isFalsePositive = falsePositive is not null;
            problems.Add(new CheckedProblem(verdict, subject.Ids, places, isFalsePositive, subject.Text));
        }

        var standing = reasons.WithoutGoneCode(facts);
        return new CheckedRepo(setup.RepoRoot, facts, subjects, problems, standing, granted);
    }

    /// <summary>Every finding of every check, with its subject and the fingerprint of the code a resolution answers.</summary>
    public static IEnumerable<Measurement> Measured(EngineSetup setup, CodebaseFacts facts, Dictionary<SymbolId, Subject> subjects)
    {
        foreach (var check in setup.Checks)
        {
            foreach (var finding in check.Measure(facts, setup.Limits))
            {
                var subject = subjects[finding.Subject];
                var fullCode = CodeOf(finding, subject.Code, subjects);
                yield return new Measurement(finding, subject, CodeFingerprint.Of(finding.Subject, fullCode));
            }
        }
    }

    /// <summary>The problem with the given id that has a finding now, or null.</summary>
    public CheckedProblem? ProblemOf(ProblemId id) => problems.FirstOrDefault(problem => problem.Verdict.Problem.Id == id);

    /// <summary>Whether Navarone granted the given id, a problem of it removed or not.</summary>
    public bool HasGranted(ProblemId id) => reasons.HasGranted(id);

    /// <summary>
    /// The problem reasons.json records under the given id with its subject in the code as it is now, or null when no
    /// problem has that id or its code is gone. Its subject is found by its recorded symbol, so it needs no finding.
    /// </summary>
    public StandingProblem? RecordedOf(ProblemId id)
    {
        var recorded = reasons.For(id);
        if (recorded is null)
        {
            return null;
        }

        var subject = SubjectOf(recorded.Symbol);
        if (subject is null)
        {
            return null;
        }

        return new StandingProblem(recorded, subject);
    }

    /// <summary>The subject with the given symbol in the code as it is now, or null when its code is gone.</summary>
    public Subject? SubjectOf(SymbolId symbol) => subjects.GetValueOrDefault(symbol);

    /// <summary>Stores the given text as the given problem's resolution, for its code as it was checked. Save writes it to reasons.json.</summary>
    public void Resolve(CheckedProblem problem, string text)
    {
        var recorded = problem.Verdict.Problem;
        var resolution = new Resolution(text, recorded.Fingerprint, recorded.Value);
        reasons = reasons.With(recorded with { Resolution = resolution });
    }

    /// <summary>Writes reasons.json as this check holds it.</summary>
    public void Save() => reasons.Save(repoRoot);

    /// <summary>
    /// The code of the subject with the given symbol that a reason defends, stored, where it is, the given line of the
    /// finding, and the lens that shows it.
    /// </summary>
    public DefendedCode Defended(NavaroneEventHandler events, SymbolId symbol, int line)
    {
        var subject = subjects[symbol];
        var start = subject.Places[0];
        var stored = events.Store(subject.Text);
        var context = ContextOf(symbol);
        return new DefendedCode(stored, start.Path, start.Line, line, context);
    }

    // The lens that makes the method with the given symbol its subject, or null for a class or a method a lens cannot
    // name.
    private string? ContextOf(SymbolId symbol)
    {
        var method = facts.Types.SelectMany(type => type.Methods).FirstOrDefault(method => method.Symbol == symbol);
        if (method is null)
        {
            return null;
        }

        return new LensQueries(facts).Of(method)?.Query;
    }

    // Where the finding's code is written: each helper's places, then its subject's.
    private static List<Place> PlacesOf(Finding finding, Subject subject, Dictionary<SymbolId, Subject> subjects)
    {
        var places = new List<Place>();
        foreach (var helper in finding.Helpers ?? [])
        {
            places.AddRange(subjects[helper.Symbol].Places);
        }

        places.AddRange(subject.Places);
        return places;
    }

    // A problem a rule calls a false positive is green without a reason, and the agent is never asked about it. Any
    // other problem is green once its resolution answers its code as it is now and the code carries its id, and red
    // otherwise.
    private static Verdict VerdictOf(Finding finding, RecordedProblem problem, IReadOnlyList<string> idsAtCode, string? falsePositive)
    {
        if (falsePositive is not null)
        {
            return new Verdict(finding, problem, FindingState.Green);
        }

        if (problem.Resolved && idsAtCode.Contains(problem.Id.Value))
        {
            return new Verdict(finding, problem, FindingState.Green);
        }

        return new Verdict(finding, problem, FindingState.Red);
    }

    // The subject's code, then the symbol and code of each helper in symbol order, so any change to a helper changes
    // the fingerprint. A finding without helpers hashes its own code alone.
    private static string CodeOf(Finding finding, string code, Dictionary<SymbolId, Subject> subjects)
    {
        var helpers = (finding.Helpers ?? [])
            .Select(helper => helper.Symbol)
            .OrderBy(helper => helper.Value, StringComparer.Ordinal);
        var parts = new List<string> { code };
        foreach (var helper in helpers)
        {
            parts.Add($"{helper.Value}\n{subjects[helper].Code}");
        }

        return string.Join("\n", parts);
    }
}

/// <summary>
/// A subject's code its fingerprint covers, its text as it stands in its file, the ids written at it and where it is
/// written. A class is written where its first declaration is and where each of its fields and methods is, so every
/// partial declaration's members are its.
/// </summary>
internal sealed record Subject(string Code, string Text, IReadOnlyList<string> Ids, IReadOnlyList<Place> Places)
{
    /// <summary>Its text from where its first place starts.</summary>
    public SubjectCode Written => new(Places[0].Path, Places[0].Line, Text);

    /// <summary>Each subject in the given facts, a type and each of its methods, by its symbol.</summary>
    public static Dictionary<SymbolId, Subject> AllIn(CodebaseFacts facts)
    {
        var subjects = new Dictionary<SymbolId, Subject>();
        foreach (var type in facts.Types)
        {
            List<Place> places = [PlaceOf(type.Location, type.Written)];
            foreach (var field in type.Fields)
            {
                places.Add(PlaceOf(field.Location, field.Written));
            }

            foreach (var method in type.Methods)
            {
                places.Add(PlaceOf(method.Location, method.Written));
            }

            subjects.TryAdd(type.Symbol, new Subject(type.Code, type.Written.Text, type.NavaroneIds, places));
            foreach (var method in type.Methods)
            {
                List<Place> written = [PlaceOf(method.Location, method.Written)];
                subjects.TryAdd(method.Symbol, new Subject(method.Code, method.Written.Text, method.NavaroneIds, written));
            }
        }

        return subjects;
    }

    private static Place PlaceOf(SourceLocation location, Written written) => new(location.Path, written.Line, written.EndLine);
}

/// <summary>Lines Line to EndLine of the file at Path, relative to its codebase's directory.</summary>
internal sealed record Place(string Path, int Line, int EndLine);

/// <summary>A finding with its subject and the fingerprint of its code.</summary>
internal sealed record Measurement(Finding Finding, Subject Subject, CodeFingerprint Fingerprint);

/// <summary>
/// A verdict with the ids written at its problem's code, which the reason classifier reads, where that code and its
/// helpers' code is written, which decides whether a coverage shows it, whether a rule calls it a false positive, and
/// its subject's text, which the event log stores.
/// </summary>
internal sealed record CheckedProblem(Verdict Verdict, IReadOnlyList<string> IdsAtCode, IReadOnlyList<Place> Places, bool FalsePositive, string Text)
{
    /// <summary>Whether the given coverage covers a line of its code or of a helper's.</summary>
    public bool ShownBy(Coverage coverage)
    {
        foreach (var place in Places)
        {
            if (coverage.Covers(place.Path, place.Line, place.EndLine))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>A problem as reasons.json records it, with its subject in the code as it is now.</summary>
internal sealed record StandingProblem(RecordedProblem Problem, Subject Subject);
