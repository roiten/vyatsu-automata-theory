using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Buckets
{
    public partial class BucketFillLevelSettingsWindow : Window
    {
        private readonly ObservableCollection<FillRow> _rows = new ObservableCollection<FillRow>();
        public int[] FillLevels { get; private set; }
        public BucketFillLevelSettingsWindow(System.Collections.Generic.IList<Bucket> buckets)
        {
            InitializeComponent();
            for (int i = 0; i < 10; i++) _rows.Add(new FillRow { Id = i, Label = "Ведро " + i, Amount = buckets[i].WaterAmount.ToString(CultureInfo.InvariantCulture) });
            FillRows.ItemsSource = _rows;
        }
        private void Increment_Click(object sender, RoutedEventArgs e) { ChangeAmount(sender, 1); }
        private void Decrement_Click(object sender, RoutedEventArgs e) { ChangeAmount(sender, -1); }
        private void ChangeAmount(object sender, int delta)
        {
            int id = (int)((Button)sender).Tag; int value;
            if (!TryAmount(_rows[id].Amount, out value)) value = 0;
            _rows[id].Amount = Math.Max(0, Math.Min(10, value + delta)).ToString(CultureInfo.InvariantCulture);
        }
        private void Reset_Click(object sender, RoutedEventArgs e) { foreach (FillRow row in _rows) row.Amount = "0"; }
        private void Random_Click(object sender, RoutedEventArgs e)
        {
            var random = new Random(); int max = (int)Math.Round(RandomPercentSlider.Value / 10.0);
            foreach (FillRow row in _rows) row.Amount = random.Next(0, max + 1).ToString(CultureInfo.InvariantCulture);
        }
        private void RandomPercentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        { if (RandomPercentLabel != null) RandomPercentLabel.Text = ((int)RandomPercentSlider.Value) + "%"; }
        private void NumberValidation(object sender, TextCompositionEventArgs e) { e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$"); }
        private void Amount_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox box = sender as TextBox; int value;
            if (box == null) return;
            if (!TryAmount(box.Text, out value)) { box.Text = "0"; MessageBox.Show(this, "Введите целое число от 0 до 10 без ведущих нулей.", "Некорректное заполнение", MessageBoxButton.OK, MessageBoxImage.Warning); }
            else box.Text = value.ToString(CultureInfo.InvariantCulture);
        }
        private bool TryAmount(string text, out int value)
        { value = 0; return Regex.IsMatch(text ?? "", "^(0|[1-9]|10)$") && int.TryParse(text, out value) && value >= 0 && value <= 10; }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            int[] values = new int[10];
            for (int i = 0; i < 10; i++) if (!TryAmount(_rows[i].Amount, out values[i]))
            { MessageBox.Show(this, "Ведро №" + i + ": заполнение должно быть от 0 до 10.", "Некорректное заполнение", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            FillLevels = values; DialogResult = true;
        }
        private sealed class FillRow { public int Id { get; set; } public string Label { get; set; } public string Amount { get; set; } }
    }
}
