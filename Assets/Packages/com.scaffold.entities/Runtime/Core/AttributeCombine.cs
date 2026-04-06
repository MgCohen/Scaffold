using System.Collections.Generic;
using System.Globalization;

namespace Scaffold.Entities
{
    internal static class AttributeCombine
    {
        public static string Combine(string basePayload, List<string> contributions)
        {
            if (contributions == null || contributions.Count == 0)
            {
                return basePayload ?? string.Empty;
            }

            if (TrySumAsFloats(basePayload, contributions, out string summed))
            {
                return summed;
            }

            return Concatenate(basePayload, contributions);
        }

        private static bool TrySumAsFloats(string basePayload, List<string> contributions, out string result)
        {
            result = string.Empty;
            if (!TryParseFloat(basePayload, out float acc))
            {
                return false;
            }

            for (int i = 0; i < contributions.Count; i++)
            {
                if (!TryParseFloat(contributions[i], out float delta))
                {
                    return false;
                }

                acc += delta;
            }

            result = acc.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static string Concatenate(string basePayload, List<string> contributions)
        {
            string result = basePayload ?? string.Empty;
            for (int i = 0; i < contributions.Count; i++)
            {
                result += contributions[i] ?? string.Empty;
            }

            return result;
        }

        private static bool TryParseFloat(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
