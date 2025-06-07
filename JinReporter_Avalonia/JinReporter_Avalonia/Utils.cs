using Avalonia.Controls;
using System.Threading.Tasks;
using JinReporter_Avalonia.Views;

namespace JinReporter_Avalonia
{
    public static class MessageBoxExtensions
    {
        public static void ShowMessageBox(this Window parent, string message, string title)
        {
            var box = new MessageBoxWindow(title, message, MessageBoxButtons.Ok);
            box.ShowDialog(parent);
        }

        public static async Task<MessageBoxResult> ShowMessageBoxAsync(this Window parent, string message, string title)
        {
            var box = new MessageBoxWindow(title, message, MessageBoxButtons.YesNo);
            var result = await box.ShowDialog<bool>(parent); // 假设返回 true = Yes，false = No

            return result ? MessageBoxResult.Yes : MessageBoxResult.No;
        }
    }

    public enum MessageBoxResult
    {
        Yes,
        No
    }
}
