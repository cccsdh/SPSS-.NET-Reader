using Microsoft.Win32;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using System.Globalization;
using System.Net;
using SpssLib.FileParser;
using ClosedXML.Excel;

namespace SpssViewer
{
    public partial class MainWindow : Window
    {
        private TabControl _tabControl;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "SPSS files (*.sav)|*.sav|All files (*.*)|*.*",
                Title = "Open SPSS .sav file"
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            string path = dlg.FileName;
            StatusText.Text = "Loading...";
            try
            {
                DataTable dt = null;

                // Open and parse metadata synchronously to get total rows for progress
                SavFileParser parser;
                int totalRows = 0;
                using (var fs = File.OpenRead(path))
                {
                    parser = new SavFileParser(fs);
                    parser.ParseMetaData();
                    if (parser.MetaData?.HeaderRecord != null)
                    {
                        totalRows = parser.MetaData.HeaderRecord.CasesCount;
                    }

                    // Prepare progress reporter
                    Progress<int> progress = new Progress<int>(count =>
                    {
                        if (totalRows > 0)
                        {
                            ProgressBar.Maximum = totalRows;
                            ProgressBar.IsIndeterminate = false;
                        }
                        else
                        {
                            ProgressBar.IsIndeterminate = true;
                        }
                        ProgressBar.Value = count;
                        ProgressText.Text = totalRows > 0 ? $"{count}/{totalRows}" : $"{count} rows";
                        StatusText.Text = $"Loading {count} rows...";
                    });

                    // Capture variables for column creation
                    var variables = parser.Variables.ToList();

                    // Build DataTable on background thread while reporting progress
                    dt = await Task.Run(() =>
                    {
                        var table = new DataTable();
                        var usedNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var variable in variables)
                        {
                            var baseName = string.IsNullOrWhiteSpace(variable.Name) ? $"V{variable.Index}" : variable.Name;
                            var colName = baseName;
                            int suffix = 1;
                            while (usedNames.Contains(colName))
                            {
                                colName = baseName + "_" + suffix++;
                            }
                            usedNames.Add(colName);
                            table.Columns.Add(colName, typeof(object));
                        }

                        int count = 0;
                        foreach (var record in parser.ParsedDataRecords)
                        {
                            var rowValues = record.ToArray();
                            table.Rows.Add(rowValues);
                            count++;
                            ((IProgress<int>)progress).Report(count);
                        }

                        return table;
                    });
                }

                // Ensure TabControl exists in the visual tree
                if (_tabControl == null)
                {
                    // DataGrid comes from XAML; replace it with TabControl in its parent
                    var parent = DataGrid.Parent as Panel;
                    if (parent != null)
                    {
                        int index = parent.Children.IndexOf(DataGrid);
                        parent.Children.Remove(DataGrid);
                        _tabControl = new TabControl();
                        parent.Children.Insert(index, _tabControl);
                    }
                    else
                    {
                        // Fallback: add to window content
                        _tabControl = new TabControl();
                        this.Content = _tabControl;
                    }
                }

                // Create a DataGrid for this dataset and add as a new tab
                var grid = new System.Windows.Controls.DataGrid
                {
                    AutoGenerateColumns = true,
                    CanUserAddRows = false,
                };

                // Limit column sizes to cell contents and cap maximum width. Also trim long text.
                grid.AutoGeneratingColumn += (s, args) =>
                {
                    // Size column to cells (content)
                    args.Column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
                    // Provide sensible min/max widths
                    args.Column.MinWidth = 30;
                    args.Column.MaxWidth = 400;
                    args.Column.CanUserResize = true;

                    // If it's a text column, set element style to trim long text with ellipsis
                    var textCol = args.Column as DataGridTextColumn;
                    if (textCol != null)
                    {
                        var textStyle = new Style(typeof(TextBlock));
                        textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                        textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
                        textCol.ElementStyle = textStyle;
                    }
                };

                // Bind after attaching handlers so AutoGeneratingColumn fires
                grid.ItemsSource = dt.DefaultView;

                var tab = new TabItem { Header = System.IO.Path.GetFileName(path), Content = grid };
                // Store the DataTable on the tab so we can export later
                tab.Tag = dt;
                _tabControl.Items.Add(tab);
                _tabControl.SelectedItem = tab;

                StatusText.Text = $"Loaded {dt.Rows.Count} rows, {dt.Columns.Count} columns.";
            }
            catch (SpssFileFormatException ex)
            {
                StatusText.Text = "Invalid SPSS file or unsupported .sav format.";
                MessageBox.Show($"SPSS parse error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error loading file.";
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportXlsxButton_Click(object sender, RoutedEventArgs e)
        {
            var dt = GetCurrentDataTable();
            if (dt == null)
            {
                MessageBox.Show("No dataset loaded. Open a .sav file first.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*", FileName = "data.xlsx" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Minimum = 0;
                ProgressBar.Maximum = dt.Rows.Count;
                ProgressBar.Value = 0;

                var progress = new Progress<int>(count =>
                {
                    ProgressBar.Value = count;
                    ProgressText.Text = $"Exporting {count}/{dt.Rows.Count}";
                });

                await Task.Run(() =>
                {
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("Sheet1");
                        // Header
                        for (int c = 0; c < dt.Columns.Count; c++)
                        {
                            ws.Cell(1, c + 1).SetValue(dt.Columns[c].ColumnName ?? string.Empty);
                        }

                        int r = 2;
                        foreach (DataRow row in dt.Rows)
                        {
                            for (int c = 0; c < dt.Columns.Count; c++)
                            {
                                var val = row[c];
                                if (val == DBNull.Value) val = null;
                                if (val == null)
                                {
                                    ws.Cell(r, c + 1).SetValue(string.Empty);
                                }
                                else if (val is double || val is float || val is decimal)
                                {
                                    ws.Cell(r, c + 1).SetValue(Convert.ToDouble(val, CultureInfo.InvariantCulture));
                                }
                                else if (val is int || val is long || val is short || val is byte)
                                {
                                    ws.Cell(r, c + 1).SetValue(Convert.ToInt64(val));
                                }
                                else
                                {
                                    ws.Cell(r, c + 1).SetValue(Convert.ToString(val, CultureInfo.InvariantCulture));
                                }
                            }
                            ((IProgress<int>)progress).Report(r - 1);
                            r++;
                        }

                        wb.SaveAs(dlg.FileName);
                    }
                });

                MessageBox.Show("Export completed.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ProgressBar.Value = 0;
                ProgressText.Text = string.Empty;
            }
        }

        private DataTable GetCurrentDataTable()
        {
            if (_tabControl == null || _tabControl.SelectedItem == null) return null;
            var tab = _tabControl.SelectedItem as TabItem;
            return tab?.Tag as DataTable;
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var dt = GetCurrentDataTable();
            if (dt == null)
            {
                MessageBox.Show("No dataset loaded. Open a .sav file first.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", FileName = "data.csv" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                ExportToCsv(dt, dlg.FileName, Encoding.UTF8);
                MessageBox.Show("Export completed.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        

        private static void ExportToCsv(DataTable table, string path, Encoding encoding)
        {
            using (var sw = new StreamWriter(path, false, encoding))
            {
                // Header
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    if (i > 0) sw.Write(',');
                    sw.Write(EscapeCsv(table.Columns[i].ColumnName));
                }
                sw.WriteLine();

                foreach (DataRow row in table.Rows)
                {
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        if (i > 0) sw.Write(',');
                        var obj = row[i];
                        if (obj == DBNull.Value || obj == null)
                        {
                            // empty
                        }
                        else if (obj is double || obj is float || obj is decimal)
                        {
                            sw.Write(Convert.ToString(obj, CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sw.Write(EscapeCsv(Convert.ToString(obj, CultureInfo.InvariantCulture)));
                        }
                    }
                    sw.WriteLine();
                }
            }
        }

        private static string EscapeCsv(string s)
        {
            if (s == null) return string.Empty;
            bool mustQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
            var res = s.Replace("\"", "\"\"");
            if (mustQuote) res = "\"" + res + "\"";
            return res;
        }

        
    }
}
