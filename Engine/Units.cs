using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.SmartCalc.Engine
{
    internal enum Kind { Time, Data, Rate, Length, Mass, Volume, Area, Speed, Temperature }

    /// <summary>
    /// base = value * Factor + Offset. Offset is only non-zero for temperatures (base is °C).
    /// Id is the Russian display name and also the key Partner refers to.
    /// </summary>
    internal sealed record Unit(Kind Kind, string Id, string En, double Factor, string[] Aliases, string Partner = null, double Offset = 0)
    {
        public double ToBase(double v) => v * Factor + Offset;
        public double FromBase(double b) => (b - Offset) / Factor;
        public string Name(Locale locale) => locale.IsRussian ? Id : En;
    }

    internal static class Units
    {
        private const double Ki = 1024, Mi = Ki * Ki, Gi = Mi * Ki, Ti = Gi * Ki;

        // Bytes use powers of 1024 like Windows Explorer; network bits use powers of 1000 like ISPs
        public static readonly Unit[] All =
        {
            new(Kind.Time, "мс", "ms", 0.001, new[] { "ms", "мс", "msec" }),
            new(Kind.Time, "с", "s", 1, new[] { "s", "sec", "secs", "second", "seconds", "с", "сек", "секунд", "секунды", "секунда" }),
            new(Kind.Time, "мин", "min", 60, new[] { "min", "mins", "minute", "minutes", "мин", "минут", "минуты", "минута" }),
            new(Kind.Time, "ч", "h", 3600, new[] { "h", "hr", "hrs", "hour", "hours", "ч", "час", "часа", "часов" }),
            new(Kind.Time, "д", "d", 86400, new[] { "d", "day", "days", "д", "дн", "день", "дня", "дней", "сут", "суток", "сутки" }),
            new(Kind.Time, "нед", "wk", 604800, new[] { "w", "wk", "week", "weeks", "нед", "неделя", "недели", "недель" }),
            new(Kind.Time, "мес", "mo", 2629746, new[] { "mo", "month", "months", "мес", "месяц", "месяца", "месяцев" }),
            new(Kind.Time, "лет", "yr", 31556952, new[] { "y", "yr", "year", "years", "год", "года", "лет" }),

            new(Kind.Data, "бит", "bit", 0.125, new[] { "bit", "bits", "бит", "бита", "битов" }),
            new(Kind.Data, "Б", "B", 1, new[] { "b", "byte", "bytes", "б", "байт", "байта", "байтов" }),
            new(Kind.Data, "КБ", "KB", Ki, new[] { "kb", "kib", "кб", "кбайт", "килобайт", "килобайта", "килобайтов" }),
            new(Kind.Data, "МБ", "MB", Mi, new[] { "mb", "mib", "мб", "мбайт", "мегабайт", "мегабайта", "мегабайтов" }),
            new(Kind.Data, "ГБ", "GB", Gi, new[] { "gb", "gib", "гб", "гбайт", "гигабайт", "гигабайта", "гигабайтов" }),
            new(Kind.Data, "ТБ", "TB", Ti, new[] { "tb", "tib", "тб", "тбайт", "терабайт", "терабайта", "терабайтов" }),
            new(Kind.Data, "Кбит", "Kbit", 1e3 / 8, new[] { "kbit", "кбит" }, "КБ"),
            new(Kind.Data, "Мбит", "Mbit", 1e6 / 8, new[] { "mbit", "мбит", "мегабит" }, "МБ"),
            new(Kind.Data, "Гбит", "Gbit", 1e9 / 8, new[] { "gbit", "гбит", "гигабит" }, "ГБ"),

            new(Kind.Rate, "Кбит/с", "Kbps", 1e3 / 8, new[] { "kbps", "kbit/s", "кбит/с" }, "КБ/с"),
            new(Kind.Rate, "Мбит/с", "Mbps", 1e6 / 8, new[] { "mbps", "mbit/s", "мбит/с", "мбитс" }, "МБ/с"),
            new(Kind.Rate, "Гбит/с", "Gbps", 1e9 / 8, new[] { "gbps", "gbit/s", "гбит/с" }, "МБ/с"),
            new(Kind.Rate, "КБ/с", "KB/s", Ki, new[] { "kb/s", "кб/с" }, "Кбит/с"),
            new(Kind.Rate, "МБ/с", "MB/s", Mi, new[] { "mb/s", "мб/с" }, "Мбит/с"),
            new(Kind.Rate, "ГБ/с", "GB/s", Gi, new[] { "gb/s", "гб/с" }, "Гбит/с"),

            new(Kind.Length, "мм", "mm", 0.001, new[] { "mm", "мм" }, "дюйм"),
            new(Kind.Length, "см", "cm", 0.01, new[] { "cm", "см" }, "дюйм"),
            new(Kind.Length, "м", "m", 1, new[] { "m", "м", "meter", "meters", "metre", "metres", "метр", "метра", "метров" }, "фут"),
            new(Kind.Length, "км", "km", 1000, new[] { "km", "км" }, "миля"),
            new(Kind.Length, "дюйм", "in", 0.0254, new[] { "in", "inch", "inches", "\"", "дюйм", "дюйма", "дюймов" }, "см"),
            new(Kind.Length, "фут", "ft", 0.3048, new[] { "ft", "foot", "feet", "фут", "фута", "футов" }, "м"),
            new(Kind.Length, "ярд", "yd", 0.9144, new[] { "yd", "yard", "yards", "ярд", "ярда", "ярдов" }, "м"),
            new(Kind.Length, "миля", "mi", 1609.344, new[] { "mi", "mile", "miles", "миля", "мили", "миль" }, "км"),

            new(Kind.Mass, "мг", "mg", 1e-6, new[] { "mg", "мг" }),
            new(Kind.Mass, "г", "g", 0.001, new[] { "g", "gr", "gram", "grams", "г", "гр", "грамм", "грамма", "граммов" }, "унция"),
            new(Kind.Mass, "кг", "kg", 1, new[] { "kg", "кг", "кило" }, "фунт"),
            new(Kind.Mass, "т", "t", 1000, new[] { "t", "т", "тонна", "тонны", "тонн", "ton", "tons", "tonne", "tonnes" }),
            new(Kind.Mass, "фунт", "lb", 0.45359237, new[] { "lb", "lbs", "pound", "pounds", "фунт", "фунта", "фунтов" }, "кг"),
            new(Kind.Mass, "унция", "oz", 0.028349523125, new[] { "oz", "ounce", "ounces", "унция", "унции", "унций" }, "г"),

            new(Kind.Volume, "мл", "ml", 0.001, new[] { "ml", "мл" }),
            new(Kind.Volume, "л", "L", 1, new[] { "l", "л", "liter", "liters", "litre", "litres", "литр", "литра", "литров" }, "галлон"),
            new(Kind.Volume, "галлон", "gal", 3.785411784, new[] { "gal", "gallon", "gallons", "галлон", "галлона", "галлонов" }, "л"),

            new(Kind.Area, "см²", "cm²", 1e-4, new[] { "cm2", "см2" }),
            new(Kind.Area, "м²", "m²", 1, new[] { "m2", "м2", "sqm" }, "фут²"),
            new(Kind.Area, "км²", "km²", 1e6, new[] { "km2", "км2" }),
            new(Kind.Area, "га", "ha", 1e4, new[] { "ha", "га", "гектар", "гектара", "гектаров" }, "м²"),
            new(Kind.Area, "сот", "a", 100, new[] { "are", "ares", "сотка", "сотки", "соток", "сот" }, "м²"),
            new(Kind.Area, "фут²", "ft²", 0.09290304, new[] { "ft2", "sqft" }, "м²"),
            new(Kind.Area, "акр", "acre", 4046.8564224, new[] { "acre", "acres", "акр", "акра", "акров" }, "м²"),

            new(Kind.Speed, "м/с", "m/s", 1, new[] { "m/s", "м/с", "mps" }, "км/ч"),
            new(Kind.Speed, "км/ч", "km/h", 1 / 3.6, new[] { "km/h", "kmh", "kph", "км/ч", "кмч" }, "м/с"),
            new(Kind.Speed, "миль/ч", "mph", 0.44704, new[] { "mph", "миль/ч" }, "км/ч"),
            new(Kind.Speed, "уз", "kn", 0.514444, new[] { "kn", "kt", "knot", "knots", "узел", "узла", "узлов" }, "км/ч"),

            new(Kind.Temperature, "°C", "°C", 1, new[] { "c", "°c", "℃", "celsius", "цельсий", "цельсия" }, "°F"),
            new(Kind.Temperature, "°F", "°F", 5.0 / 9, new[] { "f", "°f", "℉", "fahrenheit", "фаренгейт", "фаренгейта" }, "°C", -160.0 / 9),
            new(Kind.Temperature, "K", "K", 1, new[] { "k", "kelvin", "кельвин", "кельвина", "кельвинов" }, "°C", -273.15),
        };

        private static readonly Dictionary<string, Unit> ByAlias = All
            .SelectMany(u => u.Aliases.Select(a => (a, u)))
            .ToDictionary(p => p.a, p => p.u);

        private static readonly Dictionary<string, Unit> ById = All.ToDictionary(u => u.Id);

        public static readonly Unit[] Bytes = All.Where(u => u.Kind == Kind.Data && u.Id.EndsWith("Б")).OrderBy(u => u.Factor).ToArray();

        public static Unit Find(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var key = text.Trim().ToLowerInvariant().Replace(" ", "").Replace("²", "2").Replace("кв.м", "m2");
            if (key.Length > 1 && key.EndsWith('.'))
                key = key[..^1]; // "сек.", "мин."
            return ByAlias.GetValueOrDefault(key);
        }

        public static Unit Named(string id) => id == null ? null : ById.GetValueOrDefault(id);
    }
}
