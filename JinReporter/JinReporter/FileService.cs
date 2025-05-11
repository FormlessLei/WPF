using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using OfficeOpenXml;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using OfficeOpenXml.FormulaParsing.Excel.Functions.RefAndLookup;
using OfficeOpenXml.Packaging.Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
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

        public DataTable ReadExcelSheet(ExcelWorksheet sheet)
        {
            if (sheet == null) throw new Exception($"Sheet不存在");

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

        private bool ShouldSkipCell(ExcelRange configCell,ExcelRange targetCell ,object dataValue)
        {
            // 1. 检查忽略标记
            if (configCell.Text == ReportProcessor.NeglectMarker)
                return true;

            // 2. 检查公式
            if (!string.IsNullOrEmpty(targetCell.Formula))
                return true;

            // 3. 检查空值
            return IsEmptyData(dataValue);
        }

        // 辅助方法：检查是否为空数据
        private bool IsEmptyData(object value)
        {
            return value == null ||
                   value == DBNull.Value ||
                   (value is string str && string.IsNullOrWhiteSpace(str));
        }

        public string ProcessAndSaveResults(List<DataSourceConfig> configs, string templatePath)
        {
            // 创建全新的Excel文件
            string outputPath = GetOutputFilePath(templatePath);

            using (var package = new ExcelPackage(new FileInfo(outputPath)))
            {
                // 读取模板文件（只读模式）
                using (var templateStream = File.OpenRead(templatePath))
                using (var templatePackage = new ExcelPackage(templateStream))
                {
                    foreach (var config in configs)
                    {
                        //ProcessSingleTemplate(package, config);
                        // 处理每个模板
                        var templateSheet = templatePackage.Workbook.Worksheets["模板_" + config.TemplateName];
                        if (templateSheet != null)
                        {
                            // 创建结果Sheet（复制模板）
                            string newSheetName = $"{config.TemplateName}";
                            var newSheet = package.Workbook.Worksheets.Add(newSheetName, templateSheet);

                            // 填充数据...
                            var countryData = ReadDataFile(config.CountryTablePath);
                            var productData = ReadDataFile(config.ProductTablePath);
                            var templateData = ReadExcelSheet(newSheet);// 可以直接放newSheet进去

                            // 处理数据
                            new ReportProcessor().ProcessTables(countryData, productData, templateData);

                            SetData(newSheet, templateData);
                        }
                    }
                }

                // 保存新文件
                package.Save();
            }

            return outputPath;
        }

        private void SetData(ExcelWorksheet targetSheet,DataTable data)
        {
            // 填充数据
            for (int i = 2; i < data.Rows.Count - 1; i++)
            {
                for (int j = 0; j < data.Columns.Count; j++)
                {
                    var configCell = targetSheet.Cells[2, j + 1].Text;
                    if (configCell == ReportProcessor.NeglectMarker) continue;

                    if (ShouldSkipCell(targetSheet.Cells[2, j + 1], targetSheet.Cells[i + 1, j + 1], data.Rows[i][j]))
                        continue;

                    //newSheet.Cells[i + 1, j + 1].Value = data.Rows[i][j];

                    if (decimal.TryParse(data.Rows[i][j].ToString(), out decimal numValue))
                    {
                        targetSheet.Cells[i + 1, j + 1].Value = numValue;
                    }
                    else
                    {
                        targetSheet.Cells[i + 1, j + 1].Value = data.Rows[i][j]; // 或处理异常
                    }
                }
            }
        }

        private string GetOutputFilePath(string templatePath)
        {
            // 获取模板文件所在的目录
            string templateDir = Path.GetDirectoryName(templatePath);

            // 如果模板目录为空（比如templatePath是根目录），则使用桌面目录作为后备
            if (string.IsNullOrEmpty(templateDir))
            {
                templateDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            // 生成带时间戳的输出文件名
            return Path.Combine(templateDir, $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
    }
}