using System.Globalization;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Engine.Metrics;

namespace Navarone.Metrics.Cognitive;

/// <summary>Campbell: one for each break in the straight line of a method, plus one for each level of nesting.</summary>
public sealed class CognitiveMetric : ISignal
{
    /// <inheritdoc />
    public string Id => "cognitive";

    /// <inheritdoc />
    public SubjectKind Subject => SubjectKind.Method;

    /// <summary>The cognitive value of every method, whatever the limit. A method in a call cycle, direct or indirect, pays one.</summary>
    public static IReadOnlyList<(MethodFacts Method, int Value)> ValuesOf(CodebaseFacts facts)
    {
        var methods = facts.Types.SelectMany(type => type.Methods).ToList();
        var recursive = InCycle(methods);
        return [.. methods.Select(method => (method, method.ControlFlow.Sum(Increment) + (recursive.Contains(method.Symbol) ? 1 : 0)))];
    }

    /// <inheritdoc />
    public IReadOnlyList<Finding> Measure(CodebaseFacts facts, CheckLimits limits)
    {
        var limit = limits.For(Id);
        return
        [
            .. from entry in ValuesOf(facts)
               where entry.Value > limit
               select new Finding(Id, entry.Method.Symbol, Subject, entry.Method.Location, entry.Value, limit, Question(entry.Value, limit)),
        ];
    }

    // The methods that can reach themselves through calls: those in a strongly connected component of more than one
    // method, and those that call themselves. Tarjan's algorithm, iterative so a long call chain cannot overflow the stack.
    private static HashSet<SymbolId> InCycle(IReadOnlyList<MethodFacts> methods)
    {
        var calls = methods.GroupBy(method => method.Symbol).ToDictionary(group => group.Key, group => group.SelectMany(method => method.Calls).Distinct().ToList());
        var index = new Dictionary<SymbolId, int>();
        var low = new Dictionary<SymbolId, int>();
        var onStack = new HashSet<SymbolId>();
        var stack = new Stack<SymbolId>();
        var cyclic = new HashSet<SymbolId>();
        foreach (var root in calls.Keys.Where(key => !index.ContainsKey(key)))
        {
            var work = new Stack<(SymbolId Node, int Next)>();
            Enter(root);
            work.Push((root, 0));
            while (work.Count > 0)
            {
                var (node, next) = work.Pop();
                if (next < calls[node].Count)
                {
                    work.Push((node, next + 1));
                    var callee = calls[node][next];
                    if (!calls.ContainsKey(callee))
                    {
                        continue;
                    }

                    if (callee == node)
                    {
                        cyclic.Add(node);
                    }

                    if (!index.TryGetValue(callee, out var calleeIndex))
                    {
                        Enter(callee);
                        work.Push((callee, 0));
                    }
                    else if (onStack.Contains(callee))
                    {
                        low[node] = Math.Min(low[node], calleeIndex);
                    }

                    continue;
                }

                if (work.Count > 0)
                {
                    var parent = work.Peek().Node;
                    low[parent] = Math.Min(low[parent], low[node]);
                }

                if (low[node] == index[node])
                {
                    var component = new List<SymbolId>();
                    SymbolId member;
                    do
                    {
                        member = stack.Pop();
                        onStack.Remove(member);
                        component.Add(member);
                    }
                    while (member != node);

                    if (component.Count > 1)
                    {
                        cyclic.UnionWith(component);
                    }
                }
            }
        }

        return cyclic;

        void Enter(SymbolId node)
        {
            index[node] = low[node] = index.Count;
            stack.Push(node);
            onStack.Add(node);
        }
    }

    // Structures that nest cost one plus their depth. An else, an else if, a jump, a when, a pattern test and a new run
    // of one operator cost one flat. The cases of a switch cost nothing: the switch pays once.
    // navarone: 0000016
    private static int Increment(ControlFlowFact fact) => fact.Kind switch
    {
        ControlFlowKind.Branch when fact.Continues => 1,
        ControlFlowKind.Branch or ControlFlowKind.Switch or ControlFlowKind.Loop or ControlFlowKind.Catch or ControlFlowKind.Conditional
            => 1 + fact.NestingDepth,
        ControlFlowKind.Else or ControlFlowKind.Jump or ControlFlowKind.Guard or ControlFlowKind.PatternTest => 1,
        ControlFlowKind.LogicalAnd or ControlFlowKind.LogicalOr when fact.Continues => 0,
        ControlFlowKind.LogicalAnd or ControlFlowKind.LogicalOr => 1,
        _ => 0,
    };

    private static string Question(int value, int limit) => string.Create(
        CultureInfo.InvariantCulture,
        $"cognitive complexity {value} is above the limit of {limit}. Why is this method this hard to read?");
}
