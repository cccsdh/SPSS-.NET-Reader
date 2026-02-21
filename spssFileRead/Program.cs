using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using SpssLib.DataReader;
using SpssLib.FileParser;
using SpssLib.SpssDataset;

namespace spssFileRead
{
    public class Question
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
        public int SourceQuestionId { get; set; }
        public DataType Type { get; set; }
        public IEnumerable<Answer> Answers { get; set; }

        public Question()
        {
        }
    }
    public class Answer
    {
        public string Name { get; set; }
        public int Ordinal { get; set; }
        public string Text { get; set; }
        public string SourceAnswerId { get; set; }
        public bool IncludesOpenAnswer { get; set; }

        public Answer()
        {
        }
    }
    public static class ExcludedColumns
    {
        public static List<string> Names()
        {
            var names = new List<string>();
            names.Add("cfirecid");
            names.Add("wrespid");
            names.Add("wstatus");
            names.Add("datstart");
            names.Add("datend");

            return names;
        }
    }

class Program
    {
        private static List<string> _lines;
        static void Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Console.WriteLine("Usage: spssFileRead <path to .sav file> <output folder>");
                return;
            }

            var savPath = args[0];
            var outputFolder = args[1];

            if (!File.Exists(savPath))
            {
                Console.WriteLine($"File not found: {savPath}");
                return;
            }

            try
            {
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to create or access output folder '{outputFolder}': {ex.Message}");
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(savPath);
            var questionsPath = Path.Combine(outputFolder, baseName + "_questions.json");
            var csvPath = Path.Combine(outputFolder, baseName + ".csv");

            using (FileStream fileStream = new FileStream(savPath, FileMode.Open, FileAccess.Read, FileShare.Read, 2048 * 10,
                                              FileOptions.SequentialScan))
            {
                // Create the reader, this will read the file header
                SpssReader spssDataset = new SpssReader(fileStream);

                // Generate all of the questions
                var questions = new List<Question>();
                foreach (var variable in spssDataset.Variables)
                {
                    if (!ExcludedColumns.Names().Contains(variable.Name.ToLower()))
                    {
                        var question = new Question();
                        question.Name = variable.Name;
                        question.Label = variable.Label;
                        question.Type = variable.Type;// == SpssLib.SpssDataset.DataType.Numeric ? 5 : 3;
                        question.SourceQuestionId = variable.Index;

                        var answers = new List<Answer>();
                        // Display value-labels collection
                        foreach (KeyValuePair<double, string> label in variable.ValueLabels)
                        {
                            var answer = new Answer();
                            answer.Name = question.Name + "_" + label.Key;                     
                            answer.Text = label.Value;
                            answer.Ordinal = Convert.ToInt32(label.Key);
                            answer.SourceAnswerId = answer.Name;
                            answer.IncludesOpenAnswer = false;
                            answers.Add(answer);
                        }
                        if (answers.Count > 0)
                            question.Answers = answers;

                        questions.Add(question);
                    }
                }
                var jsonQuestions = JsonConvert.SerializeObject(questions);
                File.WriteAllText(questionsPath, jsonQuestions);

                // Iterate through all the varaibles
                //foreach (var variable in spssDataset.Variables)
                //{
                //    // Display name and label
                //    Console.WriteLine("{0} - {1}", variable.Name, variable.Label);
                //    // Display value-labels collection
                //    foreach (KeyValuePair<double, string> label in variable.ValueLabels)
                //    {
                //        Console.WriteLine(" {0} - {1}", label.Key, label.Value);
                //    }
                //}
                _lines = new List<string>();
                // Iterate through all data rows in the file
                BuildHeader(spssDataset);
                foreach (var record in spssDataset.Records)
                {
                    var delimitedLine = "";
                    foreach (var variable in spssDataset.Variables)
                    {
                        if (delimitedLine.Length > 0)
                        {
                            delimitedLine += ",";
                        }

                        delimitedLine += $"{record.GetValue(variable)}";
                    }
                    _lines.Add(delimitedLine);
                }

                OutputFile(csvPath);
            }
        }
        static void BuildHeader(SpssReader spssDataset)
        {
            var delimitedLine = "";
            foreach (var variable in spssDataset.Variables)
            {
                if (delimitedLine.Length > 0)
                {
                    delimitedLine += ",";
                }

                delimitedLine += $"{variable.Name}";
            }
            _lines.Add(delimitedLine);
        }
        private static void OutputFile(string path)
        {
            File.WriteAllLines(path, _lines.ToArray());
        }
    }
}
