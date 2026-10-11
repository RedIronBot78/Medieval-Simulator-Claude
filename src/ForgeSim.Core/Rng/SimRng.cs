namespace ForgeSim.Core.Rng;

/// <summary>
/// Seeded SplitMix64 generator. All sim randomness goes through one of these, passed in explicitly,
/// so the same inputs and seed give byte-identical output on every machine and .NET version
/// (System.Random's algorithm is not guaranteed stable across runtimes).
/// </summary>
public sealed class SimRng
{
    private ulong _state;

    public SimRng(ulong seed) => _state = seed;

    public ulong NextUInt64()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>True with probability <paramref name="p"/>. Always consumes exactly one draw.</summary>
    public bool Chance(double p) => NextDouble() < p;
}
