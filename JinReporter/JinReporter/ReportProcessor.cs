using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Globalization;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;

namespace JinReporter.Services
{
    public class ReportProcessor
    {
        private const string EndMarker = "[Split]";  // 数据分段标识符
        private const string Separator = "???";
        public const string NeglectMarker = "-1";

        private HashSet<int> _negCols;

        private int _split1;
        private int _split2;
        private int _split3;
        private int ProductSumRow { get => _split1 + 1; }
        private int ContrySearchStartRow { get => _split2 + 1; }
        private int ContrySearchEndRow { get => _split3 - 3; }
        private int ContrySumRow { get => _split3 - 2; }
        private int ContryFieldConfigRow { get => _split3 - 1; }

        private bool IsNeglectCol(int col)
        {
            return _negCols.Contains(col);
        }

        // 获取列映射配置--<模板列号，目标数据表配置列>
        private Dictionary<int, string> GetColumnMappings(DataRow configRow)
        {
            var mapping = new Dictionary<int, string>();
            for (int col = 1; col < configRow.Table.Columns.Count; col++)
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
        // TODO：待整合
        private Dictionary<int, string> GetColumnMappings_Country(DataRow configRow)
        {
            var mapping = new Dictionary<int, string>();
            for (int col = 1; col < configRow.Table.Columns.Count; col++)
            {
                string configValue = configRow[col].ToString();
                if (!string.IsNullOrEmpty(configValue))
                {
                    mapping[col] = configValue;
                }
            }
            return mapping;
        }

        public void ProcessTables(DataTable countryData, DataTable productData, DataTable template)
        {
            _negCols = new HashSet<int>();

            // Step 1: 解析模板结构
            ParseTemplateStructure(template, out int targetStartRow, out int productSumEndRow, out int sumStartRow, out int countryStartRow, out int countryEndRow);
            _split1 = sumStartRow;
            _split2 = countryStartRow;
            _split3 = countryEndRow;

            // Step 2: 处理商品数据
            ProcessProductData(productData, template, targetStartRow, productSumEndRow);

            // Step 3: 计算商品数据总合
            CalculateProductTotals(template, targetStartRow, productSumEndRow);

            // Step 4: 处理国家数据
            ProcessCountryData(countryData, template);

            _negCols.Clear();
            _negCols = null;
        }

        private void ParseTemplateStructure(DataTable template, out int productStartRow, out int productEndRow, out int sp1, out int sp2, out int sp3)
        {
            productStartRow = 2;  // 第三行开始数据（0-based索引）
            productEndRow = -1;
            sp1 = -1;

            // 查找数据结束标识符
            for (int i = productStartRow; i < template.Rows.Count; i++)
            {
                if (template.Rows[i][0].ToString() == EndMarker)
                {
                    productEndRow = i - 2;
                    sp1 = i;
                    break;
                }
            }

            sp2 = -1;
            for (int i = sp1 + 1; i < template.Rows.Count; i++)
            {
                if (template.Rows[i][0].ToString() == EndMarker)
                {
                    sp2 = i;
                    break;
                }
            }

            sp3 = -1;
            for (int i = sp2 + 1; i < template.Rows.Count; i++)
            {
                if (template.Rows[i][0].ToString() == EndMarker)
                {
                    sp3 = i;
                    break;
                }
            }
            if (sp1 == -1) throw new InvalidOperationException("未找到第一个数据结束标识符[Split]");
            if (sp2 == -1) throw new InvalidOperationException("未找到第二个数据结束标识符[Split]");
            if (sp3 == -1) throw new InvalidOperationException("未找到第三个数据结束标识符[Split]");
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
        }

        bool IsEmptyTemplateRow(DataRow row)
        {
            return string.IsNullOrWhiteSpace(row[0].ToString()) &&
                   string.IsNullOrWhiteSpace(row[1].ToString());
        }

        // TODO：待整合
        void ProcessSingleProductRow(DataTable productData, DataRow templateRow, Dictionary<int, string> columnMapping)
        {
            string productName = templateRow[1].ToString();

            if (string.IsNullOrEmpty(productName)) return;


            var names = productName.Split(Separator).Select(s => s.Trim());

            const int fixSearchColIndex = 1;
            var serchCol = columnMapping[fixSearchColIndex];
            EnumerableRowCollection<DataRow> productRows = null;
            if (int.TryParse(serchCol, out int serchColIndex))
            {
                productRows = productData.AsEnumerable().Where(row => names.Any(name => row.Field<string>(serchColIndex - 1)?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            else
            {
                productRows = productData.AsEnumerable().Where(row => names.Any(name => row.Field<string>(serchCol)?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            // 处理该行的每一列指定数据
            foreach (var colPair in columnMapping)
            {
                int templateCol = colPair.Key;
                if (templateCol == fixSearchColIndex) continue;

                string dataSourceCol = colPair.Value;

                if (IsNeglectCol(templateCol)) continue;

                decimal sum = 0;
                foreach (DataRow productRow in productRows)
                {
                    if (int.TryParse(dataSourceCol, out int colIndex))
                    {
                        sum += ConvertToDecimal(productRow[colIndex - 1]);
                    }
                    else if (productRow.Table.Columns.Contains(dataSourceCol))
                    {
                        sum += ConvertToDecimal(productRow[dataSourceCol]);
                    }
                }
                templateRow[templateCol] = sum;
            }
        }

        private void CalculateProductTotals(DataTable template, int targetStartRow, int targetEndRow)
        {
            DataRow sumRow = template.Rows[ProductSumRow];
            if (sumRow == null) return;
            for (int col = 2; col < template.Columns.Count; col++)
            {
                if (IsNeglectCol(col)) continue;
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
            DataRow configRow = template.Rows[ContryFieldConfigRow];
            DataRow countrySumRow = template.Rows[ContrySumRow];

            // 逐列处理。--TODO：优化，和商品数据的遍历统一。
            for (int col = 2; col < template.Columns.Count; col++)
            {
                if (IsNeglectCol(col)) continue;

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
            // TODO：扩展成多行范围
            ProcessCounrtryDataSearch(countryData, template, ContrySearchStartRow, ContrySearchEndRow);
        }

        private void ProcessCounrtryDataSearch(DataTable countryData, DataTable template, int targetStartRow, int targetEndRow)
        {
            DataRow configRow = template.Rows[ContryFieldConfigRow];
            //DataRow countrySearchRow = template.Rows[ContrySearchRow];

            // 获取列映射配置
            Dictionary<int, string> columnMapping = GetColumnMappings_Country(configRow);

            // 处理每个行（跳过空行）
            for (int rowIdx = targetStartRow; rowIdx <= targetEndRow; rowIdx++)
            {
                DataRow templateRow = template.Rows[rowIdx];

                // 跳过空行（代码和名为空的行）
                if (IsEmptyTemplateRow(templateRow)) continue;

                // 处理有效数据行
                ProcessSingleProductRow(countryData, templateRow, columnMapping);
            }
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