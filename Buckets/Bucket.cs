using System;

namespace Buckets
{
    public sealed class Bucket
    {
        public const int Capacity = 10;
        public int Id { get; private set; }
        public string Color { get; private set; }
        public int WaterAmount { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsFull { get { return WaterAmount >= Capacity; } }

        public Bucket(int id, string color, int waterAmount = 0)
        {
            if (id < 0 || id > 9) throw new ArgumentOutOfRangeException("id");
            Id = id;
            SetColor(color);
            SetWaterAmount(waterAmount);
            IsActive = true;
        }

        public void Increment() { if (WaterAmount < Capacity) WaterAmount++; }
        public void Decrement() { if (WaterAmount > 0) WaterAmount--; }
        public int GetWaterAmount() { return WaterAmount; }
        public string GetColor() { return Color; }
        public void SetColor(string color)
        {
            if (String.IsNullOrWhiteSpace(color)) throw new ArgumentException("Цвет не задан.");
            Color = color;
        }
        public void SetWaterAmount(int amount)
        {
            if (amount < 0 || amount > Capacity) throw new ArgumentOutOfRangeException("amount", "Заполнение должно быть от 0 до 10 литров.");
            WaterAmount = amount;
        }
        public void Remove() { IsActive = false; }
        public void RestoreEmpty() { WaterAmount = 0; IsActive = true; }
        public void RestoreActivePreservingWater() { IsActive = true; }
    }
}
