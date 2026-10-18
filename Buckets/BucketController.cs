using System;
using System.Collections.Generic;
using System.Linq;

namespace Buckets
{
    public sealed class BucketController
    {
        private readonly List<Bucket> _buckets;
        private readonly Random _random = new Random();
        public int WaterSpeed { get; set; }
        public int EmergencyProbability { get; set; }
        public event EventHandler StateChanged;
        public event Action<string> Message;

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

        public void ExecuteTick()
        {
            // Вёдра, исключённые на предыдущем такте, возвращаются пустыми.
            foreach (Bucket bucket in _buckets.Where(b => !b.IsActive).ToList()) bucket.RestoreEmpty();

            List<Bucket> active = _buckets.Where(b => b.IsActive).ToList();
            if (active.Count > 0)
            {
                Bucket selected = active[_random.Next(active.Count)];
                selected.Increment();
                if (selected.IsFull)
                {
                    selected.Remove();
                    RaiseMessage("Ведро №" + selected.Id + " заполнено и временно убрано.");
                }
            }

            // Авария действует только на непустые и неполные активные вёдра.
            if (_random.Next(100) < EmergencyProbability)
            {
                List<Bucket> candidates = _buckets.Where(b => b.IsActive && b.WaterAmount > 0 && b.WaterAmount < Bucket.Capacity).ToList();
                if (candidates.Count > 0)
                {
                    Bucket victim = candidates[_random.Next(candidates.Count)];
                    victim.Remove();
                    RaiseMessage("Авария: ведро №" + (victim.Id + 1) + " временно убрано.");
                }
            }
            EventHandler handler = StateChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Reset()
        {
            foreach (Bucket bucket in _buckets) bucket.RestoreEmpty();
            RaiseStateChanged();
        }

        public void SetFillLevels(IEnumerable<int> levels)
        {
            int[] values = levels.ToArray();
            if (values.Length != 10 || values.Any(v => v < 0 || v > Bucket.Capacity))
                throw new ArgumentException("Нужно указать заполнение 10 вёдер от 0 до 10.");
            for (int i = 0; i < 10; i++) _buckets[i].SetWaterAmount(values[i]);
            foreach (Bucket bucket in _buckets) bucket.RestoreActivePreservingWater();
            RaiseStateChanged();
        }

        private void RaiseStateChanged() { EventHandler handler = StateChanged; if (handler != null) handler(this, EventArgs.Empty); }
        private void RaiseMessage(string message) { Action<string> handler = Message; if (handler != null) handler(message); }
    }
}
