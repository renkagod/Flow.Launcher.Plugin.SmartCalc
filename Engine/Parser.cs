using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Flow.Launcher.Plugin.SmartCalc.Engine
{
    /// <summary>A value plus whether it is still "a percentage", which changes what + and − mean.</summary>
    internal readonly record struct Value(double X, bool Percent);

    /// <summary>
    /// Recursive-descent arithmetic with Google-style percentages: 520 + 2% = 530.4, 90*200*15% = 2700.
    /// Decimal comma or point, spaces as thousands separators ("1 385"), implicit multiplication ("2pi").
    /// </summary>
    internal sealed class Parser
    {
        private static readonly Dictionary<string, double> Constants = new()
        {
            ["pi"] = Math.PI, ["π"] = Math.PI, ["пи"] = Math.PI, ["e"] = Math.E, ["tau"] = Math.Tau,
        };

        private static readonly Dictionary<string, Func<double[], double>> Functions = new()
        {
            ["sqrt"] = a => Math.Sqrt(One(a)), ["корень"] = a => Math.Sqrt(One(a)), ["cbrt"] = a => Math.Cbrt(One(a)),
            ["abs"] = a => Math.Abs(One(a)), ["exp"] = a => Math.Exp(One(a)),
            ["ln"] = a => Math.Log(One(a)),
            ["log"] = a => a.Length == 2 ? Math.Log(a[0], a[1]) : Math.Log10(One(a)),
            ["lg"] = a => Math.Log10(One(a)), ["log10"] = a => Math.Log10(One(a)), ["log2"] = a => Math.Log2(One(a)),
            ["sin"] = a => Math.Sin(One(a)), ["cos"] = a => Math.Cos(One(a)),
            ["tan"] = a => Math.Tan(One(a)), ["tg"] = a => Math.Tan(One(a)),
            ["cot"] = a => 1 / Math.Tan(One(a)), ["ctg"] = a => 1 / Math.Tan(One(a)),
            ["asin"] = a => Math.Asin(One(a)), ["arcsin"] = a => Math.Asin(One(a)),
            ["acos"] = a => Math.Acos(One(a)), ["arccos"] = a => Math.Acos(One(a)),
            ["atan"] = a => Math.Atan(One(a)), ["arctan"] = a => Math.Atan(One(a)), ["arctg"] = a => Math.Atan(One(a)),
            ["round"] = a => a.Length == 2 ? RoundTo(a[0], a[1]) : Math.Round(One(a), MidpointRounding.AwayFromZero),
            ["floor"] = a => Math.Floor(One(a)), ["ceil"] = a => Math.Ceiling(One(a)),
            ["min"] = a => Many(a, Math.Min), ["max"] = a => Many(a, Math.Max),
        };

        // in max(1,2) the comma separates arguments; in sqrt(2,25) it is a decimal comma
        private static readonly HashSet<string> MultiArg = new() { "log", "round", "min", "max" };

        // each nesting level costs several stack frames, and a stack overflow would take Flow down with it
        private const int MaxLength = 256;

        private readonly string _s;
        private readonly bool _commaGroups;
        private int _i;
        private bool _sawOperator;
        private bool _commaArgs;

        private Parser(string s, bool commaGroups)
        {
            _s = s;
            _commaGroups = commaGroups;
        }

        /// <summary>sqrt, pi and the like say nothing about which language the query is in.</summary>
        public static bool IsMathWord(string word) => Constants.ContainsKey(word) || Functions.ContainsKey(word);

        /// <param name="hasOperator">false for a bare number, which is not worth answering</param>
        /// <param name="commaGroups">read "1,000" as a thousand, as English speakers write it, rather than as 1.000</param>
        public static bool TryEval(string text, out Value value, out bool hasOperator, bool commaGroups = false)
        {
            value = default;
            hasOperator = false;
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaxLength)
                return false;

            var p = new Parser(text, commaGroups);
            try
            {
                var v = p.Expr();
                p.SkipSpaces();
                if (p._i != p._s.Length || double.IsNaN(v.X) || double.IsInfinity(v.X))
                    return false;
                value = v;
                hasOperator = p._sawOperator;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private Value Expr()
        {
            var left = Term();
            while (true)
            {
                var c = Peek();
                if (c != '+' && c != '-')
                    return left;
                _i++;
                _sawOperator = true;
                var right = Term();
                var sign = c == '+' ? 1 : -1;
                left = right.Percent && !left.Percent
                    ? new Value(left.X * (1 + sign * right.X), false) // 520 + 2% adds 2% of 520
                    : new Value(left.X + sign * right.X, left.Percent && right.Percent);
            }
        }

        private Value Term()
        {
            var left = Unary();
            while (true)
            {
                var c = Peek();
                if (c == '*' || c == '/')
                {
                    _i++;
                    _sawOperator = true;
                    var right = Unary();
                    left = new Value(c == '*' ? left.X * right.X : left.X / right.X, false);
                }
                else if (c == '(' || IsIdentStart(c) && _s[_i - 1] != ' ')
                {
                    // implicit multiplication: 2(3+1), 2pi; not "10 e", which is more likely "10 euro" being typed
                    _sawOperator = true;
                    var right = Unary();
                    left = new Value(left.X * right.X, false);
                }
                else
                {
                    return left;
                }
            }
        }

        private Value Unary()
        {
            var c = Peek();
            if (c == '-')
            {
                _i++;
                var v = Unary();
                return new Value(-v.X, v.Percent);
            }
            if (c == '+')
            {
                _i++;
                return Unary();
            }
            return Power();
        }

        private Value Power()
        {
            var b = Postfix();
            if (Peek() != '^')
                return b;
            _i++;
            _sawOperator = true;
            var e = Unary();
            return new Value(Math.Pow(b.X, e.X), false);
        }

        private Value Postfix()
        {
            var v = Primary();
            while (true)
            {
                var c = Peek();
                if (c == '%')
                {
                    _i++;
                    _sawOperator = true;
                    v = new Value(v.X / 100, true);
                }
                else if (c == '!')
                {
                    _i++;
                    _sawOperator = true;
                    v = new Value(Factorial(v.X), false);
                }
                else if (c == '°')
                {
                    _i++;
                    v = new Value(v.X * Math.PI / 180, false);
                }
                else
                {
                    return v;
                }
            }
        }

        private Value Primary()
        {
            var c = Peek();
            if (c == '(')
            {
                _i++;
                var v = Expr();
                if (Peek() != ')')
                    throw new FormatException();
                _i++;
                return v;
            }
            if (char.IsDigit(c) || (c == '.' || c == ',' && !_commaArgs) && _i + 1 < _s.Length && char.IsDigit(_s[_i + 1]))
                return new Value(Number(), false);
            if (c == '√')
            {
                _i++;
                _sawOperator = true;
                return new Value(Math.Sqrt(Postfix().X), false);
            }
            if (IsIdentStart(c))
            {
                var start = _i;
                while (_i < _s.Length && (char.IsLetter(_s[_i]) || _i > start && char.IsDigit(_s[_i])))
                    _i++;
                var name = _s[start.._i];
                if (Constants.TryGetValue(name, out var k))
                    return new Value(k, false);
                if (Functions.TryGetValue(name, out var f))
                {
                    _sawOperator = true;
                    return new Value(f(Args(MultiArg.Contains(name))), false);
                }
            }
            throw new FormatException();
        }

        private double[] Args(bool multi)
        {
            if (Peek() != '(')
                return new[] { Postfix().X }; // sqrt 16, sin 30°

            _i++;
            // round(3,14159; 2) keeps the decimal comma: ";" already separates the arguments
            var outer = _commaArgs;
            _commaArgs = multi && !HasSemicolon();
            try
            {
                var args = new List<double> { Expr().X };
                while (Peek() == ';' || Peek() == ',')
                {
                    _i++;
                    args.Add(Expr().X);
                }
                if (Peek() != ')')
                    throw new FormatException();
                _i++;
                return args.ToArray();
            }
            finally
            {
                _commaArgs = outer;
            }
        }

        /// <summary>Whether the argument list that starts here has a ";" of its own.</summary>
        private bool HasSemicolon()
        {
            var depth = 0;
            for (var k = _i; k < _s.Length && depth >= 0; k++)
            {
                if (_s[k] == '(')
                    depth++;
                else if (_s[k] == ')')
                    depth--;
                else if (_s[k] == ';' && depth == 0)
                    return true;
            }
            return false;
        }

        private double Number()
        {
            var sb = new StringBuilder();
            var lead = ReadDigits(sb);

            // "1 385", "12 000 000": a space followed by exactly three digits continues the number
            var grouped = false;
            while (lead > 0 && (grouped || lead <= 3) && _i + 3 < _s.Length && _s[_i] == ' '
                   && IsDigits(_i + 1, 3) && (_i + 4 >= _s.Length || !char.IsDigit(_s[_i + 4])))
            {
                sb.Append(_s, _i + 1, 3);
                _i += 4;
                grouped = true;
            }

            // "1,000.50", "1,000,000" and, in English, "1,000": commas between groups of three digits
            if (lead is > 0 and <= 3 && !grouped && !_commaArgs)
            {
                var groups = 0;
                while (IsCommaGroup(_i + groups * 4))
                    groups++;
                var end = _i + groups * 4;
                if (groups > 0 && (_commaGroups || groups > 1 || end < _s.Length && _s[end] == '.'))
                {
                    for (var g = 0; g < groups; g++)
                        sb.Append(_s, _i + g * 4 + 1, 3);
                    _i = end;
                }
            }

            if (_i + 1 < _s.Length && (_s[_i] == '.' || _s[_i] == ',' && !_commaArgs) && char.IsDigit(_s[_i + 1]))
            {
                sb.Append('.');
                _i++;
                ReadDigits(sb);
            }

            if (_i + 1 < _s.Length && _s[_i] == 'e')
            {
                var j = _i + 1;
                if (_s[j] == '+' || _s[j] == '-')
                    j++;
                if (j < _s.Length && char.IsDigit(_s[j]))
                {
                    sb.Append('e').Append(_s, _i + 1, j - _i - 1);
                    _i = j;
                    ReadDigits(sb);
                }
            }

            if (sb.Length == 0 || sb[0] == '.')
                sb.Insert(0, '0');
            return double.Parse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private int ReadDigits(StringBuilder sb)
        {
            var n = 0;
            while (_i < _s.Length && char.IsDigit(_s[_i]))
            {
                sb.Append(_s[_i++]);
                n++;
            }
            return n;
        }

        private bool IsDigits(int from, int count)
        {
            for (var k = from; k < from + count; k++)
            {
                if (k >= _s.Length || !char.IsDigit(_s[k]))
                    return false;
            }
            return true;
        }

        private bool IsCommaGroup(int at) =>
            at < _s.Length && _s[at] == ',' && IsDigits(at + 1, 3) && (at + 4 >= _s.Length || !char.IsDigit(_s[at + 4]));

        private char Peek()
        {
            SkipSpaces();
            return _i < _s.Length ? _s[_i] : '\0';
        }

        private void SkipSpaces()
        {
            while (_i < _s.Length && _s[_i] == ' ')
                _i++;
        }

        private static bool IsIdentStart(char c) => char.IsLetter(c);

        private static double One(double[] a) => a.Length == 1 ? a[0] : throw new FormatException();

        private static double Many(double[] a, Func<double, double, double> f)
        {
            var r = a[0];
            for (var k = 1; k < a.Length; k++)
                r = f(r, a[k]);
            return r;
        }

        /// <summary>round(3.14159, 2) = 3.14, round(1234, -2) = 1200.</summary>
        private static double RoundTo(double x, double digits)
        {
            if (digits != Math.Floor(digits))
                throw new FormatException();
            if (digits >= 0)
                return Math.Round(x, (int)Math.Min(digits, 15), MidpointRounding.AwayFromZero); // a double has no more
            var scale = Math.Pow(10, -digits);
            return Math.Round(x / scale, MidpointRounding.AwayFromZero) * scale;
        }

        private static double Factorial(double x)
        {
            if (x < 0 || x > 170 || x != Math.Floor(x))
                throw new FormatException();
            double r = 1;
            for (var k = 2; k <= x; k++)
                r *= k;
            return r;
        }
    }
}
