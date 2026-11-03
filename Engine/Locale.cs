using System;
using System.Globalization;

namespace Flow.Launcher.Plugin.SmartCalc.Engine
{
    /// <summary>
    /// How answers read: number format and wording. Input is understood in both languages either way.
    /// </summary>
    public sealed class Locale
    {
        public static readonly Locale English = new(
            isRussian: false, decimalSeparator: ".", groupSeparator: ",", percent: "%",
            copyHint: "Enter to copy",
            percentOf: "{0}% of {1}", ratio: "{0} of {1}", asPercent: "{0} as a percentage",
            change: "{0} → {1}, difference {2}", decimalBytes: "counting in 1000s, as drive makers do",
            copyOther: "Copy with a comma: {0}", copyOtherHint: "for spreadsheets that use a decimal comma",
            durationUnits: new[] { "y", "d", "h", "min", "s" });

        public static readonly Locale Russian = new(
            // narrow no-break spaces: 1 208,32 and 116,67 % never wrap apart
            isRussian: true, decimalSeparator: ",", groupSeparator: " ", percent: " %",
            copyHint: "Enter — скопировать",
            percentOf: "{0} % от {1}", ratio: "{0} от {1}", asPercent: "{0} в процентах",
            change: "{0} → {1}, разница {2}", decimalBytes: "если считать по 1000, как производители дисков",
            copyOther: "Скопировать с точкой: {0}", copyOtherHint: "для кода и английских таблиц",
            durationUnits: new[] { "г", "д", "ч", "мин", "с" });

        private readonly NumberFormatInfo _display;
        private readonly NumberFormatInfo _copy;

        private Locale(bool isRussian, string decimalSeparator, string groupSeparator, string percent,
            string copyHint, string percentOf, string ratio, string asPercent, string change, string decimalBytes,
            string copyOther, string copyOtherHint, string[] durationUnits)
        {
            IsRussian = isRussian;
            Percent = percent;
            CopyHint = copyHint;
            PercentOf = percentOf;
            Ratio = ratio;
            AsPercent = asPercent;
            Change = change;
            DecimalBytes = decimalBytes;
            CopyOther = copyOther;
            CopyOtherHint = copyOtherHint;
            DurationUnits = durationUnits;
            _display = new NumberFormatInfo { NumberDecimalSeparator = decimalSeparator, NumberGroupSeparator = groupSeparator, NegativeSign = "−" };
            _copy = new NumberFormatInfo { NumberDecimalSeparator = decimalSeparator, NegativeSign = "-" };
        }

        public bool IsRussian { get; }
        public string Percent { get; }
        public string CopyHint { get; }
        public string PercentOf { get; }
        public string Ratio { get; }
        public string AsPercent { get; }
        public string Change { get; }
        public string DecimalBytes { get; }
        public string CopyOther { get; }
        public string CopyOtherHint { get; }
        public string[] DurationUnits { get; }

        public static Locale For(string languageCode) =>
            languageCode != null && languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? Russian : English;

        /// <summary>The copied value with the other decimal separator, for the context menu.</summary>
        public string SwapDecimal(string copied) => IsRussian ? copied.Replace(',', '.') : copied.Replace('.', ',');

        /// <summary>Up to 10 significant digits, so 0.1 + 0.2 shows 0.3.</summary>
        public string Number(double v, int decimals = -1) => Text(Round(v, decimals), _display, "#,0.##########");

        /// <summary>Unit results: 6 significant digits keep 1,208.32 MB readable.</summary>
        public string Amount(double v) => Text(Significant(v, 6), _display, "#,0.######");

        public string Copy(double v, int decimals = -1) => Text(Round(v, decimals), _copy, "0.##########");

        public string CopyAmount(double v) => Text(Significant(v, 6), _copy, "0.######");

        private static string Text(double v, NumberFormatInfo f, string pattern)
        {
            var a = Math.Abs(v);
            if (a != 0 && (a >= 1e15 || a < 1e-9))
                return v.ToString("0.######E+0", f);
            var s = v.ToString(pattern, f);
            return s == "−0" || s == "-0" ? "0" : s;
        }

        private static double Round(double v, int decimals) =>
            decimals >= 0 ? Math.Round(v, decimals, MidpointRounding.AwayFromZero) : Significant(v, 10);

        private static double Significant(double v, int digits) =>
            v == 0 ? 0 : double.Parse(v.ToString("G" + digits, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
