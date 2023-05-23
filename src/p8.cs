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
        int mantissaBits = Math.Max(0, BITS - 3 - EXPONENT_SIZE); // # fraction bits
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

    public static explicit operator p8(double value) {
        // from https://github.com/ChrisLomont/LibPosit/blob/4b9f332bfb337852d8e7eca9890c06dc14bd2d74/Testing/Posit.cs#L169

        // spec: 
        // if x representable, return posit of x
        // if |x| > max return max*sign(x)
        // if |x| < min return min*sign(x)
        // u,w are n bit posits st u < x < w and interval (u,v) has no other posits
        // let U be the n-bit representation of u
        // let v = n+1 bit posit with rep U1  (U append 1)
        // if u < x < v or (x==v && LSB(u) == 0) then
        //   return u
        // else
        //   return w

        if (value == 0) return Zero;

        if (!double.IsFinite(value)) return NaR;

        uint bits;
        int me = (1 << EXPONENT_SIZE) * (BITS - 2);
        double epsilon = Math.Pow(2, -me); // exact?
        double max = 1 / epsilon; // exact
        if (Math.Abs(value) >= max) {
            bits = (1U << (BITS - 1)) - 1;
            if (value < 0) bits = ~bits + 1;
            bits &= (1U << BITS) - 1;
            return new((byte)bits);
        }
        if (Math.Abs(value) < epsilon) {
            bits = 1;
            if (value < 0) bits = ~bits + 1;
            bits &= (1U << BITS) - 1;
            return new((byte)bits);
        }

        // IEEE 754 float64 format:
        // 1 sign bit, 11 exponent bits, 52 fraction bits
        ulong b64 = BitConverter.DoubleToUInt64Bits(value);
        ulong e64 = (b64 >> 52) & ((1UL << 11) - 1); // exponent bits
        ulong f64 = b64 & ((1UL << 52) - 1); // fraction bits

        int e = ((int)e64) - 1023; // add exponent bias
        int expSign = e < 0 ? 1 : 0;
        int k = e >> EXPONENT_SIZE; // for useed^k calc

        // create: 2 last regime bits, exponent bits, mantissa bits, at top of 64 bit value
        ulong regBits = (1UL << 63) >> expSign; // 01000... for |x| < 1 else 1000....
        ulong expBits = (ulong)(e & ((1 << EXPONENT_SIZE) - 1));
        expBits <<= 64 - 2 - EXPONENT_SIZE; // align
        ulong fractionBits = f64 << (11 - EXPONENT_SIZE - 1); // # bits for float64 exponent

        ulong bits64 = regBits | expBits | fractionBits;
        bits64 = (ulong)(((long)bits64) >>
                         (Math.Abs(k + 1) + expSign)); // treat as signed to extend bits
        bits64 &= ~(1UL << 63); // remove possible leftover sign

        // round to nearest n-bit posit
        ulong rounded = RoundBits(BITS, bits64);

        // no underflow or overflow
        int kmax = (1023 >> EXPONENT_SIZE) + 1; // check unbounded
        if (BITS <= Math.Abs(k) && Math.Abs(k) < kmax)
            rounded = (ulong)((long)rounded - Math.Sign(k));

        if (value < 0)
            rounded = (ulong)(-(long)rounded); // two's complement for neg
        bits = (uint)rounded & ((1U << BITS) - 1);
        return new((byte)bits);
    }

    public override string ToString() => this.IsNaR ? "NaR" : this.ToSingle().ToString();
    public override int GetHashCode() => this.Byte.GetHashCode();

    #region Utility functions
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    static int Bit(byte v, int index) {
        if (index is < 0 or >= 8) throw new ArgumentOutOfRangeException(nameof(index));

        byte mask = (byte)(1 << index);
        return (v & mask) >> index;
    }

    // round long representation to desired length
    static ulong RoundBits(int bitLen, ulong positBits64) {
        // rules from standard 2022
        // u,w are n bit posits st u < x < w and interval (u,v) has no other posits
        // let U be the n-bit representation of u
        // let v = n+1 bit posit with rep U1  (U append 1)
        // if u < x < v or (x==v && LSB(u) == 0) then
        //   return u
        // else
        //   return w

        int dBits = 64 - bitLen;
        if (positBits64 == (1UL << 63) >> bitLen)
            // special case: if value is 1/2 ulp, rounds up, not down ?!
            return (positBits64 * 2) >> dBits;

        // bankers rounding
        ulong halfUlp = ((1UL << 63) - 1) >> bitLen; // ...0007ffff... just less than ulp/2
        halfUlp += (positBits64 >> dBits) & 1; // odd is 008000.. (round up for tie)
        //var t1 = positBits64 >> dBits;
        positBits64 += halfUlp; // add half up
        //var t2 = positBits64 >> dBits;
        //if (t1 != t2) Console.Write($" - round up - ");
        return positBits64 >> dBits;
    }
    #endregion Utility functions

    p8(byte value) {
        this.Byte = value;
    }
}