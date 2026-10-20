using System;
using System.Collections.Generic;
using System.Linq;

namespace Buckets
{
    /// <summary>
    /// Логика одного такта (по блок-схеме):
    ///   1) PickBucket()      - выбор случайного неполного ведра a из N;
    ///   2) (анимация капли в окне);
    ///   3) CompleteDrop(a)   - инкремент воды в ведре a;
    ///   4) IncidentLamp()    - с вероятностью pLamp лампа срабатывает и из
    ///                          случайного непустого неполного ведра убирается 1 л.
    /// Интервал такта: y = -0,035 * uWater + 3,6 (секунд).
    /// </summary>
    public sealed class BucketController
    {
        private readonly List<Bucket> _buckets;
        private readonly Random _random = new Random();

        /// <summary>uWater - скорость генерации воды (0-100).</summary>
        public int WaterSpeed { get; set; }
        /// <summary>pLamp - вероятность аварийной лампочки (0-100).</summary>
        public int EmergencyProbability { get; set; }
        public event EventHandler StateChanged;

        public BucketController(IEnumerable<Bucket> buckets)
        {
            if (buckets == null) throw new ArgumentNullException("buckets");
            _buckets = buckets.ToList();
            if (_buckets.Count != 10) throw new ArgumentException("Контроллер должен получить ровно 10 вёдер.");
            WaterSpeed = 50;
            EmergencyProbability = 20;
        }

        public IList<Bucket> Buckets { get { return _buckets.AsReadOnly(); } }
        public double IntervalSeconds { get { return -0.035 * WaterSpeed + 3.6; } }
        public bool AreAllFull { get { return _buckets.All(b => b.IsFull); } }

        /// <summary>Выбирает случайное неполное ведро; null, если все полные.</summary>
        public Bucket PickBucket()
        {
            List<Bucket> candidates = _buckets.Where(b => !b.IsFull).ToList();
            return candidates.Count == 0 ? null : candidates[_random.Next(candidates.Count)];
        }

        /// <summary>Капля долетела: +1 литр (если ведро ещё не полное).</summary>
        public void CompleteDrop(Bucket bucket)
        {
            if (bucket != null && !bucket.IsFull) bucket.Increment();
            RaiseStateChanged();
        }

        /// <summary>
        /// IncidentLamp(): r = random(0..99); если r &lt; pLamp - лампа срабатывает и из
        /// случайного ведра с 1-9 литрами убирается одна капля.
        /// Возвращает ведро, из которого вылили каплю, либо null (авария не произошла).
        /// </summary>
        public Bucket IncidentLamp()
        {
            if (_random.Next(100) >= EmergencyProbability) return null;

            List<Bucket> candidates = _buckets.Where(b => b.WaterAmount > 0 && b.WaterAmount < Bucket.Capacity).ToList();
            if (candidates.Count == 0) return null;
            Bucket affected = candidates[_random.Next(candidates.Count)];
            affected.Decrement();
            RaiseStateChanged();
            return affected;
        }

        public void SetFillLevels(IEnumerable<int> levels)
        {
            int[] values = levels.ToArray();
            if (values.Length != 10 || values.Any(v => v < 0 || v > Bucket.Capacity))
                throw new ArgumentException("Нужно указать заполнение 10 вёдер от 0 до 10.");
            for (int i = 0; i < 10; i++) _buckets[i].SetWaterAmount(values[i]);
            RaiseStateChanged();
        }

        private void RaiseStateChanged()
        {
            EventHandler handler = StateChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
