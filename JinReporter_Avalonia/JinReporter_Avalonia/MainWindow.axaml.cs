using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ExcelDataReader;
using JinReporter_Avalonia.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.VisualTree;
using System.Threading.Tasks;

namespace JinReporter_Avalonia;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DataSourceConfig> _dataSources;
    private readonly FileService _fileService;
    private readonly ReportProcessor _reportProcessor;
    private List<TemplateInfo> _detectedTemplates;

    public MainWindow()
    {
        InitializeComponent();

        ConfirmButton.Click += ConfirmTemplate_Click;
        ProcessButton.Click += ProcessButton_Click;


        _fileService = new FileService();
        _reportProcessor = new ReportProcessor();
        _dataSources = new ObservableCollection<DataSourceConfig>();
        _detectedTemplates = new List<TemplateInfo>();

        DataSourceControls.ItemsSource = _dataSources;
    }

    private void ConfirmTemplate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string templatePath = TemplateFileBox.Text;
            if (!File.Exists(templatePath))
            {

                this.ShowMessageBox("模板文件不存在！", "错误"/*, MessageBoxButton.OK, MessageBoxImage.Error*/);
                return;
            }

            // 检测模板Sheet
            _detectedTemplates = DetectTemplates(templatePath);
            if (_detectedTemplates.Count == 0)
            {
                this.ShowMessageBox("未找到有效的模板Sheet！", "错误"/*, MessageBoxButton.OK, MessageBoxImage.Error*/);
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
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    private async void ProcessButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string templatePath = TemplateFileBox.Text;

            // 验证所有数据源文件
            foreach (var config in _dataSources)
            {
                if (!File.Exists(config.CountryTablePath) || !File.Exists(config.ProductTablePath))
                {
                    this.ShowMessageBox($"请检查{config.TemplateName}的数据源文件！", "错误");
                    return;
                }
            }

            var outputPath = _fileService.ProcessAndSaveResults(_dataSources.ToList(), TemplateFileBox.Text);


            MessageBoxResult result =await this.ShowMessageBoxAsync("处理完成，是否打开结果?", "打开");

            if (result == MessageBoxResult.Yes)
            {
                // 打开结果文件
                Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            }

            UpdateStatus("所有模板处理完成！");
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    private List<TemplateInfo> DetectTemplates(string filePath)
    {
        var templates = new List<TemplateInfo>();

        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

    private void UpdateStatus(string message)
    {
        Dispatcher.UIThread.Invoke(() =>
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

    private void HandleError(Exception ex)
    {
        this.ShowMessageBox($"处理过程中发生错误: {ex.Message}", "错误");
    }

    static public void LogMsg(string msg)
    {

        System.Diagnostics.Debug.WriteLine(msg);
    }
}