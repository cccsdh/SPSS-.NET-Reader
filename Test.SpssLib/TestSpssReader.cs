using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SpssLib.DataReader;
using SpssLib.FileParser;
using SpssLib.SpssDataset;

namespace Test.SpssLib
{
    [TestClass]
    public class TestSpssReader
    {
        [TestMethod]
        public void TestReadDFCUFile()
        {
            // Open file, can be read only and sequetial (for performance), or anything else
            using (FileStream fileStream = new FileStream(@"C:\fakepath\LinnAreaCreditUnion-November2021_11_1_2024_Completed.sav", FileMode.Open, FileAccess.Read, FileShare.Read, 2048 * 10,
                                                          FileOptions.SequentialScan))
            {
                // Create the reader, this will read the file header
                SpssReader spssDataset = new SpssReader(fileStream);
                var definitions = new List<ColumnDefinition>();

                var sortedVariables = spssDataset.Variables.OrderBy(o=>o.Index).ToList();
                // Iterate through all the varaibles
                foreach (var variable in sortedVariables)
                {
                    var def = new ColumnDefinition();
                    def.QuestionName = variable.Name;
                    def.DestinationName = variable.Name;
                    def.QuestionLabel = variable.Label;
                    def.QuestionType = variable.Type.ToString();
                    GetValues(def, variable);


                    definitions.Add(def);

                }

                WriteListToCsv(definitions, "C:\\fakepath\\definitions.csv");
            }
        }
        public static void WriteListToCsv(List<ColumnDefinition>list, string filePath)
        {
            using (var writer = new StreamWriter(filePath)) 
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture)) 
            { csv.WriteRecords(list); }
        }

        void GetValues(ColumnDefinition def, Variable variable)
        {
            var text = string.Empty;
            var ordinal = string.Empty;
            foreach (KeyValuePair<double, string> label in variable.ValueLabels)
            {
                text = $"{text}|{label.Value}";
                ordinal = $"{ordinal}|{label.Key}";
            }

            def.ValueText = text.Trim('|');
            def.ValueOrdinals = ordinal.Trim('|');
        }

        [TestMethod]
        [DeploymentItem(@"TestFiles\test.sav")]
        public void TestReadFile()
        {
            FileStream fileStream = new FileStream("test.sav", FileMode.Open, FileAccess.Read, 
                FileShare.Read, 2048*10, FileOptions.SequentialScan);

            int[] varenieValues = {1, 2 ,1};
            string[] streetValues = { "Landsberger Straße", "Fröbelplatz", "Bayerstraße" };

            int varCount;
            int rowCount;
            try
            {
                ReadData(fileStream, out varCount, out rowCount, 
                    new Dictionary<int, Action<int, Variable>>
                    {
                        {0, (i, variable) =>
                        {
                            Assert.AreEqual("varaible ñ", variable.Label, "Label mismatch");
                            Assert.AreEqual(DataType.Numeric, variable.Type, "First file variable should be  a Number");
                        }},
                        {1, (i, variable) =>
                        {
                            Assert.AreEqual("straße", variable.Label, "Label mismatch");
                            Assert.AreEqual(DataType.Text, variable.Type, "Second file variable should be  a text");
                        }}
                    },
                    new Dictionary<int, Action<int, int, Variable, object>>
                    {
                        {0, (r, c, variable, value) =>
                        {   // All numeric values are doubles
                            Assert.IsInstanceOfType(value, typeof(double), "First row variable should be a Number");
                            double v = (double) value;
                            Assert.AreEqual(varenieValues[r], v, "Int value is different");
                        }},
                        {1, (r, c, variable, value) =>
                        {
                            Assert.IsInstanceOfType(value, typeof(string), "Second row variable should be  a text");
                            string v = (string) value;
                            Assert.AreEqual(streetValues[r], v, "String value is different");
                        }}
                    });
            }
            finally
            {
                fileStream.Close();
            }

            Assert.AreEqual(varCount, 3, "Variable count does not match");
            Assert.AreEqual(rowCount, 3, "Rows count does not match");
        }

        [TestMethod]
        [ExpectedException(typeof(SpssFileFormatException))]
        public void TestEmptyStream()
        {
            int varCount;
            int rowCount;
            ReadData(new MemoryStream(new byte[0]), out varCount, out rowCount);
        }

        [TestMethod]
        [DeploymentItem(@"TestFiles\MissingValues.sav")]
        public void TestReadMissingValuesAsNull()
        {
            FileStream fileStream = new FileStream("MissingValues.sav", FileMode.Open, FileAccess.Read,
                FileShare.Read, 2048 * 10, FileOptions.SequentialScan);

            double?[][] varValues =
            {
                new double?[]{ 0, 1, 2, 3, 4, 5, 6, 7 }, // No missing values
                new double?[]{ 0, null, 2, 3, 4, 5, 6, 7 }, // One mssing value
                new double?[]{ 0, null, null, 3, 4, 5, 6, 7 }, // Two missing values
                new double?[]{ 0, null, null, null, 4, 5, 6, 7 }, // Three missing values
                new double?[]{ 0, null, null, null, null, null, 6, 7 }, // Range
                new double?[]{ 0, null, null, null, null, null, 6, null }, // Range & one value
            };

            Action<int, int, Variable, object> rowCheck = (r, c, variable, value) =>
            {
                Assert.AreEqual(varValues[c][r], value, $"Wrong value: row {r}, variable {c}");
            };


            try
            {
                int varCount, rowCount;
                ReadData(fileStream, out varCount, out rowCount, new Dictionary<int, Action<int, Variable>>
                    {
                        {0, (i, variable) => Assert.AreEqual(MissingValueType.NoMissingValues, variable.MissingValueType)},
                        {1, (i, variable) => Assert.AreEqual(MissingValueType.OneDiscreteMissingValue, variable.MissingValueType)},
                        {2, (i, variable) => Assert.AreEqual(MissingValueType.TwoDiscreteMissingValue, variable.MissingValueType)},
                        {3, (i, variable) => Assert.AreEqual(MissingValueType.ThreeDiscreteMissingValue, variable.MissingValueType)},
                        {4, (i, variable) => Assert.AreEqual(MissingValueType.Range, variable.MissingValueType)},
                        {5, (i, variable) => Assert.AreEqual(MissingValueType.RangeAndDiscrete, variable.MissingValueType)},
                    },
                    new Dictionary<int, Action<int, int, Variable, object>>
                    {
                        {0, rowCheck},
                        {1, rowCheck},
                        {2, rowCheck},
                        {3, rowCheck},
                        {4, rowCheck},
                        {5, rowCheck},
                        {6, rowCheck},
                    });
            }
            finally
            {
                fileStream.Close();
            }
        }

        internal static void ReadData(Stream fileStream, out int varCount, out int rowCount, 
            IDictionary<int, Action<int, Variable>> variableValidators = null, IDictionary<int, Action<int, int, Variable, object>> valueValidators = null)
        {
            SpssReader spssDataset = new SpssReader(fileStream);

            varCount = 0;
            rowCount = 0;

            var variables = spssDataset.Variables;
            foreach (var variable in variables)
            {
                Debug.WriteLine("{0} - {1}", variable.Name, variable.Label);
                foreach (KeyValuePair<double, string> label in variable.ValueLabels)
                {
                    Debug.WriteLine(" {0} - {1}", label.Key, label.Value);
                }

                Action<int, Variable> checkVariable;
                if (variableValidators != null && variableValidators.TryGetValue(varCount, out checkVariable))
                {
                    checkVariable(varCount, variable);
                }

                varCount++;
            }

            foreach (var record in spssDataset.Records)
            {
                var varIndex = 0;
                foreach (var variable in variables)
                {
                    Debug.Write(variable.Name);
                    Debug.Write(':');
                    var value = record.GetValue(variable);
                    Debug.Write(value);
                    Debug.Write('\t');

                    Action<int, int, Variable, object> checkValue;
                    if (valueValidators != null && valueValidators.TryGetValue(varIndex, out checkValue))
                    {
                        checkValue(rowCount, varIndex, variable, value);
                    }

                    varIndex++;
                }
                Debug.WriteLine("");

                rowCount++;
            }
        }
    }

    public class ColumnDefinition
    {
        public string QuestionName { get; set; }
        public string DestinationName { get; set; }
        public string QuestionLabel { get; set; }
        public string QuestionType { get; set; }
        public string ByPass { get; set; }
        public string Computed { get; set; }
        public string PremodeledScore { get; set; }
        public string CanBeNull { get; set; }
        public string DataType { get; set; }
        public string ValueOrdinals { get; set; }
        public string AnswerRecodes { get; set; }
        public string ValueText { get; set; }
        public string IsDateEnd { get; set; }
        public string IsStartDate { get; set; }
        public string Key { get; set; }
        public string PremodeledNotes { get; set; }
        public string NewNotes { get; set; }
        public string Code { get; set; }
        public string Parameters { get; set; }
        public string Target { get; set; }

    }
}
