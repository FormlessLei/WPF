using JinReporter.Services;
using OfficeOpenXml;
using System;
using System.Diagnostics.PerformanceData;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Path = System.IO.Path;

namespace JinReporter
{
    public partial class MainWindow : Window
    {
        private readonly FileService _fileService;
        private readonly ReportProcessor _reportProcessor;

        public MainWindow()
        {
            InitializeComponent();

            _fileService = new FileService();
            _reportProcessor = new ReportProcessor();

            SetDefaultPaths();
        }

        private void SetDefaultPaths()
        {
            string dataSourcePath = @"C:\Users\Lei\Desktop\DataSource";
            Table1Path.Text = Path.Combine(dataSourcePath, "表格1.csv");
            Table2Path.Text = Path.Combine(dataSourcePath, "表格2.csv");
            Table3Path.Text = Path.Combine(dataSourcePath, "表格3.xlsx");
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInputFiles()) return;

                var table1 = _fileService.ReadDataFile(Table1Path.Text);
                var table2 = _fileService.ReadDataFile(Table2Path.Text);
                var table3 = _fileService.ReadDataFile(Table3Path.Text);

                _reportProcessor.ProcessTables(table1, table2, table3);
                _fileService.SaveData(table3, Table3Path.Text);

                MessageBox.Show("处理完成！");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private bool ValidateInputFiles()
        {
            if (!File.Exists(Table1Path.Text))
            {
                ShowError("表格1文件不存在！");
                return false;
            }

            if (!File.Exists(Table2Path.Text))
            {
                ShowError("表格2文件不存在！");
                return false;
            }

            return true;
        }

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