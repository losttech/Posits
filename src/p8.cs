namespace Posits;

using System.Runtime.CompilerServices;

// ReSharper disable once InconsistentNaming
/// <summary>8 bit posit with 2 exponent bits</summary>
public readonly struct p8 {
    public byte Byte { get; }

    const int NAR = 0b1000_0000;

    /// <summary>Not a real. Similar to <see cref="float.NaN"/>.</summary>
    public static p8 NaR => new(NAR);

    public static p8 Zero => new(0b0000_0000);
    public static p8 One => new(0b0100_0000);
    public static p8 MinusOne => new(0b1100_0000);
    public static p8 Epsilon => new(0b0000_0001);
    public static p8 MaxValue => new(0b0111_1111);

    public bool IsNaR => this.Byte == NAR;

    const int BITS = 8;
    const int EXPONENT_SIZE = 2;

    public float ToSingle() {
        // from https://github.com/ChrisLomont/LibPosit/blob/4b9f332bfb337852d8e7eca9890c06dc14bd2d74/Testing/Posit.cs#L262
        byte lBits = this.Byte;

        if (lBits == 0) return 0.0f; // simply remove this case here
        if (this.IsNaR) return float.NaN; // replace with this


        // sign
        int s = Bit(lBits, BITS - 1); // this must be treated as a signed integer

        // regime: k identical bits
        int bitIndex = BITS - 2; // bit pos, walk bits in value
        int regimeBit = Bit(lBits, bitIndex); // bit val
        int regimeBitCount = 0;
        while (bitIndex >= 0 && Bit(lBits, bitIndex) == regimeBit) {
            regimeBitCount++;
            bitIndex--;
        }

        // regime value r
        int regime = regimeBit == 0 ? -regimeBitCount : regimeBitCount - 1;
        // i points to last of regime, move down one
        bitIndex--;

        // exponent
        int e = 0;
        for (int z = 0; z < EXPONENT_SIZE; ++z) {
            // get valid bits if any, else 0
            int bit = bitIndex >= 0 ? Bit(lBits, bitIndex) : 0;
            e = 2 * e + bit;
            bitIndex--;
        }

        // fraction is max(0,n-3-es) bits, any past end are 0
        int fraction = 0;
        int mantissaBits = Math.Max(0, BITS - 3 - EXPONENT_SIZE); // # frac bits
        for (int z = 0; z < mantissaBits; ++z) {
            // get valid bits if any, else 0
            int bit = bitIndex >= 0 ? Bit(lBits, bitIndex) : 0;
            fraction = 2 * fraction + bit;
            bitIndex--;
        }

        // fraction is divided by 2^m, so is 0 <= f < 1

        // for es = 2
        // posit p = (1-3s+f) * 2^((1-2s)*(4r+e+s))

        // now convert:
        float df = fraction; // exact
        df /= 1 << mantissaBits; // exact
        df = (1 - 3 * s + df); // exact?
        int bb = 1 << EXPONENT_SIZE; // 4 if es = 2
        long dexp = (1 - 2 * s) * (bb * regime + e + s);
        df *= MathF.Pow(2, dexp); // exact?
        return df;
    }

    public static implicit operator float(p8 value) => value.ToSingle();

    public override string ToString() => this.IsNaR ? "NaR" : this.ToSingle().ToString();
    public override int GetHashCode() => this.Byte.GetHashCode();

    #region Utility functions
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    static int Bit(byte v, int index) {
        if (index is < 0 or >= 8) throw new ArgumentOutOfRangeException(nameof(index));

        byte mask = (byte)(1 << index);
        return (v & mask) >> index;
    }
    #endregion Utility functions

    p8(byte value) {
        this.Byte = value;
    }
}