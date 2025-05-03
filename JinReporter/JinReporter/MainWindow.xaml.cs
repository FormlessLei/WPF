using JinReporter.Services;
using OfficeOpenXml;
using System;
using System.Diagnostics.PerformanceData;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Path = System.IO.Path;
using System.Collections.ObjectModel;
using System.Linq;
using ExcelDataReader;
using System.Collections.Generic;

namespace JinReporter
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<DataSourceConfig> _dataSources;
        private readonly FileService _fileService;
        private readonly ReportProcessor _reportProcessor;
        private List<TemplateInfo> _detectedTemplates;

        public MainWindow()
        {
            InitializeComponent();

            _fileService = new FileService();
            _reportProcessor = new ReportProcessor();
            _dataSources = new ObservableCollection<DataSourceConfig>();
            _detectedTemplates = new List<TemplateInfo>();

            DataSourceControls.ItemsSource = _dataSources;

            //SetDefaultPaths();
        }

        private void ConfirmTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string templatePath = TemplateFileBox.Text;
                if (!File.Exists(templatePath))
                {
                    MessageBox.Show("模板文件不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 检测模板Sheet
                _detectedTemplates = DetectTemplates(templatePath);
                if (_detectedTemplates.Count == 0)
                {
                    MessageBox.Show("未找到有效的模板Sheet！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 生成对应的数据源配置
                _dataSources.Clear();
                foreach (var template in _detectedTemplates)
                {
                    _dataSources.Add(new DataSourceConfig
                    {
                        TemplateName = template.TemplateName,
                        CountryTablePath = "",
                        ProductTablePath = ""
                    });
                }

                MessageBox.Show($"已检测到 {_detectedTemplates.Count} 个模板", "成功",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private List<TemplateInfo> DetectTemplates(string filePath)
        {
            var templates = new List<TemplateInfo>();

            using (var stream = File.OpenRead(filePath))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                do
                {
                    string sheetName = reader.Name;
                    if (sheetName.StartsWith("模板_"))
                    {
                        string templateName = sheetName.Substring(3); // 去掉"模板_"前缀
                        templates.Add(new TemplateInfo
                        {
                            SheetName = sheetName,
                            TemplateName = templateName
                        });
                    }
                } while (reader.NextResult());
            }

            return templates;
        }


        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string templatePath = TemplateFileBox.Text;

                // 验证所有数据源文件
                foreach (var config in _dataSources)
                {
                    if (!File.Exists(config.CountryTablePath) || !File.Exists(config.ProductTablePath))
                    {
                        MessageBox.Show($"请检查{config.TemplateName}的数据源文件！", "错误",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 处理每个模板
                using (var package = new ExcelPackage(new FileInfo(templatePath)))
                {
                    foreach (var config in _dataSources)
                    {
                        ProcessSingleTemplate(package, config);
                    }
                    package.Save();
                }

                MessageBox.Show("所有模板处理完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void ProcessSingleTemplate(ExcelPackage package, DataSourceConfig config)
        {
            // 找到对应的模板Sheet
            var templateSheet = package.Workbook.Worksheets["模板_" + config.TemplateName];
            if (templateSheet == null)
            {
                throw new Exception($"找不到模板Sheet: {config.TemplateName}");
            }

            // 读取数据
            var countryData = _fileService.ReadDataFile(config.CountryTablePath);
            var productData = _fileService.ReadDataFile(config.ProductTablePath);
            var templateData = _fileService.ReadExcelSheet(package, templateSheet.Name);

            // 处理数据
            _reportProcessor.ProcessTables(countryData, productData, templateData);

            // 创建结果Sheet
            string resultSheetName = $"{DateTime.Today:yyyy-MM-dd}_{config.TemplateName}";
            _fileService.CreateResultSheet(package, templateSheet, templateData, resultSheetName);
        }

        //private void SetDefaultPaths()
        //{
        //    string dataSourcePath = @"C:\Users\Lei\Desktop\DataSource";
        //    Table1Path.Text = Path.Combine(dataSourcePath, "表格1.csv");
        //    Table2Path.Text = Path.Combine(dataSourcePath, "表格2.csv");
        //    Table3Path.Text = Path.Combine(dataSourcePath, "表格3.xlsx");
        //}

        //private void ProcessButton_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        if (!ValidateInputFiles()) return;

        //        var table1 = _fileService.ReadDataFile(Table1Path.Text);
        //        var table2 = _fileService.ReadDataFile(Table2Path.Text);
        //        var table3 = _fileService.ReadDataFile(Table3Path.Text);

        //        _reportProcessor.ProcessTables(table1, table2, table3);
        //        _fileService.SaveData(table3, Table3Path.Text);

        //        MessageBox.Show("处理完成！");
        //    }
        //    catch (Exception ex)
        //    {
        //        HandleError(ex);
        //    }
        //}

        //private bool ValidateInputFiles()
        //{
        //    if (!File.Exists(Table1Path.Text))
        //    {
        //        ShowError("表格1文件不存在！");
        //        return false;
        //    }

        //    if (!File.Exists(Table2Path.Text))
        //    {
        //        ShowError("表格2文件不存在！");
        //        return false;
        //    }

        //    return true;
        //}

        private void HandleError(Exception ex)
        {
            string errorDetails = $"[{DateTime.Now}] 错误\n消息：{ex.Message}\n类型：{ex.GetType()}\n堆栈：{ex.StackTrace}";

            MainWindow.LogMsg(errorDetails);
            Clipboard.SetText(errorDetails);

            MessageBox.Show($"处理过程中发生错误: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowError(string message) =>
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);

        static public void LogMsg(string msg)
        {

            System.Diagnostics.Debug.WriteLine(msg);
        }
    }
}