using Navarone.Classifiers.Deterministic;
using Navarone.Engine.Classification;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Engine.Reasons;

namespace Navarone.Tests.Classifiers;

public sealed class DeterministicReasonClassifierTests
{
    private const string Why = "a reason for a split names every helper that adds a decision";

    private static readonly Helper[] Helpers = [new(new("Accepted(A)"), "Accepted", 2), new(new("Verdict(A)"), "Verdict", 4), new(new("Fooling(string)"), "Fooling", 0)];

    [Fact]
    public void AReasonForASplitThatLeavesOutHelpersThatAddDecisionsIsRejectedNamingThem()
    {
        var rejection = Rejection("the run only asks the parser and keeps every answer it reads as it is", Helpers);

        Assert.Equal($"the reason does not answer for the helpers Accepted, Verdict; {Why}", rejection);
    }

    [Theory]
    [InlineData("Accepted and Verdict each read one part of the answer the parser gives back")]
    [InlineData("`Accepted` and `Verdict()` each read one part of the answer the parser gives back")]
    [InlineData("Accepted(A) and Verdict(A) each read one part of the answer the parser gives back")]
    public void AReasonThatNamesEveryHelperThatAddsADecisionIsNotRejected(string reason)
    {
        Assert.Null(Rejection(reason, Helpers));
    }

    [Theory]
    [InlineData("accepted and Verdict each read one part of the answer the parser gives back")]
    [InlineData("IsAccepted and Verdict each read one part of the answer the parser gives back")]
    [InlineData("Accepted2 and Verdict each read one part of the answer the parser gives back")]
    public void AHelperIsNamedOnlyByItsWholeNameInItsOwnCase(string reason)
    {
        Assert.Contains("the helpers Accepted;", Rejection(reason, Helpers), StringComparison.Ordinal);
    }

    [Fact]
    public void OneMentionCoversEveryOverloadOfAHelper()
    {
        Helper[] overloads = [new(new("Read(int)"), "Read", 1), new(new("Read(string)"), "Read", 3)];

        Assert.Null(Rejection("Read parses one token of either kind the lexer hands it", overloads));
        Assert.EndsWith($"the helpers Read; {Why}", Rejection("the parser reads one token of either kind the lexer hands it", overloads), StringComparison.Ordinal);
    }

    [Fact]
    public void AFindingWithoutHelpersNeedsNoNames()
    {
        Assert.Null(Rejection("a flat dispatch over the eleven token kinds the lexer emits", null));
    }

    [Fact]
    public void AReasonOfSevenWordsIsRejectedAndOneOfEightIsNot()
    {
        Assert.Equal("the reason has 7 words; give at least 8", Rejection("one switch reads the eleven token kinds", null));
        Assert.Null(Rejection("one switch reads all the eleven token kinds", null));
    }

    private static string? Rejection(string reason, IReadOnlyList<Helper>? helpers)
    {
        var finding = new Finding("split-complexity", new SymbolId("Run()"), SubjectKind.Method, new SourceLocation("T.cs", 1, 1), 14, 10, "why?", helpers);
        var problem = new RecordedProblem(new ProblemId("0000001"), finding.Check, finding.Subject, new CodeFingerprint("f"), 14, null);
        return new DeterministicReasonClassifier().Rejection(new ReasonCase(finding, problem, ["0000001"], reason, [problem]));
    }
}
