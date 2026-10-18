using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Buckets
{
    public partial class MainWindow : Window
    {
        private readonly List<string> _availableColors;
        private readonly List<Bucket> _buckets;
        private BucketController _controller;
        private readonly DispatcherTimer _timer;
        private string _currentConfigPath;
        private bool _syncingSliders;
        private readonly Dictionary<int, StackPanel> _bucketViews = new Dictionary<int, StackPanel>();

        public MainWindow()
        {
            InitializeComponent();
            _availableColors = new List<string> { "black.png", "blue.png", "brown.png", "dark_blue.png", "dark_yellow.png", "green.png", "grey.png", "light_blue.png", "orange.png", "pink.png", "purple.png", "red.png", "turquoise.png", "yellow.png" };
            BucketConfiguration config = LoadDefaultConfiguration();
            _buckets = new List<Bucket>();
            for (int i = 0; i < 10; i++) _buckets.Add(new Bucket(i, config.Colors[i], config.FillLevels[i]));
            _controller = new BucketController(_buckets);
            _controller.StateChanged += Controller_StateChanged;
            _controller.Message += Controller_Message;
            _controller.WaterSpeed = (int)SpeedSlider.Value;
            _controller.EmergencyProbability = (int)EmergencySlider.Value;
            BuildBucketViews();
            RenderBuckets();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(_controller.IntervalSeconds);
            _timer.Tick += Timer_Tick;
            _currentConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "statt.ini");
            UpdateIntervalText();
        }

        private BucketConfiguration LoadDefaultConfiguration()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "statt.ini");
            try { return IniConfigService.Load(path, _availableColors); }
            catch (Exception ex)
            {
                StatusText.Text = "Не удалось загрузить INI: " + ex.Message;
                var cfg = new BucketConfiguration();
                for (int i = 0; i < 10; i++) { cfg.Colors[i] = _availableColors[i]; cfg.FillLevels[i] = 0; }
                return cfg;
            }
        }

        private void BuildBucketViews()
        {
            BucketsPanel.Children.Clear(); _bucketViews.Clear();
            foreach (Bucket bucket in _buckets)
            {
                var panel = new StackPanel { Margin = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var image = new Image { Width = 90, Height = 115, Stretch = Stretch.Uniform, Tag = bucket.Id };
                var title = new TextBlock { Text = "Ведро " + bucket.Id, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
                var level = new ProgressBar { Width = 76, Height = 8, Minimum = 0, Maximum = 10, Margin = new Thickness(0, 3, 0, 2) };
                var amount = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.DimGray };
                panel.Children.Add(image); panel.Children.Add(title); panel.Children.Add(level); panel.Children.Add(amount);
                BucketsPanel.Children.Add(panel); _bucketViews[bucket.Id] = panel;
            }
        }

        private void RenderBuckets()
        {
            foreach (Bucket bucket in _buckets)
            {
                StackPanel panel = _bucketViews[bucket.Id];
                panel.Visibility = bucket.IsActive ? Visibility.Visible : Visibility.Collapsed;
                Image image = (Image)panel.Children[0];
                string level = bucket.WaterAmount == 10 ? "full" : "half";
                string uri = "/resources/pictures/bucket/" + level + "/" + bucket.Color;
                image.Source = new BitmapImage(new Uri("pack://application:,,," + uri));
                ((ProgressBar)panel.Children[2]).Value = bucket.WaterAmount;
                ((TextBlock)panel.Children[3]).Text = bucket.WaterAmount + " / 10 л";
                image.ToolTip = bucket.IsActive ? ("Заполнение: " + bucket.WaterAmount + " л") : "Ведро временно убрано";
            }
        }

        private void Timer_Tick(object sender, EventArgs e) { _controller.ExecuteTick(); }
        private void Controller_StateChanged(object sender, EventArgs e) { RenderBuckets(); }
        private void Controller_Message(string message) { StatusText.Text = message; }

        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled) { _timer.Stop(); StartStopButton.Content = "Пуск"; PauseResumeButton.IsEnabled = false; StatusText.Text = "Игра остановлена."; }
            else { _timer.Interval = TimeSpan.FromSeconds(_controller.IntervalSeconds); _timer.Start(); StartStopButton.Content = "Стоп"; PauseResumeButton.IsEnabled = true; StatusText.Text = "Игра запущена."; }
        }
        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled) { _timer.Stop(); PauseResumeButton.Content = "Продолжить"; StatusText.Text = "Пауза."; }
            else { _timer.Interval = TimeSpan.FromSeconds(_controller.IntervalSeconds); _timer.Start(); PauseResumeButton.Content = "Пауза"; StatusText.Text = "Игра продолжается."; }
        }
        private void Reset_Click(object sender, RoutedEventArgs e) { _timer.Stop(); StartStopButton.Content = "Пуск"; PauseResumeButton.Content = "Пауза"; PauseResumeButton.IsEnabled = false; _controller.Reset(); StatusText.Text = "Все вёдра сброшены."; }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingSliders || SpeedInput == null) return;
            _syncingSliders = true; SpeedInput.Text = ((int)SpeedSlider.Value).ToString(CultureInfo.InvariantCulture); _syncingSliders = false;
            ApplySpeed();
        }
        private void EmergencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingSliders || EmergencyInput == null) return;
            _syncingSliders = true; EmergencyInput.Text = ((int)EmergencySlider.Value).ToString(CultureInfo.InvariantCulture); _syncingSliders = false;
            if (_controller != null) _controller.EmergencyProbability = (int)EmergencySlider.Value;
        }
        private void SpeedInput_LostFocus(object sender, RoutedEventArgs e)
        {
            int value; if (!TryReadInput(SpeedInput.Text, out value)) { SpeedInput.Text = ((int)SpeedSlider.Value).ToString(); StatusText.Text = "Введите целое число от 0 до 100 без ведущих нулей."; return; }
            SpeedSlider.Value = value; ApplySpeed();
        }
        private void EmergencyInput_LostFocus(object sender, RoutedEventArgs e)
        {
            int value; if (!TryReadInput(EmergencyInput.Text, out value)) { EmergencyInput.Text = ((int)EmergencySlider.Value).ToString(); StatusText.Text = "Введите целое число от 0 до 100 без ведущих нулей."; return; }
            EmergencySlider.Value = value; if (_controller != null) _controller.EmergencyProbability = value;
        }
        private bool TryReadInput(string text, out int value)
        {
            value = 0;
            return Regex.IsMatch(text ?? "", "^(0|[1-9][0-9]?)$") && int.TryParse(text, out value) && value >= 0 && value <= 100;
        }
        private void NumberValidation(object sender, TextCompositionEventArgs e) { e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$"); }
        private void ApplySpeed()
        {
            if (_controller == null) return;
            _controller.WaterSpeed = (int)SpeedSlider.Value;
            UpdateIntervalText();
            if (_timer != null && _timer.IsEnabled) _timer.Interval = TimeSpan.FromSeconds(_controller.IntervalSeconds);
        }
        private void UpdateIntervalText() { if (IntervalText != null) IntervalText.Text = "Интервал: " + _controller.IntervalSeconds.ToString("0.00", CultureInfo.GetCultureInfo("ru-RU")) + " с"; }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "INI-конфигурация (*.ini)|*.ini|Все файлы (*.*)|*.*", InitialDirectory = Path.GetDirectoryName(_currentConfigPath) };
            if (dialog.ShowDialog() != true) return;
            try
            {
                BucketConfiguration config = IniConfigService.Load(dialog.FileName, _availableColors);
                for (int i = 0; i < 10; i++) { _buckets[i].SetColor(config.Colors[i]); _buckets[i].SetWaterAmount(config.FillLevels[i]); _buckets[i].RestoreActivePreservingWater(); }
                _currentConfigPath = dialog.FileName; RenderBuckets(); StatusText.Text = "Конфигурация загружена: " + Path.GetFileName(_currentConfigPath);
            }
            catch (Exception ex) { StatusText.Text = "Ошибка загрузки: " + ex.Message; MessageBox.Show(this, ex.Message, "Ошибка конфигурации", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try { IniConfigService.Save(_currentConfigPath, _buckets); StatusText.Text = "Конфигурация сохранена."; }
            catch (Exception ex) { StatusText.Text = "Ошибка сохранения."; MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void SaveAsConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "INI-конфигурация (*.ini)|*.ini", FileName = "statt.ini" };
            if (dialog.ShowDialog() == true) { _currentConfigPath = dialog.FileName; SaveConfig_Click(sender, e); }
        }
        private void Colors_Click(object sender, RoutedEventArgs e)
        {
            var window = new ColorSettingsWindow(_buckets, _availableColors) { Owner = this };
            if (window.ShowDialog() == true) { for (int i = 0; i < 10; i++) _buckets[i].SetColor(window.SelectedColors[i]); RenderBuckets(); StatusText.Text = "Цвета обновлены."; }
        }
        private void Fill_Click(object sender, RoutedEventArgs e)
        {
            var window = new BucketFillLevelSettingsWindow(_buckets) { Owner = this };
            if (window.ShowDialog() == true) { for (int i = 0; i < 10; i++) { _buckets[i].SetWaterAmount(window.FillLevels[i]); _buckets[i].RestoreActivePreservingWater(); } RenderBuckets(); StatusText.Text = "Заполнение обновлено."; }
        }
        private void RandomFill_Click(object sender, RoutedEventArgs e)
        {
            var random = new Random(); foreach (Bucket bucket in _buckets) { bucket.SetWaterAmount(random.Next(0, 11)); bucket.RestoreActivePreservingWater(); }
            RenderBuckets(); StatusText.Text = "Заполнение сгенерировано случайно.";
        }
        private void About_Click(object sender, RoutedEventArgs e) { new AboutProgramWindow { Owner = this }.ShowDialog(); }
        private void Author_Click(object sender, RoutedEventArgs e) { new AboutAuthorWindow { Owner = this }.ShowDialog(); }
        private void Exit_Click(object sender, RoutedEventArgs e) { Close(); }
    }
}
