using System.Globalization;
using Navarone.Engine.Facts;

namespace Navarone.Engine.Lenses;

/// <summary>
/// How a lens names the methods of a codebase, and the lens that makes a method its subject. A method is named
/// Type.Method, with the number of its overload after a colon when the name matches several. The lens that makes a
/// method the subject reads it as the user does: a private method with its class, and any other method with its class
/// and its callers, because it is part of its class's API.
/// </summary>
internal sealed class LensQueries
{
    private readonly Dictionary<string, List<TypeFacts>> typesByName = [];
    private readonly Dictionary<SymbolId, TypeFacts> owners = [];

    public LensQueries(CodebaseFacts facts)
    {
        foreach (var type in facts.Types)
        {
            if (!typesByName.TryGetValue(type.Name, out var named))
            {
                named = [];
                typesByName.Add(type.Name, named);
            }

            named.Add(type);
            foreach (var method in type.Methods)
            {
                owners.TryAdd(method.Symbol, type);
            }
        }
    }

    /// <summary>The type that declares the given method.</summary>
    public TypeFacts OwnerOf(MethodFacts method) => owners[method.Symbol];

    /// <summary>
    /// Every method the given type name and method name match, in file and line order: a method of a type of that own
    /// name whose name is the method name, with or without its parameters.
    /// </summary>
    public List<MethodFacts> Overloads(string typeName, string methodName) =>
    [
        .. typesByName.GetValueOrDefault(typeName, [])
            .SelectMany(type => type.Methods)
            .Where(method => method.Name == methodName || method.Name.StartsWith($"{methodName}(", StringComparison.Ordinal))
            .OrderBy(method => method.Location.Path, StringComparer.Ordinal)
            .ThenBy(method => method.Location.Line),
    ];

    /// <summary>
    /// Every method the given method lens's name matches, in file and line order. The lens's rules checked that the
    /// name is Type.Method. A name that matches no method is an error at the name.
    /// </summary>
    public List<MethodFacts> Overloads(Lens lens)
    {
        var name = lens.Name.Text;
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        var found = Overloads(name[..dot], name[(dot + 1)..]);
        if (found.Count == 0)
        {
            throw LensError.At(lens.Text, lens.Name.Column, $"no method {name} in the codebase");
        }

        return found;
    }

    /// <summary>
    /// The overload of the given ones the lens numbers, from 1, or else all of them. A number no overload has is an
    /// error at the number.
    /// </summary>
    public static List<MethodFacts> Chosen(Lens lens, List<MethodFacts> overloads)
    {
        if (lens.Overload is not { } overload)
        {
            return overloads;
        }

        var number = int.Parse(overload.Text, NumberStyles.None, CultureInfo.InvariantCulture);
        if (number > overloads.Count)
        {
            var count = overloads.Count == 1 ? "1 overload" : $"{overloads.Count} overloads";
            throw LensError.At(lens.Text, overload.Column, $"{lens.Name.Text} has {count}, not {number}");
        }

        return [overloads[number - 1]];
    }

    /// <summary>
    /// The lens that makes the given method its subject, labelled with the method's name in the lens, or null when a
    /// lens cannot name it, such as an accessor or an explicit interface member.
    /// </summary>
    public LensQuery? Of(MethodFacts method)
    {
        if (NameOf(method) is not { } name)
        {
            return null;
        }

        var query = $"method {name} class:short";
        if (!method.IsPrivate)
        {
            query += " callers:3";
        }

        return new LensQuery(name, query);
    }

    // Type.Method, with the overload's number when the name matches several, or null when a part holds a character the
    // lexer does not read as part of a word, or a dot, which splits Type.Method.
    private string? NameOf(MethodFacts method)
    {
        var type = owners[method.Symbol].Name;
        var paren = method.Name.IndexOf('(', StringComparison.Ordinal);
        var bare = paren < 0 ? method.Name : method.Name[..paren];
        if (!Nameable(type) || !Nameable(bare))
        {
            return null;
        }

        var overloads = Overloads(type, bare);
        if (overloads.Count == 1)
        {
            return $"{type}.{bare}";
        }

        var number = overloads.IndexOf(method) + 1;
        return string.Create(CultureInfo.InvariantCulture, $"{type}.{bare}:{number}");
    }

    private static bool Nameable(string part) => part.Length > 0 && part.All(character => character != '.' && LensLexer.IsWordCharacter(character));
}
