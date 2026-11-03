using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Flow.Launcher.Plugin.SmartCalc.Engine;

namespace Flow.Launcher.Plugin.SmartCalc
{
    public class Main : IPlugin, IContextMenu, IPluginI18n
    {
        private sealed record Picked(Answer Answer, Locale Locale);

        private const string BuiltInCalculatorId = "CEA0FDFC6D3B4085823D60DC76F28855";
        // Above program and file matches: a query that parses as maths is almost always maths
        private const int TopScore = 1000;

        private PluginInitContext _context;
        private string _regionalFormat;

        public void Init(PluginInitContext context)
        {
            _context = context;
            _regionalFormat = RegionalFormat();
        }

        public List<Result> Query(Query query)
        {
            var locale = Calculator.PickLocale(query.Search, _regionalFormat);
            return Calculator.Evaluate(query.Search, locale, plainArithmetic: !BuiltInCalculatorEnabled())
                .Select((a, i) => new Result
                {
                    Title = a.Title,
                    SubTitle = a.SubTitle,
                    IcoPath = "Images\\icon.png",
                    Score = TopScore - i,
                    CopyText = a.Copy,
                    ContextData = new Picked(a, locale),
                    Action = _ => Copy(a.Copy),
                })
                .ToList();
        }

        public List<Result> LoadContextMenus(Result selected)
        {
            if (selected.ContextData is not Picked (var a, var locale))
                return new List<Result>();

            var other = locale.SwapDecimal(a.Copy);
            return new List<Result>
            {
                new()
                {
                    Title = string.Format(locale.CopyOther, other),
                    SubTitle = locale.CopyOtherHint,
                    IcoPath = "Images\\icon.png",
                    Action = _ => Copy(other),
                },
            };
        }

        public string GetTranslatedPluginTitle() => _context.API.GetTranslation("flowlauncher_plugin_smartcalc_name");

        public string GetTranslatedPluginDescription() => _context.API.GetTranslation("flowlauncher_plugin_smartcalc_description");

        private bool BuiltInCalculatorEnabled() =>
            _context.API.GetAllPlugins().Any(p => p.Metadata.ID == BuiltInCalculatorId && !p.Metadata.Disabled);

        private bool Copy(string text)
        {
            _context.API.CopyToClipboard(text, directCopy: true, showDefaultNotification: false);
            return true;
        }

        /// <summary>
        /// The user's "Regional format" (ru-RU, en-US...). Flow replaces the thread culture with its own
        /// interface language, so CultureInfo.CurrentCulture cannot be trusted here.
        /// </summary>
        private static string RegionalFormat()
        {
            var name = new StringBuilder(85);
            return GetUserDefaultLocaleName(name, name.Capacity) > 0 ? name.ToString() : "en-US";
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetUserDefaultLocaleName(StringBuilder localeName, int size);
    }
}
