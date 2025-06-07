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
                // 只显示确认按钮
                YesButton.Content = "确认";
                NoButton.IsVisible = false;
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                YesButton.Content = "是";
                NoButton.Content = "否";
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
        Ok,         // 只有确认按钮
        YesNo       // 是/否两个按钮
    }
}
