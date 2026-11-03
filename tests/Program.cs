using System;
using System.Linq;
using System.Text.RegularExpressions;
using Flow.Launcher.Plugin.SmartCalc.Engine;

var failed = 0;

void Check(Locale locale, bool builtInCalculator, string query, string[] expected)
{
    // compare without spaces: Russian digit grouping uses narrow no-break spaces
    static string Squash(string s) => Regex.Replace(s, @"\s", "");
    var got = Calculator.Evaluate(query, locale, () => builtInCalculator).Select(a => Squash(a.Title)).ToArray();
    expected = expected.Select(Squash).ToArray();
    var ok = expected.Length == 0 ? got.Length == 0 : got.Take(expected.Length).SequenceEqual(expected);
    if (!ok)
        failed++;
    var lang = locale.IsRussian ? "ru" : "en";
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {lang} {query,-36} → {string.Join(" | ", got)}{(ok ? "" : "   expected: " + string.Join(" | ", expected))}");
}

void Ru(string query, params string[] expected) => Check(Locale.Russian, false, query, expected);
void En(string query, params string[] expected) => Check(Locale.English, false, query, expected);
void WithBuiltIn(string query, params string[] expected) => Check(Locale.English, true, query, expected);

// arithmetic and Google-style percentages
Ru("520+2%", "530,4");
Ru("520-2%", "509,6");
Ru("370 + 2%", "377,4");
Ru("90*200*15%", "2700");
Ru("1,18*1024", "1208,32");
Ru("71/60", "1,183333333");
Ru("3.86*3", "11,58");
Ru("330+330/2+1780/2", "1385");
Ru("330+330/2=", "495");
Ru("1 385-1220", "165");
Ru("1 385-1220", "165");
Ru("2^10", "1024");
Ru("sqrt(16)+2", "6");
Ru("(2+3)*4", "20");
Ru("0.1+0.2", "0,3");
Ru("3x4", "12");
Ru("1920х1080", "2073600");
Ru("round(3.14159, 2)*1", "3,14");
Ru("5!", "120");
Ru("10/0");
Ru("round(3.14, 20)", "3,14");
Ru("round(1234, -2)", "1200");
Ru("round(3,14159; 2)", "3,14");
Ru("max(1,2)", "2");
Ru("log(8,2)", "3");
Ru("sqrt(2,25)", "1,5");
Ru("1,000*2", "2");
Ru("2pi", "6,283185307");
En("1,000.50+1", "1,001.5");
En("1,000*2", "2,000");
En("1,000,000.5+1", "1,000,001.5");
En("1,5*2", "3");
En("2*pi", "6.283185307");

// phrases
Ru("4.2 out of 3.6 how much percent", "116,67%");
Ru("4,2 от 3,6 в %", "116,67%");
Ru("сколько процентов 4,2 от 3,6", "116,67%");
Ru("15% от 200", "30");
Ru("от 1220 до 1385", "+13,52%");
En("15% of 200", "30");
En("4.2 out of 3.6", "116.67%");
En("what percent is 30 of 120", "25%");
En("from 1385 to 1220", "−11.91%");
En("45/60 in %", "75%");

// units
Ru("71 мин в ч", "1,18333ч", "1ч11мин");
Ru("71 мин", "1ч11мин", "1,18333ч");
Ru("90 сек", "1мин30с", "1,5мин");
Ru("1,18 гб в мб", "1208,32МБ", "1180МБ");
Ru("1,18 гб", "1208,32МБ");
Ru("50000 мб", "48,8281ГБ");
Ru("100 мбит/с в мб/с", "11,9209МБ/с");
Ru("-40 f в c", "−40°C");
Ru("36,6 c в f", "97,88°F");
Ru("180 см в дюйм", "70,8661дюйм");
Ru("2 кг в фунт", "4,40925фунт");
Ru("60 км/ч в м/с", "16,6667м/с");
Ru("6 соток в м2", "600м²");
En("71 min to h", "1.18333h", "1h11min");
En("1.18 gb to mb", "1,208.32MB", "1,180MB");
En("100 mbps", "11.9209MB/s");
En("100 c", "212°F");
En("10 km in mi", "6.21371mi");
En("5 ft", "1.524m");
En("3 hours", "180min");
En("300 kelvin", "26.85°C");

// with Flow's own calculator enabled, leave plain arithmetic to it, but not what only this plugin reads
WithBuiltIn("2+2");
WithBuiltIn("(2+3)*4");
WithBuiltIn("520+2%", "530.4");
WithBuiltIn("1 385-1220", "165");
WithBuiltIn("3x4", "12");
WithBuiltIn("2pi", "6.283185307");
WithBuiltIn("1,000*2", "2,000");
WithBuiltIn("1.18 gb to mb", "1,208.32MB", "1,180MB");

// answer language follows the query, bare maths follows Windows' regional format
void Picks(string query, string regional, bool russian)
{
    var ok = Calculator.PickLocale(query, regional).IsRussian == russian;
    if (!ok)
        failed++;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} pick {query,-24} ({regional}) → {(russian ? "ru" : "en")}");
}
Picks("71 мин в ч", "en-US", true);
Picks("71 min to h", "ru-RU", false);
Picks("520+2%", "ru-RU", true);
Picks("520+2%", "en-US", false);
Picks("sqrt(16)*pi", "ru-RU", true);
Picks("3x4", "ru-RU", true);

// must stay silent: other plugins own these
foreach (var q in new[] { "100", "h", "lab01", "1000 rub", "100 usd to kzt", "5000 руб в евро", "12:30", "e", "pi",
                          "throne exe", "2024", "d:\\резюме\\", "lab01 pdf", "test-aggro.cmd", "windows 11", "mp3", "7zip",
                          "2024-01-15", "15/01/2024", "8-800-555-35-35", "+7-900-123-45-67", "10 e", "5k", "100k",
                          new string('(', 30000) + "1", new string('-', 30000) + "1" })
    Ru(q);

Console.WriteLine(failed == 0 ? "\nall passed" : $"\nfailed: {failed}");
return failed == 0 ? 0 : 1;
