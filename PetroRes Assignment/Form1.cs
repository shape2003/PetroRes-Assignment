using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PetroRes_Assignment
{
    public partial class Form1 : Form
    {
        private string latestConditioningReport = null;
        public Form1()
        {
            InitializeComponent();

            btnSaveReport.Enabled = false;
        }

        //========================================================================================================================================//

        private void PlotGammaRay(
            List<CurveData> data,
            List<CurveInfo> curves,
            string gammaRayName)
        {
            formsPlot1.Plot.Clear();

            if (string.IsNullOrEmpty(gammaRayName))
                return;

            int curveIndex = curves.FindIndex(c =>
                string.Equals(
                    c.Mnemonic,
                    gammaRayName,
                    StringComparison.OrdinalIgnoreCase));

            if (curveIndex <= 0)
                return;

            int valueIndex = curveIndex - 1;

            List<double> gammaValues = new List<double>();
            List<double> depthValues = new List<double>();

            // วาดเส้นเฉพาะช่วงที่ข้อมูลต่อเนื่องกัน
            Action drawSegment = () =>
            {
                if (gammaValues.Count >= 2)
                {
                    formsPlot1.Plot.AddScatter(
                        gammaValues.ToArray(),
                        depthValues.ToArray(),
                        color: Color.FromArgb(35, 116, 217),
                        lineWidth: 1.5f,
                        markerSize: 0);
                }

                gammaValues.Clear();
                depthValues.Clear();
            };

            foreach (CurveData row in data)
            {
                double? value = null;

                if (row.Values != null &&
                    valueIndex < row.Values.Count)
                {
                    value = row.Values[valueIndex];
                }

                bool valid =
                    value.HasValue &&
                    !double.IsNaN(value.Value) &&
                    !double.IsInfinity(value.Value) &&
                    !double.IsNaN(row.Depth) &&
                    !double.IsInfinity(row.Depth);

                if (!valid)
                {
                    drawSegment();
                    continue;
                }

                gammaValues.Add(value.Value);
                depthValues.Add(row.Depth);
            }

            // วาดช่วงสุดท้ายที่ยังเหลืออยู่
            drawSegment();

            formsPlot1.Plot.Title("Gamma Ray - " + gammaRayName);
            formsPlot1.Plot.XLabel("Gamma Ray (API)");
            formsPlot1.Plot.YLabel("Depth");

            formsPlot1.Plot.AxisAuto();
            formsPlot1.Plot.YAxis.IsReverse = true;

            formsPlot1.Refresh();
        }

        //========================================================================================================================================//

        private void PlotResistivity(
            List<CurveData> data,
            List<CurveInfo> curves,
            string resistivityName)
        {
            formsPlot2.Plot.Clear();

            if (string.IsNullOrEmpty(resistivityName))
                return;

            int curveIndex = curves.FindIndex(c =>
                string.Equals(
                    c.Mnemonic,
                    resistivityName,
                    StringComparison.OrdinalIgnoreCase));

            if (curveIndex <= 0)
                return;

            int valueIndex = curveIndex - 1;

            List<double> logValues = new List<double>();
            List<double> depthValues = new List<double>();

            Action drawSegment = () =>
            {
                if (logValues.Count >= 2)
                {
                    formsPlot2.Plot.AddScatter(
                        logValues.ToArray(),
                        depthValues.ToArray(),
                        color: Color.FromArgb(217, 119, 6),
                        lineWidth: 1.5f,
                        markerSize: 0);
                }

                logValues.Clear();
                depthValues.Clear();
            };

            foreach (CurveData row in data)
            {
                double? value = null;

                if (row.Values != null &&
                    valueIndex < row.Values.Count)
                {
                    value = row.Values[valueIndex];
                }

                bool valid =
                    value.HasValue &&
                    value.Value > 0 &&
                    !double.IsNaN(value.Value) &&
                    !double.IsInfinity(value.Value) &&
                    !double.IsNaN(row.Depth) &&
                    !double.IsInfinity(row.Depth);

                if (!valid)
                {
                    drawSegment();
                    continue;
                }

                logValues.Add(Math.Log10(value.Value));
                depthValues.Add(row.Depth);
            }

            drawSegment();

            formsPlot2.Plot.Title("Resistivity - " + resistivityName);
            formsPlot2.Plot.XLabel("Resistivity (log scale, unit unverified)");
            formsPlot2.Plot.YLabel("Depth");

            double[] tickPositions = { -2, -1, 0, 1, 2, 3, 4 };
            string[] tickLabels =
                { "0.01", "0.1", "1", "10", "100", "1000", "10000" };

            formsPlot2.Plot.XAxis.ManualTickPositions(
                tickPositions,
                tickLabels);

            formsPlot2.Plot.AxisAuto();
            formsPlot2.Plot.YAxis.IsReverse = true;

            formsPlot2.Refresh();
        }

        //========================================================================================================================================//

        private void PlotDensityNeutron(
            List<CurveData> data,
            List<CurveInfo> curves,
            string densityName,
            string neutronName)
        {
            formsPlot3.Plot.Clear();

            // Bottom X axis: Density
            formsPlot3.Plot.XAxis.Label("Density (raw units)");

            // Top X axis: Neutron
            formsPlot3.Plot.XAxis2.IsVisible = true;
            formsPlot3.Plot.XAxis2.Ticks(true);
            formsPlot3.Plot.XAxis2.Label("Neutron (raw units)");

            // Temporarily omit Title to avoid overlapping top axis
            formsPlot3.Plot.YLabel("Depth");

            PlotOverlayCurve(
                data, curves, densityName, 0,
                Color.FromArgb(22, 128, 60));

            PlotOverlayCurve(
                data, curves, neutronName, 1,
                Color.FromArgb(168, 85, 247));

            formsPlot3.Plot.Legend(location: ScottPlot.Alignment.UpperRight);

            formsPlot3.Plot.AxisAuto();

            double neutronMin;
            double neutronMax;

            if (TryGetCurveRange(
                data,
                curves,
                neutronName,
                out neutronMin,
                out neutronMax))
            {
                // Prevent zero-width axis
                if (neutronMin == neutronMax)
                {
                    double padding = Math.Max(
                        Math.Abs(neutronMin) * 0.05,
                        0.1);

                    neutronMin -= padding;
                    neutronMax += padding;
                }
                else
                {
                    // Add 5% space on each side
                    double padding = (neutronMax - neutronMin) * 0.05;

                    neutronMin -= padding;
                    neutronMax += padding;
                }

                formsPlot3.Plot.SetAxisLimits(
                    xMin: neutronMin,
                    xMax: neutronMax,
                    xAxisIndex: 1);
            }

            formsPlot3.Plot.YAxis.IsReverse = true;

            formsPlot3.Refresh();
        }

        //========================================================================================================================================//

        private void PlotOverlayCurve(
            List<CurveData> data,
            List<CurveInfo> curves,
            string curveName,
            int xAxisIndex,
            Color curveColor)
        {
            if (string.IsNullOrEmpty(curveName))
                return;

            int curveIndex = curves.FindIndex(c =>
                string.Equals(
                    c.Mnemonic,
                    curveName,
                    StringComparison.OrdinalIgnoreCase));

            if (curveIndex <= 0)
                return;

            int valueIndex = curveIndex - 1;

            List<double> xValues = new List<double>();
            List<double> depthValues = new List<double>();

            bool legendAdded = false;

            Action drawSegment = () =>
            {
                if (xValues.Count >= 2)
                {
                    var scatter = formsPlot3.Plot.AddScatter(
                        xValues.ToArray(),
                        depthValues.ToArray(),
                        color: curveColor,
                        lineWidth: 1.5f,
                        markerSize: 0);

                    scatter.XAxisIndex = xAxisIndex;

                    if (!legendAdded)
                    {
                        scatter.Label = curveName;
                        legendAdded = true;
                    }
                }

                xValues.Clear();
                depthValues.Clear();
            };

            foreach (CurveData row in data)
            {
                double? value = null;

                if (row.Values != null &&
                    valueIndex < row.Values.Count)
                {
                    value = row.Values[valueIndex];
                }

                bool valid =
                    value.HasValue &&
                    !double.IsNaN(value.Value) &&
                    !double.IsInfinity(value.Value) &&
                    !double.IsNaN(row.Depth) &&
                    !double.IsInfinity(row.Depth);

                if (!valid)
                {
                    drawSegment();
                    continue;
                }

                xValues.Add(value.Value);
                depthValues.Add(row.Depth);
            }

            drawSegment();
        }

        //========================================================================================================================================//

        private bool TryGetCurveRange(
            List<CurveData> data,
            List<CurveInfo> curves,
            string curveName,
            out double minValue,
            out double maxValue)
        {
            minValue = 0;
            maxValue = 0;

            if (data == null || curves == null ||
                string.IsNullOrEmpty(curveName))
                return false;

            int curveIndex = curves.FindIndex(c =>
                string.Equals(
                    c.Mnemonic,
                    curveName,
                    StringComparison.OrdinalIgnoreCase));

            // Index 0 is Depth, not a value in row.Values
            if (curveIndex <= 0)
                return false;

            int valueIndex = curveIndex - 1;
            bool found = false;

            foreach (CurveData row in data)
            {
                if (row.Values == null ||
                    valueIndex >= row.Values.Count)
                    continue;

                double? value = row.Values[valueIndex];

                if (!value.HasValue ||
                    double.IsNaN(value.Value) ||
                    double.IsInfinity(value.Value))
                    continue;

                double number = value.Value;

                if (!found)
                {
                    minValue = number;
                    maxValue = number;
                    found = true;
                }
                else
                {
                    minValue = Math.Min(minValue, number);
                    maxValue = Math.Max(maxValue, number);
                }
            }

            return found;
        }

        //========================================================================================================================================//

        private void SetSharedDepthRange(List<CurveData> data)
        {
            if (data == null || data.Count == 0)
                return;

            List<double> depths = new List<double>();

            foreach (CurveData row in data)
            {
                if (!double.IsNaN(row.Depth) &&
                    !double.IsInfinity(row.Depth))
                {
                    depths.Add(row.Depth);
                }
            }

            if (depths.Count == 0)
                return;

            double minDepth = depths.Min();
            double maxDepth = depths.Max();

            // Prevent invalid axis limits
            if (minDepth == maxDepth)
            {
                minDepth -= 0.5;
                maxDepth += 0.5;
            }

            // Both plots use the same depth range
            formsPlot1.Plot.SetAxisLimitsY(minDepth, maxDepth);
            formsPlot2.Plot.SetAxisLimitsY(minDepth, maxDepth);
            formsPlot3.Plot.SetAxisLimitsY(minDepth, maxDepth);

            // Depth increases downward
            formsPlot1.Plot.YAxis.IsReverse = true;
            formsPlot2.Plot.YAxis.IsReverse = true;
            formsPlot3.Plot.YAxis.IsReverse = true;

            formsPlot1.Refresh();
            formsPlot2.Refresh();
            formsPlot3.Refresh();
        }

        //========================================================================================================================================//


        private void SaveCurrentPlots(string outputFolder, string wellFileName)
        {
            // Create a separate folder for each well.
            string baseName = Path.GetFileNameWithoutExtension(wellFileName);

            string wellFolder = Path.Combine(outputFolder, baseName);

            Directory.CreateDirectory(wellFolder);

            // White backgrounds for all plots.
            formsPlot1.Plot.Style(
                figureBackground: Color.White,
                dataBackground: Color.White
            );

            formsPlot2.Plot.Style(
                figureBackground: Color.White,
                dataBackground: Color.White
            );

            formsPlot3.Plot.Style(
                figureBackground: Color.White,
                dataBackground: Color.White
            );

            // Save three tracks inside the well folder.
            formsPlot1.Plot.SaveFig(
                Path.Combine(wellFolder, "GammaRay.png"),
                600, 900);

            formsPlot2.Plot.SaveFig(
                Path.Combine(wellFolder, "Resistivity.png"),
                600, 900);

            formsPlot3.Plot.SaveFig(
                Path.Combine(wellFolder, "DensityNeutron.png"),
                600, 900);
        }



        //========================================================================================================================================//

        private async void btnLoadFiles_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            
            dialog.Multiselect = true;
            
            dialog.Filter = "LAS files (*.las)|*.las";


            if (dialog.ShowDialog() == DialogResult.OK)
            {
                btnLoadFiles.Enabled = false;
                latestConditioningReport = null;

                progressBar1.Minimum = 0;
                progressBar1.Maximum = dialog.FileNames.Length;
                progressBar1.Value = 0;

                lblProgress.Text = "Starting...";

                StringBuilder report = new StringBuilder();

                List<WellPlotData> wellsToPlot = new List<WellPlotData>();

                List<CurveData> plotData = null;
                List<CurveInfo> plotCurves = null;
                string plotGammaRay = null;
                string plotResistivity = null;
                string plotDensity = null;
                string plotNeutron = null;

                IProgress<int> progress = new Progress<int>(value =>
                {
                    progressBar1.Value = value;

                    lblProgress.Text =
                        "Loading " + value + " / " +
                        dialog.FileNames.Length + " files...";
                });

                Stopwatch stopwatch = Stopwatch.StartNew();

                int successFiles = 0;
                int failedFiles = 0;

                List<string> failedFileDetails = new List<string>();

                await Task.Run(() =>
                {
                    int completedFiles = 0;

                    foreach (string filePath in dialog.FileNames)
                    {
                        string fileName = Path.GetFileName(filePath);

                        report.AppendLine("FILE: " + fileName);
                        report.AppendLine("------------------------------");

                        try
                        {
                            LasReader reader = new LasReader();

                            List<CurveData> data = reader.ReadData(filePath);
                            WellInfo well = reader.ReadWellInfo(filePath);
                            List<CurveInfo> curves = reader.ReadCurves(filePath);
                            CurveAliasMapper mapper = new CurveAliasMapper();

                            if (data == null || data.Count == 0)
                            {
                                throw new InvalidDataException(
                                    "No valid data rows were parsed.");
                            }

                            if (curves == null || curves.Count < 2)
                            {
                                throw new InvalidDataException(
                                    "No measurement curves were found.");
                            }

                            int expectedValueCount = curves.Count - 1;

                            foreach (CurveData row in data)
                            {
                                if (row == null ||
                                    row.Values == null ||
                                    row.Values.Count != expectedValueCount)
                                {
                                    throw new InvalidDataException(
                                        "Parsed data does not match the curve definitions.");
                                }
                            }

                            // DATA CONDITIONING

                            // Count duplicate depths across the entire dataset.
                            int duplicateDepthCount = data.Count -
                                data.Select(row => row.Depth).Distinct().Count();

                            // Check original depth order before changing it.
                            bool wasDepthAscending = reader.IsDepthAscending(data);

                            // Sort only when depth is not ascending.
                            if (!wasDepthAscending)
                            {
                                data = data.OrderBy(row => row.Depth).ToList();
                            }

                            // Record what was actually changed.
                            report.AppendLine("DATA CONDITIONING:");
                            report.AppendLine(
                                "Depth sorting: " +
                                (wasDepthAscending
                                    ? "Not needed (already ascending)"
                                    : "Applied (sorted ascending)"));

                            report.AppendLine(
                                "Duplicate depths detected: " + duplicateDepthCount);

                            report.AppendLine(
                                "Duplicate depth handling: Preserved (no automatic deletion)");

                            report.AppendLine(
                                "Missing value handling: LAS NULL values represented as null");

                            report.AppendLine(
                                "Resampling: Not applied (original depth samples preserved)");

                            report.AppendLine(
                                "Unit conversion: Not applied (original values preserved)");

                            report.AppendLine();


                            string gammaRay = mapper.FindCurveName(curves, "GammaRay");
                            string resistivity = mapper.FindCurveName(curves, "Resistivity");
                            string density = mapper.FindCurveName(curves, "Density");
                            string neutron = mapper.FindCurveName(curves, "Neutron");

                            if (plotData == null &&
                                gammaRay != null &&
                                data.Count > 0)
                            {
                                plotData = data;
                                plotCurves = curves;
                                plotGammaRay = gammaRay;
                                plotResistivity = resistivity;
                                plotDensity = density;
                                plotNeutron = neutron;
                            }

                            // Find the position of the Neutron curve
                            int neutronIndex = curves.FindIndex(c =>
                                string.Equals(
                                    c.Mnemonic,
                                    neutron,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            );

                            double? neutronMin = null;
                            double? neutronMax = null;

                            // Index 0 is Depth, so Neutron must be after it
                            if (neutron != null && neutronIndex > 0)
                            {
                                int valueIndex = neutronIndex - 1;

                                foreach (CurveData row in data)
                                {
                                    if (row.Values == null || valueIndex >= row.Values.Count)
                                        continue;

                                    double? value = row.Values[valueIndex];

                                    if (!value.HasValue ||
                                        double.IsNaN(value.Value) ||
                                        double.IsInfinity(value.Value))
                                        continue;

                                    if (!neutronMin.HasValue || value.Value < neutronMin.Value)
                                        neutronMin = value.Value;

                                    if (!neutronMax.HasValue || value.Value > neutronMax.Value)
                                        neutronMax = value.Value;
                                }
                            }

                            foreach (CurveInfo curve in curves)
                            {
                                if (curve.Mnemonic == gammaRay)
                                    report.AppendLine("Gamma Ray unit: " +
                                        mapper.CheckUnit(curve, "GammaRay"));

                                if (curve.Mnemonic == resistivity)
                                    report.AppendLine("Resistivity unit: " +
                                        mapper.CheckUnit(curve, "Resistivity"));

                                if (curve.Mnemonic == density)
                                    report.AppendLine("Density unit: " +
                                        mapper.CheckUnit(curve, "Density"));

                                if (curve.Mnemonic == neutron)
                                    report.AppendLine("Neutron unit: " +
                                        mapper.CheckUnit(curve, "Neutron"));
                            }

                            List<int> nullCounts =
                                reader.CountNullValuesByCurve(data);

                            List<int> validCounts =
                                reader.CountValidValuesByCurve(data);

                            int nullCount = reader.CountNullValues(data);
                            int duplicateDepths = duplicateDepthCount;

                            bool samplingCorrect =
                                reader.IsSamplingIntervalCorrect(data, well.Step ?? 0);

                            reader.GetSamplingInfo(
                                data, out double minStep, out double maxStep);

                            report.AppendLine(
                                "WRAP mode: " +
                                (reader.IsWrapped.HasValue
                                    ? (reader.IsWrapped.Value ? "YES" : "NO")
                                    : "Unknown")
                            );

                            report.AppendLine(
                                "Actual parsing mode: " +
                                (reader.UsedWrappedParsing ? "Wrapped" : "Non-wrapped")
                            );

                            report.AppendLine("Header STEP: " + well.Step);
                            report.AppendLine("Minimum actual step: " + minStep);
                            report.AppendLine("Maximum actual step: " + maxStep);
                            report.AppendLine("Rows: " + data.Count);
                            report.AppendLine("Invalid rows: " + reader.InvalidRowCount);

                            report.AppendLine(
                                "Values per row: " +
                                (data.Count > 0
                                    ? data[0].Values.Count.ToString()
                                    : "No data")
                            );

                            report.AppendLine(
                                "Original depth ascending: " + wasDepthAscending
                            );

                            report.AppendLine(
                                "Conditioned depth ascending: " + reader.IsDepthAscending(data)
                            );

                            report.AppendLine(
                                "Sampling interval correct: " + samplingCorrect
                            );

                            report.AppendLine("Duplicate depths: " + duplicateDepths);
                            report.AppendLine("Total NULL values: " + nullCount);

                            report.AppendLine();
                            report.AppendLine("NULL values by curve:");

                            report.AppendLine("Curve mapping:");
                            report.AppendLine("Gamma Ray: " + (gammaRay ?? "Not found"));
                            report.AppendLine("Resistivity: " + (resistivity ?? "Not found"));
                            report.AppendLine("Density: " + (density ?? "Not found"));
                            report.AppendLine("Neutron: " + (neutron ?? "Not found"));
                            report.AppendLine();
                            report.AppendLine("Neutron value range:");

                            report.AppendLine(
                                "Minimum: " +
                                (neutronMin.HasValue
                                    ? neutronMin.Value.ToString("G")
                                    : "N/A")
                            );

                            report.AppendLine(
                                "Maximum: " +
                                (neutronMax.HasValue
                                    ? neutronMax.Value.ToString("G")
                                    : "N/A")
                            );

                            report.AppendLine();

                            if (data.Count > 0)
                            {
                                for (int i = 1; i < curves.Count; i++)
                                {
                                    int valueIndex = i - 1;

                                    if (valueIndex < nullCounts.Count &&
                                        valueIndex < validCounts.Count)
                                    {
                                        report.AppendLine(
                                            curves[i].Mnemonic + ": " +
                                            validCounts[valueIndex] + " valid values, " +
                                            nullCounts[valueIndex] + " NULL values"
                                        );
                                    }
                                }
                            }
                            else
                            {
                                report.AppendLine("No valid data rows found.");
                            }

                            wellsToPlot.Add(new WellPlotData
                            {
                                FileName = fileName,
                                Data = data,
                                Curves = curves,
                                GammaRay = gammaRay,
                                Resistivity = resistivity,
                                Density = density,
                                Neutron = neutron
                            });


                            successFiles++;
                            report.AppendLine("Read status: SUCCESS");
                        }
                        catch (Exception ex)
                        {
                            failedFiles++;

                            string errorDetail = fileName + ": " + ex.Message;
                            failedFileDetails.Add(errorDetail);

                            report.AppendLine("Read status: FAILED");
                            report.AppendLine("Could not read this file.");
                            report.AppendLine("Reason: " + ex.Message);
                        }

                        completedFiles++;

                        progress.Report(completedFiles);

                        report.AppendLine();
                        report.AppendLine();
                    }
                });

                stopwatch.Stop();

                string summary =
                    $"Total files: {dialog.FileNames.Length}\n" +
                    $"Successfully parsed: {successFiles}\n" +
                    $"Failed: {failedFiles}\n" +
                    $"Elapsed time: {stopwatch.Elapsed.TotalSeconds:F2} seconds";

                if (failedFileDetails.Count > 0)
                {
                    summary += "\n\nFailed files:\n" +
                        string.Join("\n", failedFileDetails);
                }

                MessageBox.Show(
                    summary,
                    "LAS Reader Test");

                report.AppendLine("================================");
                report.AppendLine("BATCH TEST SUMMARY");
                report.AppendLine("================================");
                report.AppendLine(summary);

                if (plotData != null)
                {
                    PlotGammaRay(
                        plotData,
                        plotCurves,
                        plotGammaRay
                    );

                    PlotResistivity(
                        plotData,
                        plotCurves,
                        plotResistivity
                    );

                    PlotDensityNeutron(
                        plotData,
                        plotCurves,
                        plotDensity,
                        plotNeutron
                    );

                    SetSharedDepthRange(plotData);

                }

                string outputFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "WellPlots"
                );

                int exportedWells = 0;
                int failedExports = 0;

                foreach (WellPlotData wellPlot in wellsToPlot)
                {
                    try
                    {
                        PlotGammaRay(
                            wellPlot.Data,
                            wellPlot.Curves,
                            wellPlot.GammaRay);

                        PlotResistivity(
                            wellPlot.Data,
                            wellPlot.Curves,
                            wellPlot.Resistivity);

                        PlotDensityNeutron(
                            wellPlot.Data,
                            wellPlot.Curves,
                            wellPlot.Density,
                            wellPlot.Neutron);

                        SetSharedDepthRange(wellPlot.Data);

                        SetSharedDepthRange(wellPlot.Data);

                        // Save all three plots for this well.
                        SaveCurrentPlots(
                            outputFolder,
                            wellPlot.FileName
                        );

                        exportedWells++;
                    }
                    catch (Exception ex)
                    {
                        failedExports++;

                        report.AppendLine(
                            "Plot export failed: " +
                            wellPlot.FileName + " - " + ex.Message);

                        string errorMessage =
                            "Plot export failed: " +
                            wellPlot.FileName + " - " + ex.ToString();

                        report.AppendLine(errorMessage);

                        Debug.WriteLine(errorMessage);
                    }
                }

                int pngCount = Directory.Exists(outputFolder)
                    ? Directory.GetFiles(
                        outputFolder,
                        "*.png",
                        SearchOption.AllDirectories
                        ).Length
                    : 0;

                MessageBox.Show(
                    "Exported wells: " + exportedWells +
                    "\nFailed exports: " + failedExports +
                    "\nPNG files found: " + pngCount +
                    "\nFolder: " + outputFolder,
                    "Batch Plot Export");

                if (Directory.Exists(outputFolder))
                {
                    Process.Start("explorer.exe", outputFolder);
                }

                // Store the completed report for manual saving.
                latestConditioningReport = report.ToString();

                // Enable the Save Report button.
                btnSaveReport.Enabled = true;

                // Reset progress bar after all work is finished.
                progressBar1.Value = progressBar1.Minimum;

                // Enable loading another batch.
                btnLoadFiles.Enabled = true;

                lblProgress.Text = "Complete";
            }
        }

        //========================================================================================================================================//

        private void lblProgress_Click(object sender, EventArgs e)
        {

        }

        //========================================================================================================================================//

        private class WellPlotData
        {
            public string FileName { get; set; }
            public List<CurveData> Data { get; set; }
            public List<CurveInfo> Curves { get; set; }

            public string GammaRay { get; set; }
            public string Resistivity { get; set; }
            public string Density { get; set; }
            public string Neutron { get; set; }
        }


        private void btnSaveReport_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(latestConditioningReport))
            {
                MessageBox.Show("No conditioning report available.");
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Text files (*.txt)|*.txt";
                saveDialog.FileName = "ConditioningReport.txt";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(
                            saveDialog.FileName,
                            latestConditioningReport,
                            Encoding.UTF8
                        );

                        MessageBox.Show(
                            "Report saved successfully.",
                            "Save Report"
                        );
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Could not save the report:\n" + ex.Message,
                            "Save Error"
                        );
                    }
                }
            }
        }

    }
}
