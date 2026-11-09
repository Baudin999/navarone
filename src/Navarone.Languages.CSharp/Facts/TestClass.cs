using Microsoft.CodeAnalysis;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// The test-class rule: a test class has a method marked [Fact], [Theory], [Test] or [TestMethod], or with an attribute
/// derived from one of them. An attribute that does not resolve binds to an error type that carries the name as written.
/// </summary>
internal static class TestClass
{
    /// <summary>Whether the given type is a test class.</summary>
    // navarone: 0000085
    public static bool Is(INamedTypeSymbol type)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attribute in method.GetAttributes())
            {
                if (IsTestAttribute(attribute.AttributeClass))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTestAttribute(INamedTypeSymbol? attribute)
    {
        for (; attribute is not null; attribute = attribute.BaseType)
        {
            if (attribute.Name is "Fact" or "FactAttribute" or "Theory" or "TheoryAttribute" or "Test" or "TestAttribute" or "TestMethod" or "TestMethodAttribute")
            {
                return true;
            }
        }

        return false;
    }
}
