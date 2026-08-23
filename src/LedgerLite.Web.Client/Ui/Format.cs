using System.Globalization;

namespace LedgerLite.Web.Client.Ui;

/// <summary>
/// Consistent display formatting for money, dates and numbers across the app.
/// Money output is designed to be paired with <c>font-mono tnum</c> classes.
/// </summary>
public static class Format
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>"$1,234.56" (negative: "-$1,234.56").</summary>
    public static string Money(decimal amount) => amount.ToString("$#,##0.00;-$#,##0.00", Invariant);

    /// <summary>"Aug 22, 2026".</summary>
    public static string Date(DateOnly date) => date.ToString("MMM d, yyyy", Invariant);

    /// <summary>"Aug 22, 2026 14:05" (UTC timestamp of a journal entry).</summary>
    public static string Timestamp(DateTime utc) => utc.ToString("MMM d, yyyy HH:mm", Invariant);

    /// <summary>"62.5" (no unit, no symbol) — used for percentages.</summary>
    public static string Percent(decimal ratio) => (ratio * 100m).ToString("0.#", Invariant);

    /// <summary>Exact lowercase "yyyy-MM-dd" value for <c>type="date"</c> inputs.</summary>
    public static string DateInput(DateOnly date) => date.ToString("yyyy-MM-dd", Invariant);
}
