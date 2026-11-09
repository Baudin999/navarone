using Microsoft.CodeAnalysis;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// What the bodies of a method or an initializer hold: their control flow, whether a lone body is a flat dispatch,
/// the calls and field uses they make in the order written, and their tokens.
/// </summary>
internal sealed record BodyFacts(
    IReadOnlyList<ControlFlowFact> ControlFlow,
    Dispatch Dispatch,
    IReadOnlyList<CallSite> CallSites,
    IReadOnlyList<SymbolId> FieldUses,
    IReadOnlyList<CodeToken> Tokens)
{
    /// <summary>The facts of the given bodies. Only a member with one body can be a flat dispatch.</summary>
    public static BodyFacts Of(IReadOnlyList<SyntaxNode> bodies, SemanticModel model)
    {
        var dispatch = Dispatch.None;
        if (bodies is [var only])
        {
            dispatch = FlatDispatch.Of(only, model);
        }

        var uses = bodies.SelectMany(body => SymbolUses.Of(body, model)).ToList();
        List<CallSite> callSites = [.. uses.Where(use => !use.IsField).Select(use => new CallSite(use.Symbol, use.Line)).Distinct()];
        List<SymbolId> fieldUses = [.. uses.Where(use => use.IsField).Select(use => use.Symbol).Distinct()];
        List<ControlFlowFact> controlFlow = [.. bodies.SelectMany(ControlFlowReader.Read)];
        var tokens = CodeTokens.Of(bodies);
        return new BodyFacts(controlFlow, dispatch, callSites, fieldUses, tokens);
    }
}
