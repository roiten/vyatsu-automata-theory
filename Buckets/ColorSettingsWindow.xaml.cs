using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Buckets
{
    public partial class ColorSettingsWindow : Window
    {
        private readonly ObservableCollection<ColorRow> _rows = new ObservableCollection<ColorRow>();
        private readonly DispatcherTimer _toastTimer;
        private readonly string[] _initial = new string[10];
        private readonly List<ColorOption> _options;
        private readonly Random _random = new Random();
        public string[] SelectedColors { get; private set; }

        public ColorSettingsWindow(IList<Bucket> buckets, IList<string> availableColors)
        {
            InitializeComponent();
            _options = availableColors.Select(CreateOption).ToList();
            for (int i = 0; i < 10; i++)
            {
                _initial[i] = buckets[i].Color;
                var row = new ColorRow { Id = i, Label = "Ведро " + (i + 1), AvailableColors = _options };
                row.IsTaken = IsTaken;
                row.Rejected = OnRejected;
                row.Init(buckets[i].Color);
                _rows.Add(row);
            }
            ColorRows.ItemsSource = _rows;
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _toastTimer.Tick += delegate { _toastTimer.Stop(); ToastBorder.Visibility = Visibility.Collapsed; };
        }

        private bool IsTaken(ColorRow self, string color)
        {
            ColorRow other = _rows.FirstOrDefault(r => r != self && String.Equals(r.SelectedColor, color, StringComparison.OrdinalIgnoreCase));
            if (other == null) return false;
            ShowToast("Этот цвет уже назначен ведру №" + (other.Id + 1) + ". Выберите другой.");
            return true;
        }

        private void OnRejected(string message) { ShowToast(message); }

        private static ColorOption CreateOption(string fileName)
        {
            string key = System.IO.Path.GetFileNameWithoutExtension(fileName);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "black", "Чёрный" }, { "blue", "Синий" }, { "brown", "Коричневый" },
                { "dark_blue", "Тёмно-синий" }, { "dark_yellow", "Тёмно-жёлтый" }, { "green", "Зелёный" },
                { "grey", "Серый" }, { "light_blue", "Голубой" }, { "orange", "Оранжевый" },
                { "pink", "Розовый" }, { "purple", "Фиолетовый" }, { "red", "Красный" },
                { "turquoise", "Бирюзовый" }, { "yellow", "Жёлтый" }
            };
            var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "black", "#222222" }, { "blue", "#1976D2" }, { "brown", "#795548" },
                { "dark_blue", "#283593" }, { "dark_yellow", "#C0A000" }, { "green", "#388E3C" },
                { "grey", "#9E9E9E" }, { "light_blue", "#81D4FA" }, { "orange", "#F57C00" },
                { "pink", "#EC407A" }, { "purple", "#8E24AA" }, { "red", "#D32F2F" },
                { "turquoise", "#26A69A" }, { "yellow", "#FDD835" }
            };
            string display, hex;
            if (!names.TryGetValue(key, out display)) display = key;
            if (!colors.TryGetValue(key, out hex)) hex = "#808080";
            return new ColorOption { FileName = fileName, DisplayName = display, PreviewBrush = (Brush)new BrushConverter().ConvertFromString(hex) };
        }

        // Сгенерировать цвета всем вёдрам (все разные).
        private void RandomAll_Click(object sender, RoutedEventArgs e)
        {
            List<string> shuffled = _options.Select(c => c.FileName).OrderBy(x => _random.Next()).Take(10).ToList();
            for (int i = 0; i < 10; i++) _rows[i].Init(shuffled[i]);
        }

        // Сброс случайного: вернуть цвета, которые были при открытии окна.
        private void ResetRandom_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 10; i++) _rows[i].Init(_initial[i]);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Any(r => String.IsNullOrWhiteSpace(r.SelectedColor)) || _rows.Select(r => r.SelectedColor).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 10)
            {
                ShowToast("Назначьте всем 10 вёдрам разные цвета.");
                return;
            }
            SelectedColors = _rows.OrderBy(r => r.Id).Select(r => r.SelectedColor).ToArray();
            _toastTimer.Stop();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { _toastTimer.Stop(); DialogResult = false; }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is Grid) DragMove();
        }

        private void ShowToast(string message)
        {
            _toastTimer.Stop();
            ToastText.Text = message;
            ToastBorder.Visibility = Visibility.Visible;
            _toastTimer.Start();
        }

        private sealed class ColorRow : INotifyPropertyChanged
        {
            private string _selected;
            public int Id { get; set; }
            public string Label { get; set; }
            public List<ColorOption> AvailableColors { get; set; }
            public Func<ColorRow, string, bool> IsTaken;
            public Action<string> Rejected;
            public event PropertyChangedEventHandler PropertyChanged;

            public BitmapImage Preview
            {
                get
                {
                    if (String.IsNullOrEmpty(_selected)) return null;
                    return new BitmapImage(new Uri("pack://application:,,,/resources/pictures/bucket/half/" + _selected));
                }
            }

            // Установка без проверки (генерация / начальные значения).
            public void Init(string color) { _selected = color; Raise("SelectedColor"); Raise("Preview"); }

            // Установка из выпадающего списка: дубликаты отклоняются, возвращается прошлый цвет.
            public string SelectedColor
            {
                get { return _selected; }
                set
                {
                    if (value == null || String.Equals(value, _selected, StringComparison.OrdinalIgnoreCase)) return;
                    if (IsTaken != null && IsTaken(this, value))
                    {
                        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate { Raise("SelectedColor"); }), DispatcherPriority.Background);
                        return;
                    }
                    _selected = value;
                    Raise("SelectedColor");
                    Raise("Preview");
                }
            }

            private void Raise(string name)
            {
                PropertyChangedEventHandler h = PropertyChanged;
                if (h != null) h(this, new PropertyChangedEventArgs(name));
            }
        }

        private sealed class ColorOption
        {
            public string FileName { get; set; }
            public string DisplayName { get; set; }
            public Brush PreviewBrush { get; set; }
        }
    }
}
