using Navarone.Engine.Reasons;

namespace Navarone.Tests.Engine;

public sealed class ProblemIdTests
{
    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("000000a")]
    [InlineData("٠٠٠٠٠٠١")]
    [InlineData("99999999999")]
    public void AStringThatIsNotAnIdsShapeCannotBeAProblemId(string text)
    {
        Assert.False(ProblemId.IsShape(text));
        Assert.Throws<InvalidDataException>(() => new ProblemId(text));
    }

    [Fact]
    public void AnIdOfTheRightShapeHasANumber()
    {
        Assert.Equal(1234, new ProblemId("0001234").Number);
    }
}
