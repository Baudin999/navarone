using Navarone.Engine.Duplication;
using Navarone.Engine.Facts;

namespace Navarone.Tests.Engine;

public sealed class CloneReportTests
{
    [Fact]
    public async Task APairShowsItsHeadingBothLocationsAndBothPiecesWithTheLinesThatDifferMarked()
    {
        var first = new ClonePiece(new SymbolId("A.M()"), "A.cs", 3, 4, [new CloneLine(3, "var a = 1;", false), new CloneLine(4, "return a;", true)]);
        var second = new ClonePiece(new SymbolId("B.N()"), "B.cs", 7, 7, [new CloneLine(7, "var b = 2;", false)]);
        var pair = new ClonePair(first, second, 0.875, 32, 0.5);
        using var writer = new StringWriter();

        await CloneReport.WriteAsync(writer, [pair], CloneFloor.Research, "csharp");

        Assert.Equal(
            "1 clone pairs at similarity 0.70 or more and 30 tokens or more. A line marked ~ holds a token the other piece does not match; identifiers and literals match whatever they are named. Same names is the share of the matched identifiers spelled the same on both sides.\n\n"
            + "## 1. similarity 0.88, 32 tokens, same names 0.50\n\n"
            + "A.M(), lines 3 to 4\n\n`A.cs:3`\n\n```csharp\n  var a = 1;\n~ return a;\n```\n\n"
            + "B.N(), lines 7 to 7\n\n`B.cs:7`\n\n```csharp\n  var b = 2;\n```\n\n",
            writer.ToString());
    }
}
