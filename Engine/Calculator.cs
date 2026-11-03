using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.SmartCalc.Engine
{
    /// <param name="Copy">what Enter puts on the clipboard: the bare number, no grouping</param>
    public sealed record Answer(string Title, string SubTitle, string Copy);

    /// <summary>
    /// Turns a launcher query into answers: arithmetic, percentages, "x of y in %", percent change
    /// and unit conversion, phrased in English or Russian.
    /// </summary>
    public static class Calculator
    {
        private const RegexOptions Opts = RegexOptions.CultureInvariant;

        private static readonly Regex Change = new(
            @"^(?:from|с|от)\s+(?<a>.+?)\s+(?:to|до)\s+(?<b>.+?)(?:\s+(?:в|in)?\s*(?:%|процентах|percent))?$", Opts);

        private static readonly Regex PercentOf = new(@"^(?<p>.+?)\s*%\s*(?:of|от|из)\s+(?<a>.+)$", Opts);

        private static readonly Regex AsPercent = new(@"^(?<e>.+?)\s+(?:в|in|to|as)\s+(?:%|процент\w*|percent)$", Opts);

        private static readonly Regex Ratio = new(
            @"^(?:what percent(?:age)? is\s+|сколько процентов\s+(?:составляет\s+|будет\s+)?)?(?<x>.+?)\s+(?:out of|of|от|из)\s+(?<y>.+?)" +
            @"(?:\s+(?:how much percent|how many percent|what percent|in percent|as percent|in %|в процентах|в %|percent|процентов)|\s*%)?$", Opts);

        private static readonly Regex Conversion = new(
            @"^(?<src>.+?)(?:\s+(?:в|во|in|to|into)\s+|\s*(?:->|→|=)\s*)(?<dst>[^\s\d()]+[23]?(?:\s*/\s*[^\s\d()]+)?)$", Opts);

        // an amount (any expression ending in a digit, bracket, π or %) followed by a unit; m2, км2 may end in a digit
        private static readonly Regex Quantity = new(@"^(?<e>.*?[\d)π%])\s*(?<u>[^\s\d()+\-*^!%=]+[23]?(?:\s*/\s*[^\s\d()]+)?)$", Opts);

        private static readonly Regex TimesSign = new(@"(?<=[\d)])\s*[xх×]\s*(?=[\d(.,])", Opts);
        private static readonly Regex Spaces = new(@"\s+", Opts);
        private static readonly Regex Cyrillic = new(@"\p{IsCyrillic}", Opts);
        private static readonly Regex LatinWord = new(@"[a-z]+", Opts | RegexOptions.IgnoreCase);

        /// <summary>
        /// Answer in the language of the query: Russian words get a Russian answer, English words an English one,
        /// and bare arithmetic follows the number format of Windows' regional settings.
        /// </summary>
        public static Locale PickLocale(string raw, string regionalFormat)
        {
            var q = raw ?? "";
            if (Cyrillic.IsMatch(q))
                return Locale.Russian;
            if (LatinWord.Matches(q).Any(m => !Parser.IsMathWord(m.Value.ToLowerInvariant()) && m.Value.ToLowerInvariant() != "x"))
                return Locale.English;
            return Locale.For(regionalFormat);
        }

        /// <param name="plainArithmetic">
        /// false while Flow's built-in Calculator is enabled: it already answers 2+2, so only
        /// what it cannot do (percentages, units, phrases) is answered here.
        /// </param>
        public static List<Answer> Evaluate(string raw, Locale locale, bool plainArithmetic = true)
        {
            var q = Normalize(raw);
            // a digit is required, otherwise "e" or "pi" would answer while you type an app name
            if (q.Length == 0 || !q.Any(char.IsDigit))
                return new List<Answer>();

            var c = new Context(locale);
            return c.Change(q) ?? c.PercentOf(q) ?? c.AsPercent(q) ?? c.Ratio(q)
                ?? c.Conversion(q) ?? c.Quantity(q) ?? c.Expression(q, plainArithmetic) ?? new List<Answer>();
        }

        private static string Normalize(string raw)
        {
            var q = (raw ?? "").Replace(' ', ' ').Replace(' ', ' ').Replace(' ', ' ')
                .Replace('×', '*').Replace('·', '*').Replace('÷', '/')
                .Replace('−', '-').Replace('–', '-').Replace("**", "^").ToLowerInvariant();
            q = Spaces.Replace(q, " ").Trim();
            foreach (var prefix in new[] { "сколько будет ", "what is ", "whats ", "calc ", "=" })
            {
                if (q.StartsWith(prefix, StringComparison.Ordinal))
                    q = q[prefix.Length..].TrimStart();
            }
            q = q.TrimEnd('=', ' ', '?');
            return TimesSign.Replace(q, "*");
        }

        private sealed class Context
        {
            private readonly Locale _l;

            public Context(Locale locale) => _l = locale;

            public List<Answer> Change(string q)
            {
                var m = Calculator.Change.Match(q);
                if (!m.Success || !Plain(m.Groups["a"].Value, out var a) || !Plain(m.Groups["b"].Value, out var b) || a == 0)
                    return null;
                var pct = (b - a) / Math.Abs(a) * 100;
                var title = (pct > 0 ? "+" : "") + _l.Number(pct, 2) + _l.Percent;
                return One(title, string.Format(_l.Change, _l.Number(a), _l.Number(b), _l.Number(b - a)), _l.Copy(pct, 2));
            }

            public List<Answer> PercentOf(string q)
            {
                var m = Calculator.PercentOf.Match(q);
                if (!m.Success || !Plain(m.Groups["p"].Value, out var p) || !Plain(m.Groups["a"].Value, out var a))
                    return null;
                var v = a * p / 100;
                return One(_l.Number(v), string.Format(_l.PercentOf, _l.Number(p), _l.Number(a)), _l.Copy(v));
            }

            public List<Answer> AsPercent(string q)
            {
                var m = Calculator.AsPercent.Match(q);
                if (!m.Success || !Parser.TryEval(m.Groups["e"].Value, out var v, out _))
                    return null;
                var pct = v.X * 100;
                return One(_l.Number(pct, 2) + _l.Percent, string.Format(_l.AsPercent, m.Groups["e"].Value), _l.Copy(pct, 2));
            }

            public List<Answer> Ratio(string q)
            {
                var m = Calculator.Ratio.Match(q);
                if (!m.Success || !Plain(m.Groups["x"].Value, out var x) || !Plain(m.Groups["y"].Value, out var y) || y == 0)
                    return null;
                var pct = x / y * 100;
                return One(_l.Number(pct, 2) + _l.Percent, string.Format(_l.Ratio, _l.Number(x), _l.Number(y)), _l.Copy(pct, 2));
            }

            public List<Answer> Conversion(string q)
            {
                var m = Calculator.Conversion.Match(q);
                if (!m.Success)
                    return null;
                var to = Units.Find(m.Groups["dst"].Value);
                if (to == null || !TryQuantity(m.Groups["src"].Value, out var amount, out var from) || from.Kind != to.Kind)
                    return null;

                var source = $"{_l.Amount(amount)} {from.Name(_l)}";
                var answers = new List<Answer> { Converted(from.ToBase(amount), to, source) };
                if (to.Kind == Kind.Time && from.ToBase(amount) >= 60)
                    answers.Add(Duration(from.ToBase(amount), source));
                if (DecimalBytes(from, to, amount) is { } si)
                    answers.Add(si);
                return answers;
            }

            public List<Answer> Quantity(string q)
            {
                if (!TryQuantity(q, out var amount, out var from))
                    return null;

                var source = $"{_l.Amount(amount)} {from.Name(_l)}";
                var baseValue = from.ToBase(amount);
                var answers = new List<Answer>();

                if (from.Kind == Kind.Time)
                {
                    if (baseValue >= 60 && Duration(baseValue, source) is var d && d.Title != source)
                        answers.Add(d);
                    var next = Units.All.Where(u => u.Kind == Kind.Time && u.Factor > from.Factor && baseValue >= u.Factor).LastOrDefault()
                        ?? Units.All.Where(u => u.Kind == Kind.Time && u.Factor < from.Factor).LastOrDefault();
                    if (next != null)
                        answers.Add(Converted(baseValue, next, source));
                }
                else if (from.Kind == Kind.Data && from.Partner == null)
                {
                    // bytes: the next unit up when it reaches 1, otherwise the next one down
                    var at = Array.IndexOf(Units.Bytes, from);
                    var up = at + 1 < Units.Bytes.Length ? Units.Bytes[at + 1] : null;
                    var target = up != null && baseValue >= up.Factor ? up : at > 0 ? Units.Bytes[at - 1] : null;
                    if (target != null)
                        answers.Add(Converted(baseValue, target, source));
                }
                else if (Units.Named(from.Partner) is { } partner)
                {
                    answers.Add(Converted(baseValue, partner, source));
                }

                return answers.Count > 0 ? answers : null;
            }

            public List<Answer> Expression(string q, bool plainArithmetic)
            {
                if (!plainArithmetic && !q.Contains('%'))
                    return null;
                if (!Parser.TryEval(q, out var v, out var hasOperator) || !hasOperator)
                    return null;
                return v.Percent
                    ? One(_l.Number(v.X * 100) + _l.Percent, _l.CopyHint, _l.Copy(v.X * 100))
                    : One(_l.Number(v.X), _l.CopyHint, _l.Copy(v.X));
            }

            private Answer Converted(double baseValue, Unit to, string source)
            {
                var value = to.FromBase(baseValue);
                return new Answer($"{_l.Amount(value)} {to.Name(_l)}", $"{source} → {to.Name(_l)}", _l.CopyAmount(value));
            }

            /// <summary>1 h 11 min, 2 d 3 h: the three largest non-zero parts.</summary>
            private Answer Duration(double seconds, string source)
            {
                var parts = new List<string>();
                var left = Math.Round(seconds);
                var sizes = new[] { 31556952.0, 86400.0, 3600.0, 60.0, 1.0 };
                for (var k = 0; k < sizes.Length; k++)
                {
                    var n = Math.Floor(left / sizes[k]);
                    if (n > 0 && parts.Count < 3)
                        parts.Add($"{_l.Number(n)} {_l.DurationUnits[k]}");
                    left -= n * sizes[k];
                }
                var text = parts.Count > 0 ? string.Join(" ", parts) : "0 " + _l.DurationUnits[^1];
                return new Answer(text, source, text);
            }

            /// <summary>The same byte conversion counted in powers of 1000, as disk makers do.</summary>
            private Answer DecimalBytes(Unit from, Unit to, double amount)
            {
                if (!Units.Bytes.Contains(from) || !Units.Bytes.Contains(to) || from == to)
                    return null;
                var power = Array.IndexOf(Units.Bytes, from) - Array.IndexOf(Units.Bytes, to);
                var value = amount * Math.Pow(1000, power);
                return new Answer($"{_l.Amount(value)} {to.Name(_l)}", _l.DecimalBytes, _l.CopyAmount(value));
            }

            private static bool TryQuantity(string text, out double amount, out Unit unit)
            {
                amount = 0;
                unit = null;
                var m = Calculator.Quantity.Match(text.Trim());
                if (!m.Success)
                    return false;
                unit = Units.Find(m.Groups["u"].Value);
                return unit != null && Plain(m.Groups["e"].Value, out amount);
            }

            /// <summary>A plain (non-percent) expression, including a bare number.</summary>
            private static bool Plain(string text, out double value)
            {
                var ok = Parser.TryEval(text, out var v, out _) && !v.Percent;
                value = v.X;
                return ok;
            }

            private static List<Answer> One(string title, string subTitle, string copy) => new() { new Answer(title, subTitle, copy) };
        }
    }
}
