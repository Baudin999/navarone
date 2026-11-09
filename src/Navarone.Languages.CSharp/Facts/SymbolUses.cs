using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>One use a body makes of a symbol: a call, or a field use, on the line of the file that names it.</summary>
internal readonly record struct SymbolUse(SymbolId Symbol, bool IsField, int Line);

/// <summary>
/// Reads the uses a body makes of the codebase's symbols from the operations the compiler binds it to, so every use
/// counts whatever syntax writes it: a call, an object creation, a method group, an operator, a user-defined
/// conversion, an indexer, a foreach's enumerator, a collection initializer's Add and a Deconstruct. A method,
/// constructor or non-auto accessor is a call; a field, auto-property or field-like event is a field use, and so is a
/// primary constructor parameter a member captures. A constant is neither, and a nameof uses nothing. A use of the
/// framework counts like any other: a call into it is work.
/// </summary>
internal static class SymbolUses
{
    private static readonly ConditionalWeakTable<ISymbol, StrongBox<bool>> BackedByField = new();

    /// <summary>The uses of the body in the order they are written.</summary>
    public static IEnumerable<SymbolUse> Of(SyntaxNode body, SemanticModel model)
    {
        var uses = new List<(int Position, SymbolUse Use)>();
        foreach (var root in RootsOf(body, model))
        {
            Collect(root, model, uses);
        }

        return uses.OrderBy(use => use.Position).Select(use => use.Use);
    }

    /// <summary>
    /// A class's or struct's primary constructor, whose parameters are state its members capture. A record's become
    /// properties, which its members read instead.
    /// </summary>
    public static bool CapturesItsParameters(IMethodSymbol constructor)
    {
        if (constructor.ContainingType.IsRecord)
        {
            return false;
        }

        return constructor.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is TypeDeclarationSyntax);
    }

    // The outermost operations of the body. A node the compiler binds to no operation of its own, such as a global
    // statement, holds them in its children.
    private static IEnumerable<IOperation> RootsOf(SyntaxNode node, SemanticModel model)
    {
        if (model.GetOperation(node) is { } operation)
        {
            return [operation];
        }

        return node.ChildNodes().SelectMany(child => RootsOf(child, model));
    }

    private static void Collect(IOperation operation, SemanticModel model, List<(int Position, SymbolUse Use)> uses)
    {
        if (operation is INameOfOperation)
        {
            return;
        }

        var used = UsedBy(operation, model).ToList();
        if (used.Count > 0)
        {
            var writer = WriterOf(operation.Syntax);
            var line = writer.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            foreach (var (symbol, isField) in used)
            {
                uses.Add((writer.SpanStart, new SymbolUse(Symbols.Id(symbol), isField, line)));
            }
        }

        foreach (var child in operation.ChildOperations)
        {
            Collect(child, model, uses);
        }
    }

    // The node that writes the use: a call's or a member access's name, so a call in a chain written over several
    // lines is on the line of its name.
    private static SyntaxNode WriterOf(SyntaxNode syntax) => syntax switch
    {
        InvocationExpressionSyntax invocation => WriterOf(invocation.Expression),
        MemberAccessExpressionSyntax access => access.Name,
        MemberBindingExpressionSyntax binding => binding.Name,
        _ => syntax,
    };

    // What the one operation uses, without what its children use.
    private static IEnumerable<(ISymbol Symbol, bool IsField)> UsedBy(IOperation operation, SemanticModel model) => operation switch
    {
        IInvocationOperation invocation => Calls(invocation.TargetMethod),
        IObjectCreationOperation creation => Calls(creation.Constructor),
        IMethodReferenceOperation reference => Calls(reference.Method),
        IUnaryOperation unary => Calls(unary.OperatorMethod),
        IBinaryOperation binary => Calls(binary.OperatorMethod),
        IIncrementOrDecrementOperation step => Calls(step.OperatorMethod),
        ICompoundAssignmentOperation compound => Calls(compound.OperatorMethod),
        IConversionOperation conversion => Calls(conversion.OperatorMethod),
        IEventAssignmentOperation assignment => EventAccessorOf(assignment),
        IForEachLoopOperation loop => EnumerationOf(loop, model),
        IDeconstructionAssignmentOperation deconstruction => DeconstructionOf(deconstruction, model),
        IRecursivePatternOperation pattern => Calls(pattern.DeconstructSymbol as IMethodSymbol),
        IFieldReferenceOperation reference => FieldUseOf(reference.Field),
        IPropertyReferenceOperation reference => PropertyUseOf(reference.Property, AccessorsUsed(reference)),
        IEventReferenceOperation reference => EventUseOf(reference.Event),
        IParameterReferenceOperation parameter => CaptureOf(parameter),
        _ => [],
    };

    private static IEnumerable<(ISymbol Symbol, bool IsField)> Calls(params IMethodSymbol?[] methods)
    {
        foreach (var method in methods)
        {
            if (method is not null)
            {
                yield return (method, false);
            }
        }
    }

    // Adding to a field-like event uses it as a field, which its reference counts; a custom event's add or remove is a
    // call.
    private static IEnumerable<(ISymbol Symbol, bool IsField)> EventAccessorOf(IEventAssignmentOperation assignment)
    {
        if (assignment.EventReference is not IEventReferenceOperation { Event: var @event })
        {
            return [];
        }

        if (IsBackedByField(@event))
        {
            return [];
        }

        if (assignment.Adds)
        {
            return Calls(@event.AddMethod);
        }

        return Calls(@event.RemoveMethod);
    }

    // A foreach calls its enumerator's GetEnumerator, MoveNext and Dispose, reads its Current, and calls a Deconstruct
    // for each element it deconstructs.
    private static IEnumerable<(ISymbol Symbol, bool IsField)> EnumerationOf(IForEachLoopOperation loop, SemanticModel model)
    {
        var statement = (CommonForEachStatementSyntax)loop.Syntax;
        var info = model.GetForEachStatementInfo(statement);
        var enumeration = Calls(info.GetEnumeratorMethod, info.MoveNextMethod, info.DisposeMethod).Concat(CurrentOf(info.CurrentProperty));
        if (statement is not ForEachVariableStatementSyntax deconstructed)
        {
            return enumeration;
        }

        return enumeration.Concat(DeconstructionOf(model.GetDeconstructionInfo(deconstructed)));
    }

    private static IEnumerable<(ISymbol Symbol, bool IsField)> CurrentOf(IPropertySymbol? current)
    {
        if (current is null)
        {
            return [];
        }

        return PropertyUseOf(current, [current.GetMethod]);
    }

    private static IEnumerable<(ISymbol Symbol, bool IsField)> DeconstructionOf(IDeconstructionAssignmentOperation deconstruction, SemanticModel model)
    {
        var assignment = (AssignmentExpressionSyntax)deconstruction.Syntax;
        var info = model.GetDeconstructionInfo(assignment);
        return DeconstructionOf(info);
    }

    // A deconstruction calls the Deconstruct of each value it takes apart, a nested one's included.
    private static IEnumerable<(ISymbol Symbol, bool IsField)> DeconstructionOf(DeconstructionInfo info) =>
        Calls(info.Method).Concat(info.Nested.SelectMany(DeconstructionOf));

    // A constant is no use. A backing field the field keyword names stands for its property.
    private static IEnumerable<(ISymbol Symbol, bool IsField)> FieldUseOf(IFieldSymbol field)
    {
        if (field.IsConst)
        {
            return [];
        }

        return [(field.AssociatedSymbol ?? field, true)];
    }

    // An auto-property is a field. A property with a body is called through the accessors the use takes.
    private static IEnumerable<(ISymbol Symbol, bool IsField)> PropertyUseOf(IPropertySymbol property, IMethodSymbol?[] accessors)
    {
        if (IsBackedByField(property))
        {
            return [(property, true)];
        }

        return Calls(accessors);
    }

    // A field-like event is a field. A custom event is called through its add and remove.
    private static IEnumerable<(ISymbol Symbol, bool IsField)> EventUseOf(IEventSymbol @event)
    {
        if (IsBackedByField(@event))
        {
            return [(@event, true)];
        }

        return [];
    }

    // A plain assignment to a property calls its setter, a compound assignment or an increment calls both, anything
    // else its getter. A property a deconstruction assigns, inside any tuple, is assigned.
    private static IMethodSymbol?[] AccessorsUsed(IPropertyReferenceOperation reference)
    {
        var property = reference.Property;
        IOperation assigned = reference;
        while (assigned.Parent is ITupleOperation tuple)
        {
            assigned = tuple;
        }

        if (assigned.Parent is IAssignmentOperation assignment && assignment.Target == assigned)
        {
            if (assignment is ISimpleAssignmentOperation or IDeconstructionAssignmentOperation)
            {
                return [property.SetMethod];
            }

            return [property.GetMethod, property.SetMethod];
        }

        if (reference.Parent is IIncrementOrDecrementOperation step && step.Target == reference)
        {
            return [property.GetMethod, property.SetMethod];
        }

        return [property.GetMethod];
    }

    // A member reads a primary constructor parameter it captures. A read in an initializer copies the value once and
    // captures nothing.
    private static IEnumerable<(ISymbol Symbol, bool IsField)> CaptureOf(IParameterReferenceOperation reference)
    {
        if (reference.Parameter.ContainingSymbol is not IMethodSymbol constructor)
        {
            return [];
        }

        if (!CapturesItsParameters(constructor))
        {
            return [];
        }

        if (InInitializer(reference.Syntax))
        {
            return [];
        }

        return [(reference.Parameter, true)];
    }

    private static bool InInitializer(SyntaxNode node) =>
        node.Ancestors().Any(ancestor => ancestor is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax } or PropertyDeclarationSyntax });

    // Answered once per member: the scan of its type's members runs on the first reference only.
    private static bool IsBackedByField(ISymbol member) => BackedByField.GetValue(member, HasBackingField).Value;

    private static StrongBox<bool> HasBackingField(ISymbol member)
    {
        foreach (var field in member.ContainingType.GetMembers().OfType<IFieldSymbol>())
        {
            if (SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, member))
            {
                return new StrongBox<bool>(true);
            }
        }

        return new StrongBox<bool>(false);
    }
}
