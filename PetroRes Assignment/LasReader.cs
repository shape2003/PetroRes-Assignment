using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PetroRes_Assignment
{
    public class LasReader
    {
        public string ReadFile(string filepath)
        {
            return System.IO.File.ReadAllText(filepath);
        }
        public List<string> ReadLines(string filePath)
        {
            return File.ReadAllLines(filePath).ToList();
        }
        public List<string> GetSections(string filePath)
        {
            List<string> lines = ReadLines(filePath);
            List<string> sections = new List<string>();

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("~"))
                {
                    sections.Add(trimmedLine);
                }
            }

            return sections;
        }

        //========================================================================================================================================//

        public WellInfo ReadWellInfo(string filePath)
        {
            List<string> lines = ReadLines(filePath);
            WellInfo well = new WellInfo();

            bool insideWellSection = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed == "" || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("~"))
                {
                    string section = trimmed.Substring(1).Trim();

                    insideWellSection =
                        section.Equals("W", StringComparison.OrdinalIgnoreCase) ||
                        section.StartsWith("WELL", StringComparison.OrdinalIgnoreCase);

                    continue;
                }

                if (!insideWellSection)
                    continue;

                // แยกข้อมูลก่อนและหลัง :
                string[] parts = trimmed.Split(
                    new char[] { ':' }, 2
                );

                string[] columns = parts[0].Split(
                    new char[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries
                );

                if (columns.Length < 2)
                    continue;

                // รองรับทั้ง STRT.M และ STRT .M
                string mnemonic = columns[0]
                    .Split('.')[0]
                    .ToUpperInvariant();

                // กรณีข้อความ ให้อ่านจาก Description
                string description = "";

                if (parts.Length > 1)
                    description = parts[1].Trim();

                // ค่าตัวเลขอยู่ท้ายข้อมูลก่อน :
                string value = columns[columns.Length - 1];

                double number;

                if (double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number))
                {
                    switch (mnemonic)
                    {
                        case "STRT":
                            well.StartDepth = number;
                            break;

                        case "STOP":
                            well.StopDepth = number;
                            break;

                        case "STEP":
                            well.Step = number;
                            break;

                        case "NULL":
                            well.NullValue = number;
                            break;

                        case "LATI":
                            well.Latitude = number;
                            break;

                        case "LONG":
                            well.Longitude = number;
                            break;
                    }
                }
                else
                {
                    switch (mnemonic)
                    {
                        case "COMP":
                            well.Company = description;
                            break;

                        case "WELL":
                            well.WellName = description;
                            break;

                        case "FLD":
                            well.Field = description;
                            break;

                        case "LOC":
                            well.Location = description;
                            break;

                        case "CNTY":
                            well.County = description;
                            break;

                        case "STAT":
                            well.State = description;
                            break;

                        case "CTRY":
                            well.Country = description;
                            break;

                        case "SRVC":
                            well.ServiceCompany = description;
                            break;

                        case "API":
                            well.ApiNumber = description;
                            break;

                        case "DATE":
                            well.LogDate = description;
                            break;

                        case "UWI":
                            well.Uwi = description;
                            break;
                    }
                }
            }

            return well;
        }

        //========================================================================================================================================//

        public List<CurveInfo> ReadCurves(string filePath)
        {
            List<string> lines = ReadLines(filePath);
            List<CurveInfo> curves = new List<CurveInfo>();

            bool insideCurveSection = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed == "")
                    continue;

                if (trimmed.StartsWith("~"))
                {
                    string section = trimmed.Substring(1).Trim();

                    insideCurveSection =
                        section.Equals("C", StringComparison.OrdinalIgnoreCase) ||
                        section.StartsWith("CURVE", StringComparison.OrdinalIgnoreCase);

                    continue;
                }

                if (!insideCurveSection || trimmed.StartsWith("#"))
                    continue;

                string[] parts = trimmed.Split(new char[] { ':' }, 2);

                string curveText = parts[0].Trim();

                // แยก Mnemonic และ Unit ด้วยจุด
                int dotIndex = curveText.IndexOf('.');

                if (dotIndex < 0)
                    continue;

                CurveInfo curve = new CurveInfo();

                curve.Mnemonic = curveText.Substring(0, dotIndex).Trim();

                string remaining = curveText.Substring(dotIndex + 1);

                curve.Unit = "";
                curve.ApiCode = "";

                if (remaining.Length > 0)
                {
                    if (!char.IsWhiteSpace(remaining[0]))
                    {
                        int spaceIndex = remaining.IndexOfAny(
                            new char[] { ' ', '\t' }
                        );

                        if (spaceIndex >= 0)
                        {
                            curve.Unit = remaining.Substring(0, spaceIndex);
                            curve.ApiCode = remaining.Substring(spaceIndex).Trim();
                        }
                        else
                        {
                            curve.Unit = remaining;
                        }
                    }
                    else
                    {
                        curve.ApiCode = remaining.Trim();
                    }
                }

                if (parts.Length > 1)
                    curve.Description = parts[1].Trim();

                curves.Add(curve);
            }

            return curves;
        }

        //========================================================================================================================================//

        public List<CurveData> ReadData(string filePath)
        {
            List<string> lines = ReadLines(filePath);
            List<CurveData> allData = new List<CurveData>();

            WellInfo well = ReadWellInfo(filePath);
            int expectedColumns = ReadCurves(filePath).Count;

            IsWrapped = ReadWrapMode(filePath);
            InvalidRowCount = 0;

            bool useWrappedParsing = IsWrapped == true;

            if (IsWrapped == false &&
                HasMultilineRecords(lines, expectedColumns, well.Step))
            {
                useWrappedParsing = true;
            }

            UsedWrappedParsing = useWrappedParsing;

            if (expectedColumns == 0)
                return allData;

            bool insideDataSection = false;
            List<string> rowParts = new List<string>();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed == "" || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("~"))
                {
                    string section = trimmed.Substring(1).Trim();

                    string[] sectionParts = section.Split(
                        new char[] { ' ', '\t' },
                        StringSplitOptions.RemoveEmptyEntries
                    );

                    if (sectionParts.Length == 0)
                    {
                        insideDataSection = false;
                        continue;
                    }

                    string sectionName = sectionParts[0];

                    insideDataSection =
                        sectionName.Equals("A", StringComparison.OrdinalIgnoreCase) ||
                        sectionName.StartsWith("ASCII", StringComparison.OrdinalIgnoreCase);

                    continue;
                }

                if (!insideDataSection)
                    continue;

                string[] parts = trimmed.Split(
                    new char[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries
                );

                if (!useWrappedParsing)
                {
                    // WRAP.NO: หนึ่งบรรทัดต้องมีค่าครบหนึ่ง Record
                    if (parts.Length != expectedColumns)
                    {
                        InvalidRowCount++;
                        continue;
                    }

                    CurveData row = ParseDataRow(
                        new List<string>(parts), well);

                    if (row != null)
                        allData.Add(row);
                    else
                        InvalidRowCount++;
                }
                else
                {
                    // WRAP.YES หรือไม่ระบุ: สะสมค่าข้ามบรรทัด
                    foreach (string part in parts)
                    {
                        rowParts.Add(part);

                        if (rowParts.Count == expectedColumns)
                        {
                            CurveData row = ParseDataRow(rowParts, well);

                            if (row != null)
                                allData.Add(row);
                            else
                                InvalidRowCount++;

                            rowParts.Clear();
                        }
                    }
                }
            }

            if (rowParts.Count > 0)
                InvalidRowCount++;

            return allData;
        }

        //========================================================================================================================================//

        private CurveData ParseDataRow(
            List<string> parts,
            WellInfo well)
        {
            double depth;

            if (!double.TryParse(
                parts[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out depth))
            {
                return null;
            }

            CurveData row = new CurveData();
            row.Depth = depth;
            row.Values = new List<double?>();

            for (int i = 1; i < parts.Count; i++)
            {
                double value;

                if (!double.TryParse(
                    parts[i],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
                {
                    return null;
                }

                if (well.NullValue.HasValue &&
                    value == well.NullValue.Value)
                {
                    row.Values.Add(null);
                }
                else
                {
                    row.Values.Add(value);
                }
            }

            return row;
        }

        //========================================================================================================================================//

        public int InvalidRowCount { get; private set; }

        public bool IsDepthAscending(List<CurveData> data)
        {
            for (int i = 1; i < data.Count; i++)
            {
                if (data[i].Depth < data[i - 1].Depth)
                {
                    return false;
                }
            }
            return true;
        }

        //========================================================================================================================================//

        public bool IsSamplingIntervalCorrect(
            List<CurveData> data,
            double expectedStep)
        {
            if (data.Count < 2)
                return true;

            // ถ้า Header STEP เป็น 0
            // ให้ใช้ระยะห่างจริงระหว่างสองแถวแรกเป็นค่าอ้างอิง
            if (expectedStep == 0)
            {
                expectedStep = data[1].Depth - data[0].Depth;
            }

            for (int i = 1; i < data.Count; i++)
            {
                double actualStep =
                    data[i].Depth - data[i - 1].Depth;

                if (Math.Abs(actualStep - expectedStep) > 0.0001)
                {
                    return false;
                }
            }

            return true;
        }

        //========================================================================================================================================//

        public int CountDuplicateDepths(List<CurveData> data)
        {
            int duplicateCount = 0;

            for (int i = 1; i < data.Count; i++)
            {
                if (data[i].Depth == data[i - 1].Depth)
                {
                    duplicateCount++;
                }
            }

            return duplicateCount;
        }

        //========================================================================================================================================//

        public int CountNullValues(List<CurveData> data)
        {
            int nullCount = 0;

            foreach (CurveData row in data)
            {
                foreach (double? value in row.Values)
                {
                    if (!value.HasValue)
                    {
                        nullCount++;
                    }
                }
            }

            return nullCount;
        }

        //========================================================================================================================================//

        public List<int> CountNullValuesByCurve(List<CurveData> data)
        {
            List<int> counts = new List<int>();

            if (data.Count == 0)
                return counts;

            int curveCount = data[0].Values.Count;

            for (int i = 0; i < curveCount; i++)
            {
                counts.Add(0);
            }

            foreach (CurveData row in data)
            {
                for (int i = 0; i < row.Values.Count; i++)
                {
                    if (!row.Values[i].HasValue)
                    {
                        counts[i]++;
                    }
                }
            }

            return counts;
        }

        //========================================================================================================================================//

        public List<int> CountValidValuesByCurve(List<CurveData> data)
        {
            List<int> counts = new List<int>();

            if (data.Count == 0)
                return counts;

            int curveCount = data[0].Values.Count;

            for (int i = 0; i < curveCount; i++)
            {
                counts.Add(0);
            }

            foreach (CurveData row in data)
            {
                for (int i = 0; i < row.Values.Count; i++)
                {
                    if (row.Values[i].HasValue)
                    {
                        counts[i]++;
                    }
                }
            }

            return counts;
        }

        //========================================================================================================================================//

        public void GetSamplingInfo(
            List<CurveData> data,
            out double minStep,
            out double maxStep)
        {
            minStep = 0;
            maxStep = 0;

            if (data.Count < 2)
                return;

            minStep = double.MaxValue;
            maxStep = double.MinValue;

            for (int i = 1; i < data.Count; i++)
            {
                double actualStep = data[i].Depth - data[i - 1].Depth;

                if (actualStep < minStep)
                    minStep = actualStep;

                if (actualStep > maxStep)
                    maxStep = actualStep;
            }
        }

        //========================================================================================================================================//

        public bool? IsWrapped { get; private set; }

        //========================================================================================================================================//

        public bool UsedWrappedParsing { get; private set; }

        //========================================================================================================================================//

        public bool? ReadWrapMode(string filePath)
        {
            List<string> lines = ReadLines(filePath);

            bool insideVersionSection = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed == "" || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("~"))
                {
                    string section = trimmed.Substring(1).Trim();

                    string[] sectionParts = section.Split(
                        new char[] { ' ', '\t' },
                        StringSplitOptions.RemoveEmptyEntries
                    );

                    if (sectionParts.Length == 0)
                    {
                        insideVersionSection = false;
                        continue;
                    }

                    string sectionName = sectionParts[0];

                    insideVersionSection =
                        sectionName.Equals("V", StringComparison.OrdinalIgnoreCase) ||
                        sectionName.StartsWith("VERSION", StringComparison.OrdinalIgnoreCase);

                    continue;
                }

                if (!insideVersionSection)
                    continue;

                // ตัด Description หลังเครื่องหมาย :
                string[] parts = trimmed.Split(new char[] { ':' }, 2);

                string headerText = parts[0].Trim();

                int dotIndex = headerText.IndexOf('.');

                if (dotIndex < 0)
                    continue;

                string mnemonic = headerText.Substring(0, dotIndex).Trim();

                if (!mnemonic.Equals("WRAP", StringComparison.OrdinalIgnoreCase))
                    continue;

                string value = headerText.Substring(dotIndex + 1).Trim();

                if (value.Equals("YES", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (value.Equals("NO", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return null;
        }

        //========================================================================================================================================//

        private bool HasMultilineRecords(
            List<string> lines,
            int expectedColumns,
            double? headerStep)
        {
            if (expectedColumns <= 1 ||
                !headerStep.HasValue ||
                headerStep.Value == 0)
            {
                return false;
            }

            bool insideDataSection = false;
            List<string[]> dataLines = new List<string[]>();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed == "" || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("~"))
                {
                    string section = trimmed.Substring(1).Trim();

                    string[] sectionParts = section.Split(
                        new char[] { ' ', '\t' },
                        StringSplitOptions.RemoveEmptyEntries
                    );

                    if (sectionParts.Length == 0)
                    {
                        insideDataSection = false;
                        continue;
                    }

                    string sectionName = sectionParts[0];

                    insideDataSection =
                        sectionName.Equals("A", StringComparison.OrdinalIgnoreCase) ||
                        sectionName.StartsWith("ASCII", StringComparison.OrdinalIgnoreCase);

                    continue;
                }

                if (!insideDataSection)
                    continue;

                string[] parts = trimmed.Split(
                    new char[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries
                );

                dataLines.Add(parts);

                if (dataLines.Count >= 50)
                    break;
            }

            // ต้องมีข้อมูลอย่างน้อย 2 Records ให้เปรียบเทียบ
            if (dataLines.Count < 2)
                return false;

            int position = 0;
            List<double> depths = new List<double>();

            while (position < dataLines.Count)
            {
                // Depth ต้องอยู่บรรทัดเดียว
                if (dataLines[position].Length != 1)
                    break;

                double depth;

                if (!double.TryParse(
                    dataLines[position][0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out depth))
                {
                    break;
                }

                int totalColumns = 0;
                int startPosition = position;

                // สะสมค่าจนครบหนึ่ง Record
                while (position < dataLines.Count &&
                       totalColumns < expectedColumns)
                {
                    totalColumns += dataLines[position].Length;
                    position++;
                }

                if (totalColumns != expectedColumns)
                    break;

                // ต้องมีข้อมูลกระจายมากกว่าหนึ่งบรรทัด
                if (position - startPosition <= 1)
                    break;

                depths.Add(depth);

                if (depths.Count >= 3)
                    break;
            }

            if (depths.Count < 3)
                return false;

            // ตรวจ Depth ว่าตรงกับ STEP ใน Header
            for (int i = 1; i < depths.Count; i++)
            {
                double actualStep = depths[i] - depths[i - 1];

                if (Math.Abs(actualStep - headerStep.Value) > 0.0001)
                    return false;
            }

            return true;
        }
    }
}
