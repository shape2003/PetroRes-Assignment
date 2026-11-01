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

                string remaining = curveText.Substring(dotIndex + 1).Trim();

                string[] columns = remaining.Split(
                    new char[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries
                );

                if (columns.Length > 0)
                    curve.Unit = columns[0];

                if (columns.Length > 1)
                    curve.ApiCode = columns[1];

                if (parts.Length > 1)
                    curve.Description = parts[1].Trim();

                curves.Add(curve);
            }

            return curves;
        }
        public List<CurveData> ReadData(string filePath)
        {
            List<string> lines = ReadLines(filePath);
            List<CurveData> allData = new List<CurveData>();

            WellInfo well = ReadWellInfo(filePath);
            int expectedColumns = ReadCurves(filePath).Count;

            InvalidRowCount = 0;

            if (expectedColumns == 0)
                return allData;

            bool insideDataSection = false;

            // เก็บค่าจากหลายบรรทัดจนกว่าจะครบหนึ่งแถว
            List<string> rowParts = new List<string>();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed == "" || trimmed.StartsWith("#"))
                    continue;

                // ตรวจสอบการเปลี่ยน Section
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

                // แยกค่าด้วยช่องว่างหรือ Tab
                string[] parts = trimmed.Split(
                    new char[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries
                );

                foreach (string part in parts)
                {
                    rowParts.Add(part);

                    // เมื่อสะสมข้อมูลครบจำนวนคอลัมน์แล้ว
                    if (rowParts.Count == expectedColumns)
                    {
                        CurveData row = new CurveData();

                        double depth;

                        bool validRow = double.TryParse(
                            rowParts[0],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out depth
                        );

                        row.Values = new List<double?>();

                        if (validRow)
                        {
                            row.Depth = depth;

                            for (int i = 1; i < rowParts.Count; i++)
                            {
                                double value;

                                if (!double.TryParse(
                                    rowParts[i],
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out value))
                                {
                                    validRow = false;
                                    break;
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
                        }

                        if (validRow)
                        {
                            allData.Add(row);
                        }
                        else
                        {
                            InvalidRowCount++;
                        }

                        // เริ่มสะสมข้อมูลของแถวถัดไป
                        rowParts.Clear();
                    }
                }
            }

            // ถ้าเหลือข้อมูลไม่ครบหนึ่งแถวเมื่อจบไฟล์
            if (rowParts.Count > 0)
            {
                InvalidRowCount++;
            }

            return allData;
        }

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
    }
}
