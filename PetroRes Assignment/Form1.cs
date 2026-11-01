using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Threading;

namespace PetroRes_Assignment
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

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
                    StringComparison.OrdinalIgnoreCase
                )
            );

            // curves[0] is Depth, stored separately in row.Depth
            if (curveIndex <= 0)
                return;

            int valueIndex = curveIndex - 1;

            List<double> gammaValues = new List<double>();
            List<double> depthValues = new List<double>();

            foreach (CurveData row in data)
            {
                if (row.Values == null ||
                    valueIndex >= row.Values.Count)
                    continue;

                double? value = row.Values[valueIndex];

                if (!value.HasValue ||
                    double.IsNaN(value.Value) ||
                    double.IsInfinity(value.Value) ||
                    double.IsNaN(row.Depth) ||
                    double.IsInfinity(row.Depth))
                    continue;

                gammaValues.Add(value.Value);
                depthValues.Add(row.Depth);
            }

            if (gammaValues.Count == 0)
                return;

            formsPlot1.Plot.AddScatter(
                gammaValues.ToArray(),
                depthValues.ToArray(),
                markerSize: 0
            );

            formsPlot1.Plot.Title("Gamma Ray - " + gammaRayName);
            formsPlot1.Plot.XLabel("Gamma Ray (API)");
            formsPlot1.Plot.YLabel("Depth");

            formsPlot1.Plot.AxisAuto();
            formsPlot1.Plot.YAxis.IsReverse = true;

            formsPlot1.Refresh();
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

                progressBar1.Minimum = 0;
                progressBar1.Maximum = dialog.FileNames.Length;
                progressBar1.Value = 0;

                lblProgress.Text = "Starting...";

                StringBuilder report = new StringBuilder();

                List<CurveData> plotData = null;
                List<CurveInfo> plotCurves = null;
                string plotGammaRay = null;

                IProgress<int> progress = new Progress<int>(value =>
                {
                    progressBar1.Value = value;

                    lblProgress.Text =
                        "Reading " + value + " / " +
                        dialog.FileNames.Length + " files...";
                });

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
                            int duplicateDepths = reader.CountDuplicateDepths(data);

                            bool samplingCorrect =
                                reader.IsSamplingIntervalCorrect(data, well.Step ??  0);

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
                                "Depth ascending: " + reader.IsDepthAscending(data)
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
                        }
                        catch (Exception ex)
                        {
                            report.AppendLine("Could not read this file.");
                            report.AppendLine("Reason: " + ex.Message);
                        }

                        completedFiles++;

                        progress.Report(completedFiles);

                        report.AppendLine();
                        report.AppendLine();
                    }
                });

                if (plotData != null)
                {
                    PlotGammaRay(
                        plotData,
                        plotCurves,
                        plotGammaRay
                    );
                }

                btnLoadFiles.Enabled = true;
                lblProgress.Text = "Finished reading all files.";

                SaveFileDialog saveDialog = new SaveFileDialog();
                
                saveDialog.Filter = "Text files (*.txt)|*.txt";
                
                saveDialog.FileName = "ConditioningReport.txt";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(
                            saveDialog.FileName,
                            report.ToString(),
                            Encoding.UTF8
                        );

                        MessageBox.Show("Report saved successfully.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not save the report:\n" + ex.Message);
                    }
                }
            }
        }

        //========================================================================================================================================//

        private void lblProgress_Click(object sender, EventArgs e)
        {

        }
    }
}
