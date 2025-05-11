using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Globalization;

namespace JinReporter.Services
{
    public class ReportProcessor
    {
        private const string EndMarker = "N";  // 数据结束标识符
        private const string Separator = "???";
        public const string NeglectMarker = "-1";

        private HashSet<int> _negCols;


        private int _sumStartRow;
        private int ContrySumRow { get => _sumStartRow + 1; }
        private int ProductSumRow { get => _sumStartRow; }
        private int DiffRow { get => _sumStartRow - 2; }
        private int ContryFieldConfigRow { get => _sumStartRow + 2; }

        private bool IsNegCol(int col)
        {
            return _negCols.Contains(col);
        }

        // 辅助方法：获取列映射配置
        private Dictionary<int, string> GetColumnMappings(DataRow configRow)
        {
            var mapping = new Dictionary<int, string>();
            for (int col = 2; col < configRow.Table.Columns.Count; col++)
            {
                string configValue = configRow[col].ToString();
                if (!string.IsNullOrEmpty(configValue))
                {
                    mapping[col] = configValue;
                }
                if (configValue == NeglectMarker)
                {
                    _negCols.Add(col);
                }
            }
            return mapping;
        }


        public void ProcessTables(DataTable countryData, DataTable productData, DataTable template)
        {
            _negCols = new HashSet<int>();

            // Step 1: 解析模板结构
            ParseTemplateStructure(template, out int targetStartRow, out int targetEndRow, out int sumStartRow);
            _sumStartRow = sumStartRow;

            // Step 2: 处理商品数据
            ProcessProductData(productData, template, targetStartRow, targetEndRow);

            // Step 3: 计算商品数据总合
            CalculateProductTotals(template, targetStartRow, targetEndRow);

            // Step 4: 处理国家数据
            ProcessCountryData(countryData, template);

            // Step 5: 计算差值
            //CalculateDifferences(template);

            _negCols.Clear();
            _negCols = null;
        }

        private void ParseTemplateStructure(DataTable template, out int dataStartRow, out int dataEndRow, out int sumStartRow)
        {
            dataStartRow = 2;  // 第三行开始数据（0-based索引）
            dataEndRow = -1;
            sumStartRow = -1;

            // 查找数据结束标识符
            for (int i = dataStartRow; i < template.Rows.Count; i++)
            {
                if (template.Rows[i][0].ToString() == EndMarker)
                {
                    dataEndRow = i - 2;
                    sumStartRow = i + 1;
                    break;
                }
            }

            if (dataEndRow == -1) throw new InvalidOperationException("未找到数据结束标识符N");
        }

        private void ProcessProductData(DataTable productData, DataTable template, int targetStartRow, int targetEndRow)
        {
            // 获取列映射配置
            Dictionary<int, string> columnMapping = GetColumnMappings(template.Rows[1]);

            // 处理每个商品行（跳过空行）
            for (int rowIdx = targetStartRow; rowIdx <= targetEndRow; rowIdx++)
            {
                DataRow templateRow = template.Rows[rowIdx];

                // 跳过空行（商品代码和商品名为空的行）
                if (IsEmptyTemplateRow(templateRow)) continue;

                // 处理有效数据行
                ProcessSingleProductRow(productData, templateRow, columnMapping);
            }

            bool IsEmptyTemplateRow(DataRow row)
            {
                return string.IsNullOrWhiteSpace(row[0].ToString()) &&
                       string.IsNullOrWhiteSpace(row[1].ToString());
            }

            void ProcessSingleProductRow(DataTable productData, DataRow templateRow, Dictionary<int, string> columnMapping)
            {
                string productName = templateRow[1].ToString();

                var names = productName.Split(Separator).Select(s => s.Trim());
                var productRows = productData.AsEnumerable()
                    .Where(row => names.Any(name =>
                        row.Field<string>("商品名")?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false));

                foreach (var colPair in columnMapping)
                {
                    int templateCol = colPair.Key;
                    string dataSourceCol = colPair.Value;

                    if (IsNegCol(templateCol)) continue;

                    decimal sum = 0;
                    int count = 0;
                    foreach (DataRow productRow in productRows)
                    {
                        if (int.TryParse(dataSourceCol, out int colIndex))
                        {
                            sum += ConvertToDecimal(productRow[colIndex - 1]);
                            count++;
                        }
                        else if (productRow.Table.Columns.Contains(dataSourceCol))
                        {
                            sum += ConvertToDecimal(productRow[dataSourceCol]);
                        }
                    }
                    templateRow[templateCol] = sum;
                }
            }
        }

        private void CalculateProductTotals(DataTable template, int targetStartRow, int targetEndRow)
        {
            DataRow sumRow = template.Rows[ProductSumRow];
            if (sumRow == null) return;
            for (int col = 2; col < template.Columns.Count; col++)
            {
                if (IsNegCol(col)) continue;
                decimal total = 0;
                for (int row = targetStartRow; row <= targetEndRow; row++)
                {
                    total += ConvertToDecimal(template.Rows[row][col]);
                }
                sumRow[col] = total;
            }
        }

        private void ProcessCountryData(DataTable countryData, DataTable template)
        {
            DataRow configRow = template.Rows[ContryFieldConfigRow];  // 第二行配置
            DataRow countrySumRow = template.Rows[ContrySumRow];  // M+2行

            for (int col = 2; col < template.Columns.Count; col++)
            {
                if (IsNegCol(col)) continue;

                string dataSourceCol = configRow[col].ToString();
                decimal countryTotal = 0;

                // 计算国家数据总和
                foreach (DataRow countryRow in countryData.Rows)
                {
                    if (int.TryParse(dataSourceCol, out int colIndex))
                    {
                        countryTotal += ConvertToDecimal(countryRow[colIndex - 1]);
                    }
                    else if (countryData.Columns.Contains(dataSourceCol))
                    {
                        countryTotal += ConvertToDecimal(countryRow[dataSourceCol]);
                    }
                }

                countrySumRow[col] = countryTotal;
            }
        }

        private void CalculateDifferences(DataTable template)
        {
            DataRow countrySumRow = template.Rows[ContrySumRow];
            DataRow productSumRow = template.Rows[ProductSumRow];
            DataRow diffRow = template.Rows[DiffRow];

            for (int col = 2; col < template.Columns.Count; col++)
            {
                if (IsNegCol(col)) continue;

                decimal country = ConvertToDecimal(countrySumRow[col]);
                decimal product = ConvertToDecimal(productSumRow[col]);
                diffRow[col] = country - product;
            }
        }


        private double ConvertToDouble(object value)
        {
            if (value == DBNull.Value || value == null) return 0;

            // 处理带引号的数字和千位分隔符
            string strValue = value.ToString()
                .Replace("\"", "")
                .Replace(",", "")
                .Trim();

            if (double.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }
            return 0;
        }

        private decimal ConvertToDecimal(object value)
        {
            if (value == DBNull.Value || value == null) return 0m;

            string strValue = value.ToString()
                .Replace("\"", "")
                .Replace(",", "")
                .Trim();

            if (decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }
            return 0m;
        }
    }
}