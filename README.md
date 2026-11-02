# LAS Well Log Processing Application

A Windows Forms application developed in C# using .NET Framework 4.8 for reading, analyzing, and visualizing LAS (Log ASCII Standard) well log files.

This project was developed as part of the PetroRes Consulting Software Engineering Internship coding assignment.

## Features

- **LAS File Parsing:** Reads LAS files and extracts well log curves, depth samples, and relevant metadata.
- **Batch Processing:** Processes multiple LAS files in a single operation.
- **Data Conditioning:** Checks depth ordering, duplicate depths, missing values, and sampling intervals.
- **Curve Mapping:** Identifies common Gamma Ray, Resistivity, Density, and Neutron curves using supported mnemonic names.
- **Well Log Visualization:** Displays three plot types:
  - Gamma Ray vs. Depth
  - Resistivity vs. Depth (logarithmic scale)
  - Density–Neutron overlay vs. Depth
- **PNG Export:** Exports plots as PNG images.
- **Conditioning Report:** Generates a text report containing file-level processing results, curve availability, unit checks, and missing-value statistics.

## Technologies

- C#
- Windows Forms
- .NET Framework 4.8
- ScottPlot 4.1.71
- Visual Studio

## Requirements

- Windows
- .NET Framework 4.8
- Visual Studio with .NET desktop development support (for building from source)

## How to Run

1. Clone or download the repository.
2. Open the Visual Studio solution (`.sln`) file.
3. Restore NuGet packages if prompted.
4. Build and run the application.
5. Use the file-loading button to select one or multiple LAS files.
6. Review the plots and processing results.
7. Export the conditioning report and inspect the generated PNG files.

## Data Handling

The application checks and reports:

- Depth ordering and sorting requirements
- Duplicate depth samples
- Missing and NULL values
- Sampling interval consistency
- Curve availability and mnemonic mapping
- Selected curve unit information

Original depth samples are preserved unless sorting is required. Duplicate depths are retained rather than automatically deleted.

Resampling and unit conversion are not performed. Original curve values and units are preserved.

## Testing

A batch test was performed using 50 LAS files.

|        Result        |Value|
|----------------------|-----|
| Total files          |  50 |
| Successfully parsed  |  49 |
| Failed               |   1 |
| Parsing success rate | 98% |

The unsupported file contained multiple Curve or ASCII sections. The application reported the failure and continued processing the remaining files.

## Known Limitations

- LAS files containing multiple Curve or ASCII sections, including certain LAS 3.0 structures, are not currently supported.
- Unit conversion and resampling are not implemented.
- Curve recognition depends on the supported mnemonic mappings.
- Some LAS files do not contain all four required curve types. Missing curves are identified in the plot or report.
- Density–Neutron plots may have scaling or display issues with certain datasets, particularly when values or units vary significantly.

### AI Usage

ChatGPT was used during the development of this assignment to support learning C# and Windows Forms, generating and refining code, troubleshooting errors, and improving LAS parsing, data conditioning, and plotting functionality.

I worked iteratively with AI-generated suggestions, integrated the code into the application, tested it using the provided LAS files, investigated errors, and evaluated the results.

## Notes

The application is intended as a coding-assignment implementation demonstrating LAS parsing, data validation, batch processing, reporting, and visualization. It is not intended as a fully featured commercial well log interpretation system.