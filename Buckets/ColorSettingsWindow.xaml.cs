using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Buckets
{
    public partial class ColorSettingsWindow : Window
    {
        private readonly ObservableCollection<ColorRow> _rows = new ObservableCollection<ColorRow>();
        private bool _initializing = true;
        public string[] SelectedColors { get; private set; }

        public ColorSettingsWindow(IList<Bucket> buckets, IList<string> availableColors)
        {
            InitializeComponent();
            for (int i = 0; i < 10; i++) _rows.Add(new ColorRow { Id = i, Label = "Ведро " + i, SelectedColor = buckets[i].Color, AvailableColors = availableColors.ToList() });
            ColorRows.ItemsSource = _rows;
            _initializing = false;
        }

        private void Color_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            var combo = sender as ComboBox;
            if (combo == null || combo.SelectedItem == null) return;
            string selected = combo.SelectedItem.ToString();
            ColorRow row = combo.DataContext as ColorRow;
            if (row == null) return;
            ColorRow duplicate = _rows.FirstOrDefault(r => r.Id != row.Id && String.Equals(r.SelectedColor, selected, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                MessageBox.Show(this, "Этот цвет уже назначен ведру №" + duplicate.Id + ". Выберите другой цвет.", "Цвет уже используется", MessageBoxButton.OK, MessageBoxImage.Warning);
                row.SelectedColor = null;
                combo.SelectedItem = null;
            }
        }

        private void Random_Click(object sender, RoutedEventArgs e)
        {
            List<string> shuffled = _rows[0].AvailableColors.OrderBy(x => Guid.NewGuid()).Take(10).ToList();
            for (int i = 0; i < 10; i++) _rows[i].SelectedColor = shuffled[i];
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Any(r => String.IsNullOrWhiteSpace(r.SelectedColor)) || _rows.Select(r => r.SelectedColor).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 10)
            {
                MessageBox.Show(this, "Выберите 10 разных цветов.", "Некорректная конфигурация", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            SelectedColors = _rows.OrderBy(r => r.Id).Select(r => r.SelectedColor).ToArray();
            DialogResult = true;
        }

        private sealed class ColorRow
        {
            public int Id { get; set; }
            public string Label { get; set; }
            public string SelectedColor { get; set; }
            public List<string> AvailableColors { get; set; }
        }
    }
}
