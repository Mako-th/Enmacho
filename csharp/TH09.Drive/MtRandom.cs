namespace TH09.Drive;

public sealed class MtRandom
{
    private const int N = 624;
    private const int M = 397;
    private const uint MatrixA = 0x9908B0DFu;
    private const uint UpperMask = 0x80000000u;
    private const uint LowerMask = 0x7FFFFFFFu;

    private readonly uint[] _mt = new uint[N];
    private int _mti = N + 1;

    public MtRandom(int seed)
    {
        long n = seed;
        if (n < 0) n = -n;
        var key = new List<uint>();
        if (n == 0)
        {
            key.Add(0u);
        }
        else
        {
            while (n != 0)
            {
                key.Add((uint)(n & 0xFFFFFFFFL));
                n >>= 32;
            }
        }
        InitByArray(key.ToArray());
    }

    private void InitGenrand(uint s)
    {
        _mt[0] = s;
        for (int i = 1; i < N; i++)
            _mt[i] = unchecked(1812433253u * (_mt[i - 1] ^ (_mt[i - 1] >> 30)) + (uint)i);
        _mti = N;
    }

    private void InitByArray(uint[] key)
    {
        InitGenrand(19650218u);
        int i = 1;
        int j = 0;
        int k = Math.Max(N, key.Length);
        for (; k > 0; k--)
        {
            _mt[i] = unchecked((_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1664525u)) + key[j] + (uint)j);
            i++;
            j++;
            if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
            if (j >= key.Length) j = 0;
        }
        for (k = N - 1; k > 0; k--)
        {
            _mt[i] = unchecked((_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1566083941u)) - (uint)i);
            i++;
            if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
        }
        _mt[0] = UpperMask;
        _mti = N;
    }

    public uint NextUInt32()
    {
        if (_mti >= N)
        {
            int kk;
            for (kk = 0; kk < N - M; kk++)
            {
                uint y0 = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + M] ^ (y0 >> 1) ^ ((y0 & 1u) != 0 ? MatrixA : 0u);
            }
            for (; kk < N - 1; kk++)
            {
                uint y1 = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + (M - N)] ^ (y1 >> 1) ^ ((y1 & 1u) != 0 ? MatrixA : 0u);
            }
            uint y2 = (_mt[N - 1] & UpperMask) | (_mt[0] & LowerMask);
            _mt[N - 1] = _mt[M - 1] ^ (y2 >> 1) ^ ((y2 & 1u) != 0 ? MatrixA : 0u);
            _mti = 0;
        }
        uint y = _mt[_mti++];
        y ^= y >> 11;
        y ^= (y << 7) & 0x9D2C5680u;
        y ^= (y << 15) & 0xEFC60000u;
        y ^= y >> 18;
        return y;
    }

    public double NextDouble() => NextRandom53() * (1.0 / 9007199254740992.0);

    public long NextRandom53()
    {
        long a = NextUInt32() >> 5;
        long b = NextUInt32() >> 6;
        return a * 67108864L + b;
    }

    public uint GetRandBits(int k)
    {
        if (k <= 0) return 0u;
        if (k > 32) throw new ArgumentException("getrandbits は 32 ビットまでしか移していません: " + k);
        return NextUInt32() >> (32 - k);
    }

    private int RandBelow(int n)
    {
        if (n == 0) return 0;
        int k = BitLength(n);
        uint r = GetRandBits(k);
        while (r >= (uint)n) r = GetRandBits(k);
        return (int)r;
    }

    private static int BitLength(int value)
    {
        int bits = 0;
        while (value > 0) { bits++; value >>= 1; }
        return bits;
    }

    public int Choice(int[] seq) => seq[RandBelow(seq.Length)];
}
