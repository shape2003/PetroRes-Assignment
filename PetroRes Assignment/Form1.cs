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

                            List<int> nullCounts =
                                reader.CountNullValuesByCurve(data);

                            List<int> validCounts =
                                reader.CountValidValuesByCurve(data);

                            int nullCount = reader.CountNullValues(data);
                            int duplicateDepths = reader.CountDuplicateDepths(data);

                            bool samplingCorrect =
                                reader.IsSamplingIntervalCorrect(data, well.Step);

                            reader.GetSamplingInfo(
                                data, out double minStep, out double maxStep);

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

        private void lblProgress_Click(object sender, EventArgs e)
        {

        }
    }
}
