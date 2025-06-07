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
    // �¼����壨�� WPF ��ͬ��
    public event Action<FileDropTextBox, int>? FileDropped;

    // Avalonia ���������Զ���
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
        // ����Ƿ����ļ��Ϸ�
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
                Text = files[0].Path.LocalPath; // �����ı�
                FileDropped?.Invoke(this, files.Count); // �����¼�
            }
        }
    }

    private void PathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // ͬ������������
        Text = PathTextBox.Text;
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        // Avalonia ���ļ��Ի���
        var dialog = new OpenFileDialog();
        var result = await dialog.ShowAsync((Window)this.VisualRoot);
        if (result?.Length > 0)
        {
            PathTextBox.Text = result[0];
        }
    }

    // ���Ա���ص�����ѡ��
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            PathTextBox.Text = change.NewValue?.ToString();
        }
    }
}
