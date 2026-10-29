using System.Text.RegularExpressions;

namespace Sherlock.Core.Analysis
{
    public static class SignatureNormalizer
    {
        private static readonly Regex Guid = new(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled);

        private static readonly Regex QuotedString = new("'[^']*'|\"[^\"]*\"", RegexOptions.Compiled);
        private static readonly Regex Number = new(@"\d+", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string message)
        {
            var result = Guid.Replace(message, "#GUID#");
            result = QuotedString.Replace(result, "#VALUE#");
            result = Number.Replace(result, "#");
            result = Whitespace.Replace(result, " ").Trim();
            return result;
        }
    }
}
