# C# SPSS SAV file reader and writer library (updated for .NET 8)

This repository contains a library that reads and writes SPSS data files (.sav). A set of .NET 8-compatible projects are provided in the `.net/` folder and are recommended for new development:

- `.net/SpssLibNet8` — .NET 8 class library (produces `SpssLib.dll` / NuGet package)
- `.net/SpssFileReadNet8` — .NET 8 console app
- `.net/SpssViewerNet8` — .NET 8 WPF viewer (net8.0-windows)

Viewer utility

A new viewer utility is provided at `.net/SpssViewerNet8`. It is a WPF application for inspecting `.sav` files and includes export features (CSV / .xlsx). Run it from the repository root with:

```bash
dotnet run --project .net/SpssViewerNet8 --configuration Release
```

The repository also includes original legacy .NET Framework projects. The `.net/` projects are the supported .NET 8 builds.

Install (from NuGet)

Install the library with the .NET CLI (replace package id/version as appropriate):

```bash
dotnet add package SpssLib
```

Examples (usage)

The example code below is preserved from the original documentation and is intended to work with the .NET 8 build (`SpssLibNet8`).

To read a data file:

```csharp
// Open file (sequential scan for performance)
using (FileStream fileStream = new FileStream("data.sav", FileMode.Open, FileAccess.Read, FileShare.Read, 2048*10, FileOptions.SequentialScan))
{
    // Create the reader, this will read the file header
    SpssReader spssDataset = new SpssReader(fileStream);

    // Iterate through all the variables
    foreach (var variable in spssDataset.Variables)
    {
        Console.WriteLine("{0} - {1}", variable.Name, variable.Label);
        foreach (KeyValuePair<double, string> label in variable.ValueLabels)
        {
            Console.WriteLine(" {0} - {1}", label.Key, label.Value);
        }
    }

    // Iterate through all data rows
    foreach (var record in spssDataset.Records)
    {
        foreach (var variable in spssDataset.Variables)
        {
            Console.Write(variable.Name);
            Console.Write(':');
            Console.Write(record.GetValue(variable));
            Console.Write('\t');
        }
        Console.WriteLine("");
    }
}
```

To write a data file:

```csharp
var variables = new List<Variable>
{
    new Variable
    {
        Label = "The variable Label",
        ValueLabels = new Dictionary<double, string>
        {
            {1, "Label for 1"},
            {2, "Label for 2"},
        },
        Name = "avariablename_01",
        PrintFormat = new OutputFormat(FormatType.F, 8, 2),
        WriteFormat = new OutputFormat(FormatType.F, 8, 2),
        Type = DataType.Numeric,
        Width = 10,
        MissingValueType = MissingValueType.NoMissingValues
    },
    new Variable
    {
        Label = "Another variable",
        ValueLabels = new Dictionary<double, string>
        {
            {1, "this is 1"},
            {2, "this is 2"},
        },
        Name = "avariablename_02",
        PrintFormat = new OutputFormat(FormatType.F, 8, 2),
        WriteFormat = new OutputFormat(FormatType.F, 8, 2),
        Type = DataType.Numeric,
        Width = 10,
        MissingValueType = MissingValueType.OneDiscreteMissingValue
    }
};
// Set a special missing value
variables[1].MissingValues[0] = 999;

var options = new SpssOptions();

using (FileStream fileStream = new FileStream("data.sav", FileMode.Create, FileAccess.Write))
{
    using (var writer = new SpssWriter(fileStream, variables, options))
    {
        var newRecord = writer.CreateRecord();
        newRecord[0] = 15d;
        newRecord[1] = 15.5d;
        writer.WriteRecord(newRecord);

        newRecord = writer.CreateRecord();
        newRecord[0] = null;
        newRecord[1] = 200d;
        writer.WriteRecord(newRecord);
        writer.EndFile();
    }
}
```


Notes
This code is a fork of fbiagi/SPSS-.NET-Reader, which was a fork of spsslib-80132.   All functionality is preserved in the port to .net8, The viewer is an example of library usage for parsing a sav file.
At some point the .net framework version of the code will be removed.
