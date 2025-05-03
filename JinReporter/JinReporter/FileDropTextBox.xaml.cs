using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JinReporter
{
    public partial class FileDropTextBox : UserControl
    {
        // 定义依赖属性
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(FileDropTextBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (FileDropTextBox)d;
            control.PathTextBox.Text = e.NewValue?.ToString();
        }

        public FileDropTextBox()
        {
            InitializeComponent();
            PathTextBox.AllowDrop = true;
            PathTextBox.PreviewDragOver += PathTextBox_PreviewDragOver;
            PathTextBox.Drop += PathTextBox_Drop;
            PathTextBox.TextChanged += PathTextBox_TextChanged;
        }

        //public string Text
        //{
        //    get => PathTextBox.Text;
        //    set => PathTextBox.Text = value;
        //}
        private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 更新依赖属性值
            Text = PathTextBox.Text;
        }

        private void PathTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ?
                DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void PathTextBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    PathTextBox.Text = files[0];
                }
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                PathTextBox.Text = openFileDialog.FileName;
            }
        }
    }
}
