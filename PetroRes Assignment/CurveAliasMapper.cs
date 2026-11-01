using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PetroRes_Assignment
{
    public class CurveAliasMapper
    {
        private readonly Dictionary<string, string[]> aliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "GammaRay", new[] { "GR", "GAPI", "GR_EDTC" } },
                { "Resistivity", new[] { "RLLD", "ILD", "LLD", "RT" } },
                { "Density", new[] { "RHOB" } },
                { "Neutron", new[] { "NPHI" } }
            };

        public string FindCurveName(
            List<CurveInfo> curves,
            string curveType)
        {
            if (!aliases.ContainsKey(curveType))
                return null;

            foreach (string alias in aliases[curveType])
            {
                foreach (CurveInfo curve in curves)
                {
                    if (string.Equals(
                        curve.Mnemonic?.Trim(),
                        alias,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return curve.Mnemonic;
                    }
                }
            }

            return null;
        }

        public string CheckUnit(CurveInfo curve, string curveType)
        {
            if (curve == null)
                return "Curve not found";

            string unit = (curve.Unit ?? "").Trim().ToUpperInvariant();

            switch (curveType)
            {
                case "GammaRay":
                    if (unit == "API" || unit == "GAPI")
                        return "Valid";
                    break;

                case "Resistivity":
                    if (unit == "OHMM" || unit == "OHM.M")
                        return "Valid";
                    break;

                case "Density":
                    if (unit == "G/C3" || unit == "G/CC")
                        return "Valid";
                    break;

                case "Neutron":
                    if (unit == "V/V")
                        return "Valid";

                    if (unit == "%" || unit == "PU")
                        return "Needs conversion";
                    break;
            }

            return "Unknown or unsupported unit: " + curve.Unit;
        }
    }
}
