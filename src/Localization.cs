// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace HorizonSync
{
    public static class Localization
    {
        static readonly Dictionary<string,string> english = Load();
        public static string Choice = "auto";
        public static bool IsEnglish { get { return Choice == "en" || (Choice != "zh-CN" && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "zh"); } }
        static Dictionary<string,string> Load()
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("HorizonSync.Locales.en.json"))
            {
                if(stream==null)throw new InvalidDataException("The English language resource is missing.");
                using(var reader=new StreamReader(stream))return new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(reader.ReadToEnd());
            }
        }
        public static void Use(string choice) { Choice=choice=="en"||choice=="zh-CN"?choice:"auto"; }
        public static string T(string source)
        {
            string result;
            return IsEnglish && english.TryGetValue(source,out result)?result:source;
        }
    }
}
