using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Buckets
{
    public partial class BucketFillLevelSettingsWindow : Window
    {
        private readonly ObservableCollection<FillRow> _rows = new ObservableCollection<FillRow>();
        private readonly DispatcherTimer _toastTimer;
        private readonly int[] _initial = new int[10];
        private readonly Random _random = new Random();
        public int[] FillLevels { get; private set; }

        public BucketFillLevelSettingsWindow(IList<Bucket> buckets)
        {
            InitializeComponent();
            for (int i = 0; i < 10; i++)
            {
                _initial[i] = buckets[i].WaterAmount;
                _rows.Add(new FillRow { Id = i, Label = "Ведро " + (i + 1), Amount = buckets[i].WaterAmount.ToString(CultureInfo.InvariantCulture) });
            }
            FillRows.ItemsSource = _rows;
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _toastTimer.Tick += delegate { _toastTimer.Stop(); ToastBorder.Visibility = Visibility.Collapsed; };
        }

        private void Increment_Click(object sender, RoutedEventArgs e) { ChangeAmount(sender, 1); }
        private void Decrement_Click(object sender, RoutedEventArgs e) { ChangeAmount(sender, -1); }

        private void ChangeAmount(object sender, int delta)
        {
            ChangeAmount((int)((FrameworkElement)sender).Tag, delta);
        }

        private void ChangeAmount(int id, int delta)
        {
            int value;
            if (!TryAmount(_rows[id].Amount, out value)) value = _initial[id];
            _rows[id].Amount = Math.Max(0, Math.Min(Bucket.Capacity, value + delta)).ToString(CultureInfo.InvariantCulture);
        }

        private void Amount_KeyDown(object sender, KeyEventArgs e)
        {
            FillRow row = ((FrameworkElement)sender).DataContext as FillRow;
            if (row == null) return;
            if (e.Key == Key.Up) { ChangeAmount(row.Id, 1); e.Handled = true; }
            else if (e.Key == Key.Down) { ChangeAmount(row.Id, -1); e.Handled = true; }
        }

        private void RandomAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (FillRow row in _rows) row.Amount = _random.Next(0, Bucket.Capacity + 1).ToString(CultureInfo.InvariantCulture);
        }

        // Сброс случайного: вернуть значения, которые были при открытии окна.
        private void ResetRandom_Click(object sender, RoutedEventArgs e)
        {
            foreach (FillRow row in _rows) row.Amount = _initial[row.Id].ToString(CultureInfo.InvariantCulture);
        }

        private void NumberValidation(object sender, TextCompositionEventArgs e) { e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$"); }

        // "qwerty" -> прошлое значение; "07" -> 7; "123" -> 10.
        private void Amount_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox box = sender as TextBox;
            FillRow row = box == null ? null : box.DataContext as FillRow;
            if (row == null) return;
            string text = (box.Text ?? "").Trim();
            if (!Regex.IsMatch(text, "^[0-9]+$"))
            {
                row.Amount = _initial[row.Id].ToString(CultureInfo.InvariantCulture);
                ShowToast("Введите целое число от 0 до 10. Возвращено прошлое значение.");
                return;
            }
            string digits = text.TrimStart('0');
            int value = digits.Length == 0 ? 0 : (digits.Length > 2 ? 99 : int.Parse(digits, CultureInfo.InvariantCulture));
            if (value > Bucket.Capacity)
            {
                value = Bucket.Capacity;
                ShowToast("Ёмкость ведра - 10 литров. Установлено 10.");
            }
            row.Amount = value.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryAmount(string text, out int value)
        {
            value = 0;
            return Regex.IsMatch(text ?? "", "^(0|[1-9]|10)$") && int.TryParse(text, out value);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            int[] values = new int[10];
            for (int i = 0; i < 10; i++)
            {
                if (!TryAmount(_rows[i].Amount, out values[i]))
                {
                    ShowToast("Ведро №" + (i + 1) + ": заполнение должно быть от 0 до 10.");
                    return;
                }
            }
            FillLevels = values;
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

        private sealed class FillRow : INotifyPropertyChanged
        {
            private string _amount;
            public int Id { get; set; }
            public string Label { get; set; }
            public string Amount
            {
                get { return _amount; }
                set { _amount = value; PropertyChangedEventHandler h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs("Amount")); }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
