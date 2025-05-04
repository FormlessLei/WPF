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
using System.Windows.Threading;
using System.Windows.Media;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Media.Effects;

namespace JinReporter
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<DataSourceConfig> _dataSources;
        private readonly FileService _fileService;
        private readonly ReportProcessor _reportProcessor;
        private List<TemplateInfo> _detectedTemplates;

        private List<FileDropTextBox> _fileInputs = new List<FileDropTextBox>();
        private int _currentFillIndex = 0;


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

                UpdateStatus($"就绪 | 检测到 {_detectedTemplates.Count} 个模板");

                // 2. 显示拖放区（在确认模板后激活）
                DropZone.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            //var files = ((string[])e.Data.GetData(DataFormats.FileDrop))
            //    .OrderBy(f => f) // 按文件名排序
            //    .ToArray();

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // 获取所有有效输入框
            var inputs = GetActiveInputs();

            // 智能分配（一对一填充）
            for (int i = 0; i < Math.Min(files.Length, inputs.Count); i++)
            {
                inputs[i].Text = files[i];
            }

            ResetDropZoneAppearance();
            // 自动隐藏（2秒后）
            //var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            //timer.Tick += (s, _) => { DropZone.Visibility = Visibility.Collapsed; timer.Stop(); };
            //timer.Start();
        }
        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 使用更柔和的视觉反馈
                DropZone.Background = new SolidColorBrush(Color.FromArgb(255, 220, 240, 255));
                DropZone.BorderBrush = Brushes.SteelBlue;
                DropZone.Effect = new DropShadowEffect
                {
                    Color = Colors.SteelBlue,
                    BlurRadius = 8,
                    ShadowDepth = 0
                };
            }
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            ResetDropZoneAppearance();
        }

        private void ResetDropZoneAppearance()
        {
            // 使用资源颜色保证主题一致性
            DropZone.Background = (Brush)FindResource("DropZoneBackground");
            DropZone.BorderBrush = (Brush)FindResource("DropZoneBorder");
            DropZone.Effect = null;
        }

        private List<TextBox> GetActiveInputs()
        {
            var inputs = new List<TextBox>();
            foreach (var item in DataSourceControls.Items)
            {
                var container = DataSourceControls.ItemContainerGenerator.ContainerFromItem(item);
                if (container != null)
                {
                    inputs.AddRange(FindVisualChildren<TextBox>(container)
                        .Where(t => t.IsVisible));
                }
            }
            return inputs;
        }

        // 辅助方法：查找所有子控件
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
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

                MessageBoxResult result = MessageBox.Show("处理完成，是否打开模板文件?", "打开", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = TemplateFileBox.Text,
                        UseShellExecute = true
                    });
                }


                UpdateStatus("所有模板处理完成！");
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

        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;

                StatusText.Foreground = message.StartsWith("错误") ? Brushes.Red : Brushes.DarkBlue;

                //// 自动淡出非错误消息
                //if (!message.StartsWith("错误"))
                //{
                //    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                //    timer.Tick += (s, e) => { StatusText.Text = ""; timer.Stop(); };
                //    timer.Start();
                //}
            });
        }
    }
}