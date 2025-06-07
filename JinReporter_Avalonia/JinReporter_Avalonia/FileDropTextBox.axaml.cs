using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Linq;

namespace JinReporter_Avalonia;

public partial class FileDropTextBox : UserControl
{
    // 事件定义（与 WPF 相同）
    public event Action<FileDropTextBox, int>? FileDropped;

    // Avalonia 的依赖属性定义
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FileDropTextBox, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FileDropTextBox()
    {
        InitializeComponent();


        PathTextBox.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        PathTextBox.AddHandler(DragDrop.DropEvent, OnDrop);

        PathTextBox.TextChanged += PathTextBox_TextChanged;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        // 检查是否是文件拖放
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles()?.ToList();
            if (files?.Count > 0)
            {
                Text = files[0].Path.LocalPath; // 更新文本
                FileDropped?.Invoke(this, files.Count); // 触发事件
            }
        }
    }

    private void PathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // 同步到依赖属性
        Text = PathTextBox.Text;
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        // Avalonia 的文件对话框
        var dialog = new OpenFileDialog();
        var result = await dialog.ShowAsync((Window)this.VisualRoot);
        if (result?.Length > 0)
        {
            PathTextBox.Text = result[0];
        }
    }

    // 属性变更回调（可选）
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            PathTextBox.Text = change.NewValue?.ToString();
        }
    }
}
