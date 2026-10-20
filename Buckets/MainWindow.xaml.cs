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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Buckets
{
    public partial class MainWindow : Window
    {
        // Размеры "виртуального" ведра внутри Viewbox (дальше всё масштабируется само).
        private const double ViewW = 120, ViewH = 190;
        private const double BucketW = 100, BucketH = 142, BucketTop = 24;

        private readonly List<string> _availableColors;
        private readonly List<Bucket> _buckets;
        private readonly BucketController _controller;
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _toastTimer;
        private readonly string _autosavePath;
        private string _currentConfigPath;
        private bool _syncingSliders;
        private bool _running;
        private bool _paused;
        private bool _dropInFlight;
        private int _dropToken;
        private AnimationClock _dropClock;
        private readonly Dictionary<int, BucketView> _views = new Dictionary<int, BucketView>();

        private sealed class BucketView
        {
            public Image Image;
            public TextBlock Amount;
            public Ellipse Drop;
            public TranslateTransform DropMove;
        }

        public MainWindow()
        {
            InitializeComponent();
            _availableColors = new List<string> { "black.png", "blue.png", "brown.png", "dark_blue.png", "dark_yellow.png", "green.png", "grey.png", "light_blue.png", "orange.png", "pink.png", "purple.png", "red.png", "turquoise.png", "yellow.png" };
            string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buckets");
            Directory.CreateDirectory(appData);
            _autosavePath = System.IO.Path.Combine(appData, "statt.ini");
            _currentConfigPath = _autosavePath;

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _toastTimer.Tick += delegate { _toastTimer.Stop(); ToastBorder.Visibility = Visibility.Collapsed; };

            BucketConfiguration config = LoadInitialConfiguration();
            SpeedSlider.Value = config.WaterSpeed;
            EmergencySlider.Value = config.EmergencyProbability;
            _buckets = new List<Bucket>();
            for (int i = 0; i < 10; i++) _buckets.Add(new Bucket(i, config.Colors[i], config.FillLevels[i]));
            _controller = new BucketController(_buckets);
            _controller.StateChanged += delegate { RenderBuckets(); };
            _controller.WaterSpeed = (int)SpeedSlider.Value;
            _controller.EmergencyProbability = (int)EmergencySlider.Value;

            BuildBucketViews();
            RenderBuckets();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(_controller.IntervalSeconds);
            _timer.Tick += Timer_Tick;
            UpdateIntervalText();
            UpdateButtons();
        }

        private BucketConfiguration LoadInitialConfiguration()
        {
            string bundledPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "statt.ini");
            string path = File.Exists(_autosavePath) ? _autosavePath : bundledPath;
            try { return IniConfigService.Load(path, _availableColors); }
            catch (Exception ex)
            {
                ShowToast("Не удалось загрузить настройки: " + ex.Message, false);
                var cfg = new BucketConfiguration();
                for (int i = 0; i < 10; i++) { cfg.Colors[i] = _availableColors[i]; cfg.FillLevels[i] = 0; }
                return cfg;
            }
        }

        // ---------- Отрисовка ----------

        private void BuildBucketViews()
        {
            BucketsPanel.Children.Clear();
            _views.Clear();
            foreach (Bucket bucket in _buckets)
            {
                var canvas = new Canvas { Width = ViewW, Height = ViewH, ClipToBounds = false };
                var title = new TextBlock { Text = "Ведро " + (bucket.Id + 1), FontWeight = FontWeights.SemiBold, FontSize = 14, Width = ViewW, TextAlignment = TextAlignment.Center };
                Canvas.SetTop(title, 0);

                var image = new Image { Width = BucketW, Height = BucketH, Stretch = Stretch.Uniform };
                Canvas.SetLeft(image, (ViewW - BucketW) / 2); Canvas.SetTop(image, BucketTop + 14);

                var amount = new TextBlock { FontSize = 13, Foreground = Brushes.DimGray, Width = ViewW, TextAlignment = TextAlignment.Center };
                Canvas.SetTop(amount, BucketTop + 14 + BucketH + 4);

                var move = new TranslateTransform();
                var drop = new Ellipse { Width = 12, Height = 17, Fill = Brushes.DeepSkyBlue, Stroke = Brushes.White, StrokeThickness = 0.6, Visibility = Visibility.Collapsed, IsHitTestVisible = false, RenderTransform = move };
                Canvas.SetLeft(drop, ViewW / 2 - 6); Canvas.SetTop(drop, BucketTop - 14);

                canvas.Children.Add(title);
                canvas.Children.Add(image);
                canvas.Children.Add(amount);
                canvas.Children.Add(drop);
                BucketsPanel.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(4), Child = canvas });
                _views[bucket.Id] = new BucketView { Image = image, Amount = amount, Drop = drop, DropMove = move };
            }
        }

        private void RenderBuckets()
        {
            foreach (Bucket bucket in _buckets)
            {
                BucketView v = _views[bucket.Id];
                string level = bucket.IsFull ? "full" : "half";
                v.Image.Source = new BitmapImage(new Uri("pack://application:,,,/resources/pictures/bucket/" + level + "/" + bucket.Color));
                v.Image.ToolTip = "Заполнение: " + bucket.WaterAmount + " л";
                v.Amount.Text = bucket.WaterAmount + " / " + Bucket.Capacity + " л";

            }
        }

        // ---------- Такт: выбрать ведро -> анимация капли -> +1 л -> лампочка ----------

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_dropInFlight) return;
            Bucket target = _controller.PickBucket();
            if (target == null) { FinishAllFull(); return; }
            StartDrop(target);
        }

        private void StartDrop(Bucket target)
        {
            BucketView v = _views[target.Id];
            int token = ++_dropToken;
            _dropInFlight = true;

            double ms = Math.Max(60, Math.Min(350, _controller.IntervalSeconds * 1000 * 0.8));
            var anim = new DoubleAnimation(0, BucketH * 0.55, TimeSpan.FromMilliseconds(ms)) { AccelerationRatio = 0.6 };
            _dropClock = anim.CreateClock();
            _dropClock.Completed += delegate
            {
                if (token != _dropToken) return; // остановлено кнопкой "Стоп"
                v.Drop.Visibility = Visibility.Collapsed;
                v.DropMove.ApplyAnimationClock(TranslateTransform.YProperty, null);
                _dropClock = null;
                _dropInFlight = false;

                _controller.CompleteDrop(target);       // +1 литр
                Bucket affected = _controller.IncidentLamp(); // авария считается после каждой капли
                if (affected != null) ShowToast("Из ведра " + (affected.Id + 1) + " вылит 1 л воды.", false);

                if (_controller.AreAllFull) FinishAllFull();
            };
            v.Drop.Visibility = Visibility.Visible;
            v.DropMove.ApplyAnimationClock(TranslateTransform.YProperty, _dropClock);
            if (_paused) _dropClock.Controller.Pause();
        }

        private void CancelDrop()
        {
            _dropToken++;
            _dropInFlight = false;
            _dropClock = null;
            foreach (BucketView v in _views.Values)
            {
                v.DropMove.ApplyAnimationClock(TranslateTransform.YProperty, null);
                v.Drop.Visibility = Visibility.Collapsed;
            }
        }

        private void FinishAllFull()
        {
            _timer.Stop();
            _running = false;
            _paused = false;
            UpdateButtons();
            ShowToast("Игра закончена", true);
        }

        // ---------- Тосты ----------

        private void ShowToast(string message, bool success)
        {
            if (ToastBorder == null || ToastText == null) return;
            _toastTimer.Stop();
            ToastText.Text = message;
            ToastBorder.Background = success ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) : new SolidColorBrush(Color.FromRgb(198, 40, 40));
            ToastBorder.Visibility = Visibility.Visible;
            _toastTimer.Start();
        }

        // ---------- Кнопки старт/стоп, пауза/продолжить ----------

        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                // СТОП: останавливаем процесс, пауза сбрасывается, вёдра остаются как есть
                _running = false;
                _paused = false;
                _timer.Stop();
                CancelDrop();
            }
            else
            {
                if (_controller.AreAllFull)
                {
                    ShowToast("Все вёдра уже заполнены. Измените наполненность в настройках.", false);
                    return;
                }
                // СТАРТ: если пауза была нажата заранее - процесс стартует в состоянии паузы
                _running = true;
                _timer.Interval = TimeSpan.FromSeconds(_controller.IntervalSeconds);
                if (!_paused) _timer.Start();
            }
            UpdateButtons();
        }

        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            _paused = !_paused; // можно нажимать всегда, в том числе до старта
            if (_running)
            {
                if (_paused)
                {
                    _timer.Stop();
                    if (_dropClock != null) _dropClock.Controller.Pause();
                }
                else
                {
                    if (_dropClock != null) _dropClock.Controller.Resume();
                    _timer.Interval = TimeSpan.FromSeconds(_controller.IntervalSeconds);
                    _timer.Start();
                }
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (StartStopButton == null || PauseResumeButton == null) return;
            StartStopButton.Content = _running ? "Стоп" : "Старт";
            PauseResumeButton.Content = _paused ? "Продолжить" : "Пауза";
        }

        // ---------- Ползунки и ввод чисел ----------

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingSliders || SpeedInput == null) return;
            _syncingSliders = true;
            SpeedInput.Text = ((int)Math.Round(SpeedSlider.Value)).ToString(CultureInfo.InvariantCulture);
            _syncingSliders = false;
            ApplySpeed();
        }

        private void EmergencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingSliders || EmergencyInput == null) return;
            _syncingSliders = true;
            EmergencyInput.Text = ((int)Math.Round(EmergencySlider.Value)).ToString(CultureInfo.InvariantCulture);
            _syncingSliders = false;
            if (_controller != null) _controller.EmergencyProbability = (int)Math.Round(EmergencySlider.Value);
        }

        private void SpeedInput_LostFocus(object sender, RoutedEventArgs e)
        {
            int value = NormalizeInput(SpeedInput.Text, (int)Math.Round(SpeedSlider.Value), "Скорость");
            SpeedInput.Text = value.ToString(CultureInfo.InvariantCulture);
            SpeedSlider.Value = value;
            ApplySpeed();
        }

        private void EmergencyInput_LostFocus(object sender, RoutedEventArgs e)
        {
            int value = NormalizeInput(EmergencyInput.Text, (int)Math.Round(EmergencySlider.Value), "Вероятность");
            EmergencyInput.Text = value.ToString(CultureInfo.InvariantCulture);
            EmergencySlider.Value = value;
            if (_controller != null) _controller.EmergencyProbability = value;
        }

        /// <summary>
        /// "qwerty" -> возвращаем прошлое значение (красный тост);
        /// "07"     -> конвертируем в 7;
        /// "123"    -> конвертируем в 100 (красный тост).
        /// </summary>
        private int NormalizeInput(string text, int previous, string what)
        {
            text = (text ?? "").Trim();
            if (!Regex.IsMatch(text, "^[0-9]+$"))
            {
                ShowToast(what + ": введите целое число от 0 до 100. Возвращено " + previous + ".", false);
                return previous;
            }
            string digits = text.TrimStart('0');
            if (digits.Length == 0) return 0;
            if (digits.Length > 3 || int.Parse(digits, CultureInfo.InvariantCulture) > 100)
            {
                ShowToast(what + ": значение не может быть больше 100. Установлено 100.", false);
                return 100;
            }
            return int.Parse(digits, CultureInfo.InvariantCulture);
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Keyboard.ClearFocus(); MainContent.Focus(); }
        }

        private void NumberValidation(object sender, TextCompositionEventArgs e) { e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$"); }

        private void ApplySpeed()
        {
            if (_controller == null) return;
            _controller.WaterSpeed = (int)Math.Round(SpeedSlider.Value);
            UpdateIntervalText();
            if (_timer != null && _running && !_paused) _timer.Interval = TimeSpan.FromSeconds(_controller.IntervalSeconds);
        }

        private void UpdateIntervalText()
        {
            if (IntervalText != null) IntervalText.Text = "Интервал: " + _controller.IntervalSeconds.ToString("0.00", CultureInfo.GetCultureInfo("ru-RU")) + " с";
        }

        // ---------- Файл ----------

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "INI-конфигурация (*.ini)|*.ini|Все файлы (*.*)|*.*", InitialDirectory = System.IO.Path.GetDirectoryName(_currentConfigPath) };
            if (dialog.ShowDialog() != true) return;
            try
            {
                BucketConfiguration config = IniConfigService.Load(dialog.FileName, _availableColors);
                for (int i = 0; i < 10; i++) { _buckets[i].SetColor(config.Colors[i]); _buckets[i].SetWaterAmount(config.FillLevels[i]); }
                _currentConfigPath = dialog.FileName;
                SpeedSlider.Value = config.WaterSpeed;
                EmergencySlider.Value = config.EmergencyProbability;
                _controller.WaterSpeed = config.WaterSpeed;
                _controller.EmergencyProbability = config.EmergencyProbability;
                ApplySpeed();
                RenderBuckets();
                ShowToast("Конфигурация загружена.", true);
            }
            catch (Exception ex) { ShowToast("Ошибка загрузки: " + ex.Message, false); }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "INI-конфигурация (*.ini)|*.ini", FileName = "statt.ini", InitialDirectory = System.IO.Path.GetDirectoryName(_currentConfigPath) };
            if (dialog.ShowDialog() != true) return;
            try
            {
                IniConfigService.Save(dialog.FileName, _buckets, (int)Math.Round(SpeedSlider.Value), (int)Math.Round(EmergencySlider.Value));
                _currentConfigPath = dialog.FileName;
                ShowToast("Конфигурация сохранена.", true);
            }
            catch (Exception ex) { ShowToast("Ошибка сохранения: " + ex.Message, false); }
        }

        // ---------- Настройки ----------

        private void Colors_Click(object sender, RoutedEventArgs e)
        {
            var window = new ColorSettingsWindow(_buckets, _availableColors) { Owner = this };
            if (window.ShowDialog() == true)
            {
                for (int i = 0; i < 10; i++) _buckets[i].SetColor(window.SelectedColors[i]);
                RenderBuckets();
            }
        }

        private void Fill_Click(object sender, RoutedEventArgs e)
        {
            var window = new BucketFillLevelSettingsWindow(_buckets) { Owner = this };
            if (window.ShowDialog() == true) _controller.SetFillLevels(window.FillLevels);
        }

        private void About_Click(object sender, RoutedEventArgs e) { new AboutProgramWindow { Owner = this }.ShowDialog(); }
        private void AboutAuthor_Click(object sender, RoutedEventArgs e) { new AboutAuthorWindow { Owner = this }.ShowDialog(); }
        private void Exit_Click(object sender, RoutedEventArgs e) { Close(); }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        // Закрытие только через "Выход" (меню / кнопка). Текущие настройки сохраняются в ini.
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            _toastTimer.Stop();
            try { IniConfigService.Save(_autosavePath, _buckets, (int)Math.Round(SpeedSlider.Value), (int)Math.Round(EmergencySlider.Value)); }
            catch (Exception ex) { MessageBox.Show("Не удалось сохранить настройки при выходе: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); }
            base.OnClosing(e);
        }
    }
}
