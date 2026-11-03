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
            ["round"] = a => a.Length == 2 ? Math.Round(a[0], (int)a[1], MidpointRounding.AwayFromZero) : Math.Round(One(a), MidpointRounding.AwayFromZero),
            ["floor"] = a => Math.Floor(One(a)), ["ceil"] = a => Math.Ceiling(One(a)),
            ["min"] = a => Many(a, Math.Min), ["max"] = a => Many(a, Math.Max),
        };

        private readonly string _s;
        private int _i;
        private bool _sawOperator;

        private Parser(string s) => _s = s;

        /// <summary>sqrt, pi and the like say nothing about which language the query is in.</summary>
        public static bool IsMathWord(string word) => Constants.ContainsKey(word) || Functions.ContainsKey(word);

        /// <param name="hasOperator">false for a bare number, which is not worth answering</param>
        public static bool TryEval(string text, out Value value, out bool hasOperator)
        {
            value = default;
            hasOperator = false;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var p = new Parser(text);
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
                else if (c == '(' || IsIdentStart(c))
                {
                    // implicit multiplication: 2(3+1), 2pi
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
            if (char.IsDigit(c) || (c == '.' || c == ',') && _i + 1 < _s.Length && char.IsDigit(_s[_i + 1]))
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
                    return new Value(f(Args()), false);
                }
            }
            throw new FormatException();
        }

        private double[] Args()
        {
            if (Peek() != '(')
                return new[] { Postfix().X }; // sqrt 16, sin 30°

            _i++;
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

            // "1,000.50": a comma before three digits and a point is a thousands separator
            while (_i + 4 < _s.Length && _s[_i] == ',' && IsDigits(_i + 1, 3) && _s[_i + 4] == '.')
            {
                sb.Append(_s, _i + 1, 3);
                _i += 4;
            }

            if (_i + 1 < _s.Length && (_s[_i] == '.' || _s[_i] == ',') && char.IsDigit(_s[_i + 1]))
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
