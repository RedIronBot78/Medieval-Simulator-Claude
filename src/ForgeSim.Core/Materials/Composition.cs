namespace ForgeSim.Core.Materials;

/// <summary>
/// Mass-percent composition. The base element (Cu for the bronze tier) is always the balance,
/// so the stored alloying elements must sum to at most 100.
/// </summary>
public sealed class Composition
{
    private readonly SortedDictionary<string, double> _alloying;

    public string BaseElement { get; }

    private Composition(string baseElement, SortedDictionary<string, double> alloying)
    {
        BaseElement = baseElement;
        _alloying = alloying;
    }

    /// <summary>Builds a composition from wt% values. Any entry for the base element is ignored; it is the balance.</summary>
    public static Composition FromWt(string baseElement, IEnumerable<KeyValuePair<string, double>> wt)
    {
        var alloying = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var (symbol, value) in wt)
        {
            if (symbol == baseElement) continue;
            if (value < 0) throw new ArgumentException($"Negative wt% for {symbol}: {value}");
            if (value == 0) continue;
            alloying[symbol] = value;
        }
        var sum = alloying.Values.Sum();
        if (sum > 100.0 + 1e-9) throw new ArgumentException($"Alloying elements sum to {sum} wt%, more than 100.");
        return new Composition(baseElement, alloying);
    }

    public static Composition Pure(string baseElement) => FromWt(baseElement, []);

    /// <summary>wt% of an element; the base element returns the balance.</summary>
    public double this[string symbol] =>
        symbol == BaseElement ? Balance : _alloying.TryGetValue(symbol, out var v) ? v : 0.0;

    public double Balance => Math.Max(0.0, 100.0 - _alloying.Values.Sum());

    /// <summary>Alloying elements only (base excluded), in ordinal symbol order.</summary>
    public IReadOnlyDictionary<string, double> Alloying => _alloying;

    /// <summary>All elements including the base, base first, then ordinal symbol order.</summary>
    public IEnumerable<KeyValuePair<string, double>> All()
    {
        yield return new(BaseElement, Balance);
        foreach (var kv in _alloying) yield return kv;
    }

    /// <summary>Returns a copy with one element set to a new wt% (base element stays the balance).</summary>
    public Composition With(string symbol, double wt)
    {
        var copy = new Dictionary<string, double>(_alloying) { [symbol] = Math.Max(0.0, wt) };
        return FromWt(BaseElement, copy);
    }

    public override string ToString() =>
        string.Join(" ", All().Where(kv => kv.Value > 0).Select(kv => $"{kv.Key} {kv.Value:0.00}%"));
}
