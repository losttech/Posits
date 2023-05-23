namespace Posits;

public class Constants {
    [Fact]
    public void Zero() => Assert.Equal(0f, p8.Zero.ToSingle());
    [Fact]
    public void One() => Assert.Equal(1f, p8.One.ToSingle());
    [Fact]
    public void MinusOne() => Assert.Equal(-1f, p8.MinusOne.ToSingle());
    [Fact]
    public void SmallestPositive() => Assert.Equal(MathF.Pow(2, -24), p8.Epsilon.ToSingle());
    [Fact]
    public void MaxValue() => Assert.Equal(MathF.Pow(2, 24), p8.MaxValue.ToSingle());
}