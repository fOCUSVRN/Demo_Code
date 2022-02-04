using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastReport;
using FastReport.Data;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace FiscalViewer.Services
{
    public static class ReportTranslator
    {
        private static async Task<Dictionary<string, string>> FromFile(string fileName)
        {
            var raw = await File.ReadAllTextAsync(fileName, Encoding.UTF8);

            var res = JsonConvert.DeserializeObject<Dictionary<string, string>>(raw);

            return res;
        }

        public static async Task<Dictionary<string, string>> GetFrontLanguageDictionary(string lang, string path)
        {
            var file = Path.Combine(path, $"{lang}.json");

            if (!File.Exists(file))
            {
                file = Path.Combine(path, "en.json");
            }

            return await FromFile(file);
        }


        public static Task<Dictionary<string, string>> GetFrontLanguageDictionary(string lang) => GetFrontLanguageDictionary(lang, Path.Combine(Program.RealPath, "i18n", "FastReport"));


        public static string Translate(Dictionary<string, string> translateDictionary, string val)
        {
            foreach (var pair in translateDictionary)
            {
                var match = $"%{pair.Key}%";

                if (val.Contains(match, StringComparison.CurrentCultureIgnoreCase))
                {
                    val = val.Replace(match, translateDictionary[pair.Key]);
                }
            }

            return val;
        }

        public static bool IsTranslated(Dictionary<string, string> translateDictionary, string val, out string newVal)
        {
            var isChanged = false;

            newVal = val;

            foreach (var pair in translateDictionary)
            {
                var match = $"%{pair.Key}%";

                if (newVal.Contains(match, StringComparison.CurrentCultureIgnoreCase))
                {
                    newVal = newVal.Replace(match, translateDictionary[pair.Key]);
                    isChanged = true;
                }
            }

            return isChanged;
        }


        public static async Task TranslateReport(Report report, string lang)
        {
            var translateDictionary = await GetFrontLanguageDictionary(lang);


            foreach (var obj in report.AllObjects)
            {
                if (!(obj is TextObject textObject))
                {
                    continue;
                }


                if (!textObject.Text.Contains("%"))
                {
                    continue;
                }

                textObject.Text = Translate(translateDictionary, textObject.Text);
            }


            // дорого так переводить....
            foreach (var conn in report.Dictionary.Connections)
            {
                if (!(conn is DataConnectionBase dbConn))
                {
                    continue;
                }

                foreach (var table in dbConn.Tables)
                {
                    if (!(table is TableDataSource tableDs))
                    {
                        continue;
                    }

                    if (!tableDs.SelectCommand.Contains("%"))
                    {
                        continue;
                    }

                    foreach (var pair in translateDictionary)
                    {
                        var match = $"%{pair.Key}%";

                        if (tableDs.SelectCommand.Contains(match, StringComparison.CurrentCultureIgnoreCase))
                        {
                            tableDs.SelectCommand = tableDs.SelectCommand.Replace(match, translateDictionary[pair.Key]);
                        }
                    }
                }
            }
        }
    }
}
