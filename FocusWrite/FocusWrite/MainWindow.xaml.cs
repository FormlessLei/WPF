using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FocusWrite
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer; // 定时器，用于实时更新统计信息
        private DateTime lastInputTime; // 记录最后一次输入的时间
        private int wordCount = 0; // 记录当前字数
        private int sessionWordCount = 0; // 记录本次字数
        private int idleTime = 0; // 记录空闲时间
        private int typingTime = 0; // 记录打字时间
        private bool isTyping = false; // 标记用户是否正在输入
        private bool isSpeedPerHour = false; // 标记是否以小时为单位显示输入速度
        private bool isStarted = false; // 标记用户是否已经开始打字
        private bool isSessionWordCountVisible = true; // 标记是否显示本次字数
        private int initialWordCount = 0; // 记录加载文件时的初始字数

        public MainWindow()
        {
            InitializeComponent();
            InitializeTimer(); // 初始化定时器
        }

        // 初始化定时器
        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1); // 每秒触发一次
            timer.Tick += Timer_Tick;
            timer.Start();
            lastInputTime = DateTime.Now; // 初始化最后一次输入时间
        }

        // 定时器触发事件
        private void Timer_Tick(object sender, EventArgs e)
        {
            var currentTime = DateTime.Now;
            var elapsedSinceLastInput = (currentTime - lastInputTime).TotalSeconds;

            // 如果用户已经开始打字
            if (isStarted)
            {
                // 如果用户超过1秒没有输入，则认为是空闲状态
                if (elapsedSinceLastInput > 1)
                {
                    isTyping = false; // 用户空闲
                    idleTime += 1; // 空闲时间增加
                }
                else
                {
                    // 用户正在输入，增加打字时间
                    if (isTyping)
                    {
                        typingTime += 1; // 打字时间增加
                    }
                }
            }

            // 更新界面
            TypingTimeText.Text = $"打字时间: {typingTime} 秒";
            IdleTimeText.Text = $"空闲时间: {idleTime} 秒";

            // 计算输入速度（使用本次字数）
            if (isSpeedPerHour)
            {
                var typingSpeedPerHour = (typingTime > 0) ? sessionWordCount / (typingTime / 3600.0) : 0;
                TypingSpeedText.Text = $"输入速度: {typingSpeedPerHour:F0} 字/小时";
            }
            else
            {
                var typingSpeedPerMinute = (typingTime > 0) ? sessionWordCount / (typingTime / 60.0) : 0;
                TypingSpeedText.Text = $"输入速度: {typingSpeedPerMinute:F0} 字/分钟";
            }
        }

        // 初始事件处理程序（包含 isStarted 逻辑）
        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isStarted)
            {
                isStarted = true; // 用户已经开始打字
                InputTextBox.TextChanged -= InputTextBox_TextChanged; // 移除初始事件处理程序
                InputTextBox.TextChanged += InputTextBox_TextChanged_Normal; // 添加正常事件处理程序
            }

            wordCount = InputTextBox.Text.Length; // 更新总字数
            UpdateSessionWordCount(); // 更新本次字数
            UpdateWordCountDisplay(); // 更新字数显示
            lastInputTime = DateTime.Now; // 更新最后一次输入时间
            isTyping = true; // 标记用户正在输入
        }

        // 正常事件处理程序（不包含 isStarted 逻辑）
        private void InputTextBox_TextChanged_Normal(object sender, TextChangedEventArgs e)
        {
            wordCount = InputTextBox.Text.Length; // 更新总字数
            UpdateSessionWordCount(); // 更新本次字数
            UpdateWordCountDisplay(); // 更新字数显示
            lastInputTime = DateTime.Now; // 更新最后一次输入时间
            isTyping = true; // 标记用户正在输入
        }

        // 更新本次字数
        private void UpdateSessionWordCount()
        {
            if (wordCount >= initialWordCount)
            {
                sessionWordCount = wordCount - initialWordCount; // 本次字数 = 总字数 - 初始字数
            }
            else
            {
                sessionWordCount = 0; // 如果总字数少于初始字数，本次字数为 0
                initialWordCount = wordCount; // 重置初始字数，以便后续输入从 0 开始累加
            }
        }

        // 更新字数显示
        private void UpdateWordCountDisplay()
        {
            if (isSessionWordCountVisible)
            {
                SessionWordCountText.Text = $"本次字数: {sessionWordCount}";
            }
            else
            {
                SessionWordCountText.Text = $"总字数: {wordCount}";
            }
        }

        // 保存按钮点击事件
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "文本文件 (*.txt)|*.txt"; // 文件过滤器
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, InputTextBox.Text); // 保存文本内容
            }
        }

        // 加载按钮点击事件
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "文本文件 (*.txt)|*.txt"; // 文件过滤器
            if (openFileDialog.ShowDialog() == true)
            {
                // 加载文件内容
                InputTextBox.Text = File.ReadAllText(openFileDialog.FileName);

                // 重置统计状态
                ResetStatistics();

                // 更新字数显示
                wordCount = InputTextBox.Text.Length; // 更新总字数
                initialWordCount = wordCount; // 记录初始字数
                sessionWordCount = 0; // 重置本次字数
                UpdateWordCountDisplay();
            }
        }

        // 重置统计状态
        private void ResetStatistics()
        {
            isStarted = false; // 重置为未开始状态
            wordCount = 0; // 重置总字数
            initialWordCount = 0; // 重置初始字数
            sessionWordCount = 0; // 重置本次字数
            idleTime = 0; // 重置空闲时间
            typingTime = 0; // 重置打字时间
            isTyping = false; // 重置输入状态

            // 更新界面显示
            TypingTimeText.Text = $"打字时间: {typingTime} 秒";
            IdleTimeText.Text = $"空闲时间: {idleTime} 秒";
            TypingSpeedText.Text = $"输入速度: 0 字/分钟";
            UpdateWordCountDisplay();

            // 重新绑定初始事件处理程序
            InputTextBox.TextChanged -= InputTextBox_TextChanged_Normal; // 移除正常事件处理程序
            InputTextBox.TextChanged += InputTextBox_TextChanged; // 重新绑定初始事件处理程序
        }

        // 输入速度文本点击事件
        private void TypingSpeedText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isSpeedPerHour = !isSpeedPerHour; // 切换单位
        }

        // 本次字数文本点击事件
        private void SessionWordCountText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isSessionWordCountVisible = !isSessionWordCountVisible; // 切换显示模式
            UpdateWordCountDisplay(); // 更新字数显示
        }
    }
}