using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using OfficeOpenXml;
using OfficeOpenXml.Packaging.Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Interop;

namespace JinReporter.Services
{
    public class FileService
    {
        public DataTable ReadDataFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);

            string extension = Path.GetExtension(filePath).ToLower();

            return extension switch
            {
                ".csv" => ReadCsvFile(filePath),
                ".xlsx" or ".xls" => ReadExcelFile(filePath),
                _ => throw new NotSupportedException($"不支持的文件格式: {extension}")
            };
        }

        private DataTable ReadCsvFile(string filePath)
        {
            var table = new DataTable();
            // 先自动检测分隔符
            //char delimiter = DetectDelimiter(filePath);
            char delimiter = '\t';
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter.ToString(), // 使用检测到的分隔符
                HasHeaderRecord = false,
                BadDataFound = _ => { },
                MissingFieldFound = null,
                Encoding = Encoding.UTF8,
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true
            };

            int headerRowIndex = 2; // 表头在第3行（0-based索引为2）
            bool hasProcessedHeader = false;
            int currentRow = 0;

            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, config))
            {
                while (csv.Read())
                {
                    // 跳过表头之前的行
                    if (currentRow++ < headerRowIndex) continue;

                    // 处理表头
                    if (!hasProcessedHeader)
                    {
                        for (int i = 0; i < csv.Parser.Count; i++)
                        {
                            string header = csv.GetField(i)?.Trim();
                            table.Columns.Add(string.IsNullOrEmpty(header) ? $"Column{i + 1}" : header);
                        }
                        hasProcessedHeader = true;
                        continue;
                    }

                    // 处理数据行
                    var row = table.NewRow();
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        if (i < csv.Parser.Count)
                        {
                            try
                            {
                                string stringValue = csv.GetField(i);
                                if (string.IsNullOrEmpty(stringValue))
                                {
                                    row[i] = DBNull.Value;
                                }
                                else
                                {
                                    row[i] = ConvertToDataType(stringValue, table.Columns[i].DataType);
                                }
                            }
                            catch
                            {
                                row[i] = DBNull.Value;
                            }
                        }
                        else
                        {
                            row[i] = DBNull.Value;
                        }
                    }
                    table.Rows.Add(row);
                }
            }


            return table;
        }
        private object ConvertToDataType(string value, Type targetType)
        {
            if (string.IsNullOrEmpty(value))
                return DBNull.Value;

            try
            {
                if (targetType == typeof(string))
                    return value.Trim();

                if (targetType == typeof(int))
                    return int.Parse(value);

                if (targetType == typeof(double))
                    return double.Parse(value);

                if (targetType == typeof(decimal))
                    return decimal.Parse(value);

                if (targetType == typeof(DateTime))
                    return DateTime.Parse(value);

                if (targetType == typeof(bool))
                {
                    if (int.TryParse(value, out int intValue))
                        return intValue != 0;
                    return bool.Parse(value);
                }

                // 默认返回字符串
                return value;
            }
            catch
            {
                return DBNull.Value;
            }
        }
        // 自动检测分隔符的方法
        private char DetectDelimiter(string filePath)
        {
            var delimiterScores = new Dictionary<char, int>
            {
                { '\t', 0 }, { ',', 0 }, { ';', 0 }, { '|', 0 }
            };

            using (var reader = new StreamReader(filePath))
            {
                string? line;
                MainWindow.LogMsg(reader.BaseStream.Position.ToString());
                while ((line = reader.ReadLine()) != null && reader.BaseStream.Position < 1024) // 只检查前1KB
                {
                    MainWindow.LogMsg(reader.BaseStream.Position.ToString());
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    foreach (var delim in delimiterScores.Keys.ToList())
                    {
                        bool inQuotes = false;
                        int count = 0;

                        foreach (char c in line)
                        {
                            if (c == '"') inQuotes = !inQuotes;
                            if (!inQuotes && c == delim) count++;
                        }

                        delimiterScores[delim] += count;
                    }
                }
            }

            return delimiterScores.OrderByDescending(kv => kv.Value)
                                 .First().Key;
        }

        private DataTable ReadExcelFile(string filePath)
        {
            try
            {
                var configuration = new ExcelReaderConfiguration
                {
                    FallbackEncoding = System.Text.Encoding.GetEncoding(1252)
                };

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = ExcelReaderFactory.CreateReader(stream, configuration);
                return reader.AsDataSet().Tables[0];
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"读取Excel文件失败: {Path.GetFileName(filePath)}", ex);
            }
        }

        //public void SaveData(DataTable table, string filePath)
        //{
        //    string extension = Path.GetExtension(filePath).ToLower();
        //    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

        //    switch (extension)
        //    {
        //        case ".csv":
        //            SaveAsCsv(table, filePath);
        //            break;
        //        case ".xlsx":
        //        case ".xls":
        //            SaveWithTemplate(table, filePath);
        //            break;
        //        default:
        //            var newPath = Path.ChangeExtension(filePath, ".csv");
        //            SaveAsCsv(table, newPath);
        //            break;
        //    }
        //}

        //private void SaveAsCsv(DataTable table, string filePath)
        //{
        //    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        //    {
        //        Encoding = new UTF8Encoding(true),
        //        ShouldQuote = args => true
        //    };

        //    using var writer = new StreamWriter(filePath, false, config.Encoding);
        //    using var csv = new CsvWriter(writer, config);

        //    foreach (DataColumn column in table.Columns)
        //    {
        //        csv.WriteField(column.ColumnName);
        //    }
        //    csv.NextRecord();

        //    foreach (DataRow row in table.Rows)
        //    {
        //        foreach (var item in row.ItemArray)
        //        {
        //            csv.WriteField(item);
        //        }
        //        csv.NextRecord();
        //    }
        //}

        //private void SaveAsExcel(DataTable table, string filePath, string customSheetName = null)
        //{
        //    var file = new FileInfo(filePath);
        //    using var package = new ExcelPackage(file);

        //    // 确定工作表名称（优先使用自定义名称，否则使用日期）
        //    string baseSheetName = !string.IsNullOrWhiteSpace(customSheetName)
        //        ? customSheetName
        //        : DateTime.Today.ToString("yyyy-MM-dd");

        //    // 清理非法字符（Excel工作表名称不能包含:\/?*[]）
        //    string sanitizedName = new string(baseSheetName
        //        .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
        //        .ToArray())
        //        .Trim();

        //    // 确保名称长度合法（31字符限制）和非空
        //    if (sanitizedName.Length > 31)
        //    {
        //        sanitizedName = sanitizedName.Substring(0, 31);
        //    }
        //    if (string.IsNullOrEmpty(sanitizedName))
        //    {
        //        sanitizedName = "Data";
        //    }

        //    // 确保名称唯一
        //    string sheetName = sanitizedName;
        //    int counter = 1;
        //    while (package.Workbook.Worksheets.Any(ws => ws.Name == sheetName))
        //    {
        //        sheetName = $"{sanitizedName}_{counter++}";
        //        if (sheetName.Length > 31)
        //        {
        //            sheetName = $"S{counter}";
        //        }
        //    }

        //    // 创建工作表并保存
        //    var worksheet = package.Workbook.Worksheets.Add(sheetName);
        //    worksheet.Cells.LoadFromDataTable(table, true);
        //    worksheet.Cells.Style.Numberformat.Format = "@";
        //    worksheet.Cells.AutoFitColumns();

        //    package.Save();
        //}

        public void SaveWithTemplate(DataTable data, string filePath, string suffix, string templateSheetName = "模板")
        {
            var file = new FileInfo(filePath);
            using var package = new ExcelPackage(file);

            // 检查模板是否存在
            ExcelWorksheet templateSheet = null;
            foreach (var sheet in package.Workbook.Worksheets)
            {
                if (sheet.Name == templateSheetName)
                {
                    templateSheet = sheet;
                    break;
                }
            }

            if (templateSheet == null)
            {
                MainWindow.LogMsg($"模板工作表'{templateSheetName}'不存在，使用第一张表");
                templateSheet = package.Workbook.Worksheets[0];
            }

            if (templateSheet == null)
            {
                throw new FileNotFoundException($"无法获取模版");
            }

            // 创建基于日期的新工作表名称
            string newSheetName = GenerateNewSheetName(package, $"{DateTime.Today.ToString("yyyy-MM-dd")}_{suffix}");

            // 复制模板到新工作表
            var newSheet = package.Workbook.Worksheets.Add(newSheetName, templateSheet);

            // 填充数据到新工作表（从第2行开始，假设第1行是标题）
            int startRow = 1; // 从第2行开始填充数据
            for (int i = 0; i < data.Rows.Count; i++)
            {
                for (int j = 0; j < data.Columns.Count; j++)
                {
                    var configCell = newSheet.Cells[2, j + 1].Text;
                    if (configCell == ReportProcessor.NeglectMarker) continue;

                    newSheet.Cells[i + 1, j + 1].Value = data.Rows[i][j];
                }
            }

            package.Save();
        }

        private string GenerateNewSheetName(ExcelPackage package, string baseName)
        {
            string sheetName = baseName;
            int counter = 1;

            while (package.Workbook.Worksheets.Any(ws => ws.Name.Equals(sheetName, System.StringComparison.OrdinalIgnoreCase)))
            {
                sheetName = $"{baseName}_{counter++}";

                // 确保不超过31字符限制
                if (sheetName.Length > 31)
                {
                    sheetName = sheetName.Substring(0, 31);
                }
            }

            return sheetName;
        }

        public DataTable ReadExcelSheet(ExcelPackage package, string sheetName)
        {
            var sheet = package.Workbook.Worksheets[sheetName];
            if (sheet == null) throw new Exception($"Sheet不存在: {sheetName}");

            var table = new DataTable();

            // 读取Sheet内容到DataTable
            foreach (var firstRowCell in sheet.Cells[1, 1, 1, sheet.Dimension.End.Column])
            {
                table.Columns.Add(firstRowCell.Text);
            }

            for (int rowNum = 1; rowNum <= sheet.Dimension.End.Row; rowNum++)
            {
                var row = table.NewRow();
                for (int colNum = 1; colNum <= sheet.Dimension.End.Column; colNum++)
                {
                    row[colNum - 1] = sheet.Cells[rowNum, colNum].Text;
                }
                table.Rows.Add(row);
            }

            return table;
        }

        public void CreateResultSheet(ExcelPackage package, ExcelWorksheet templateSheet,
                                    DataTable data, string newSheetName)
        {
            // 确保Sheet名称唯一
            newSheetName = GenerateNewSheetName(package, newSheetName);

            // 复制模板样式
            var newSheet = package.Workbook.Worksheets.Add(newSheetName, templateSheet);

            // 填充数据
            for (int i = 2; i < data.Rows.Count - 1; i++)
            {
                for (int j = 0; j < data.Columns.Count; j++)
                {
                    var configCell = newSheet.Cells[2, j + 1].Text;
                    if (configCell == ReportProcessor.NeglectMarker) continue;

                    newSheet.Cells[i + 1, j + 1].Value = data.Rows[i][j];
                }
            }
        }

    }
}