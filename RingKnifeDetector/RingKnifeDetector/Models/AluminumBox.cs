using System.ComponentModel;

namespace RingKnifeDetector.Models
{
    public class AluminumBox : INotifyPropertyChanged
    {
        private string _boxNo = string.Empty;
        private decimal? _boxMass;
        private decimal? _wetSampleMass;
        private decimal? _drySampleMass;
        private decimal? _moistureRate;

        public string BoxNo { get => _boxNo; set { _boxNo = value; OnPropertyChanged(); } }
        public decimal? BoxMass { get => _boxMass; set { _boxMass = value; OnPropertyChanged(); } }
        public decimal? WetSampleMass { get => _wetSampleMass; set { _wetSampleMass = value; OnPropertyChanged(); } }
        public decimal? DrySampleMass { get => _drySampleMass; set { _drySampleMass = value; OnPropertyChanged(); } }
        public decimal? MoistureRate { get => _moistureRate; set { _moistureRate = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}