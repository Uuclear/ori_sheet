using System.ComponentModel;

using RingKnifeDetector.Helpers;

namespace RingKnifeDetector.Models
{
    public class RingKnifeSample : INotifyPropertyChanged
    {
        private string _sampleNo = string.Empty;
        private string _elevation = string.Empty;
        private string _samplingDate = string.Empty;
        private string _testDate = string.Empty;
        private string _thickness = string.Empty;
        private decimal? _ringSampleMass;
        private decimal? _ringMass;
        private decimal? _ringVolume;

        public string SampleNo { get => _sampleNo; set { _sampleNo = value; OnPropertyChanged(); } }
        public string Elevation { get => _elevation; set { _elevation = value; OnPropertyChanged(); } }
        public string SamplingDate { get => _samplingDate; set { _samplingDate = value; OnPropertyChanged(); } }
        public string TestDate { get => _testDate; set { _testDate = value; OnPropertyChanged(); } }
        public string Thickness { get => _thickness; set { _thickness = value; OnPropertyChanged(); } }
        public decimal? RingSampleMass { get => _ringSampleMass; set { _ringSampleMass = value; OnPropertyChanged(); Recalculate(); } }
        public decimal? RingMass { get => _ringMass; set { _ringMass = value; OnPropertyChanged(); Recalculate(); } }
        public decimal? RingVolume { get => _ringVolume; set { _ringVolume = value; OnPropertyChanged(); Recalculate(); } }

        public List<AluminumBox> Boxes { get; set; } = new() { new AluminumBox(), new AluminumBox() };
        public List<RingMeasurement> Rings { get; set; } = new();

        public decimal? WetMass { get; private set; }
        public decimal? WetDensity { get; private set; }
        public decimal? AvgWetDensity { get; set; }
        public decimal? AvgMoisture { get; set; }
        public decimal? DryDensity { get; private set; }
        public decimal? AvgDryDensity { get; set; }
        public decimal? CompactionCoeff { get; set; }
        public decimal? CompactionPercent { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Recalculate()
        {
            WetMass = (RingSampleMass != null && RingMass != null)
                ? Math.Round(RingSampleMass.Value - RingMass.Value, 2) : null;
            WetDensity = (WetMass != null && RingVolume is > 0)
                ? Math.Round(WetMass.Value / RingVolume.Value, 2) : null;
            AvgWetDensity = WetDensity;

            foreach (var box in Boxes)
            {
                if (box.BoxMass != null && box.WetSampleMass != null && box.DrySampleMass != null)
                {
                    var drySoil = box.DrySampleMass.Value - box.BoxMass.Value;
                    if (drySoil > 0)
                    {
                        var wetSoil = box.WetSampleMass.Value - box.BoxMass.Value;
                        box.MoistureRate = wetSoil > drySoil
                            ? CompactionFormat.RoundMoisture((wetSoil - drySoil) / drySoil * 100) : null;
                    }
                    else box.MoistureRate = null;
                }
                else box.MoistureRate = null;
            }

            var validMoistures = Boxes.Take(2)
                .Where(b => b.MoistureRate != null)
                .Select(b => b.MoistureRate!.Value).ToList();
            AvgMoisture = validMoistures.Count > 0
                ? CompactionFormat.RoundMoisture(validMoistures.Average()) : null;

            if (WetDensity != null && AvgMoisture != null)
            {
                var factor = 1 + AvgMoisture.Value / 100;
                DryDensity = factor > 0 ? Math.Round(WetDensity.Value / factor, 2) : null;
            }
            else DryDensity = null;
            AvgDryDensity = DryDensity;

            OnPropertyChanged(nameof(WetMass));
            OnPropertyChanged(nameof(WetDensity));
            OnPropertyChanged(nameof(AvgWetDensity));
            OnPropertyChanged(nameof(AvgMoisture));
            OnPropertyChanged(nameof(DryDensity));
            OnPropertyChanged(nameof(AvgDryDensity));
            OnPropertyChanged(nameof(CompactionCoeff));
            OnPropertyChanged(nameof(CompactionPercent));

            foreach (var box in Boxes)
                box.OnPropertyChanged(nameof(AluminumBox.MoistureRate));
        }
    }
}