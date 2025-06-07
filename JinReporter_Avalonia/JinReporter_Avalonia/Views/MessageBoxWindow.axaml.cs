using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace JinReporter_Avalonia.Views
{
    public partial class MessageBoxWindow : Window
    {
        public string TitleText { get; }
        public string Message { get; }

        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        public MessageBoxWindow(string title, string message, MessageBoxButtons buttons) : this()
        {
            InitializeComponent();

            TitleText = title;
            Message = message;
            DataContext = this;

            if (buttons == MessageBoxButtons.Ok)
            {
                // ֻ��ʾȷ�ϰ�ť
                YesButton.Content = "ȷ��";
                NoButton.IsVisible = false;
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                YesButton.Content = "��";
                NoButton.Content = "��";
                NoButton.IsVisible = true;
            }

            YesButton.Click += OnYesClicked;
            NoButton.Click += OnNoClicked;
        }


        private void OnYesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnNoClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }
    }

    public enum MessageBoxButtons
    {
        Ok,         // ֻ��ȷ�ϰ�ť
        YesNo       // ��/��������ť
    }
}
