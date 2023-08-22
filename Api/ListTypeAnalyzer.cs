using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocumentaConAI.Api
{
    public static class ListTypeAnalyzer
    {
        private static readonly List<string> ListTypes = new List<string>
        {
            "IEnumerable",
            "List",
            "ICollection",
            "IEnumerator"
        };

        private static readonly string ListPattern = @"^(?<listType>" + string.Join("|", ListTypes) + @")\<(?<genericType>[^\>]+)\>$";

        public static bool IsListType(string typeDescription)
        {
            return Regex.IsMatch(typeDescription, ListPattern);
        }

        public static string ExtractGenericType(string typeDescription)
        {
            var match = Regex.Match(typeDescription, ListPattern);
            if (match.Success)
            {
                return match.Groups["genericType"].Value;
            }
            throw new ArgumentException("The provided type description is not a recognized list type.");
        }
    }
}
