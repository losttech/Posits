namespace Posits;

public class Conversions {
    [Fact]
    public void NegativeHalf() => Assert.Equal(-0.5f, (p8)(-0.5));

    [Fact]
    public void Two() => Assert.Equal(2f, (p8)2);
}