//using System;
//using System.ComponentModel;
//using System.Runtime.CompilerServices;
//using System.Windows.Input;
//using System.Windows.Media;


//namespace Project_Exercise.Models
//{
//    public enum FlightState { PreReady, Ready, InitialGuidance, Midcourse, Terminal, Detonated }
//    public enum CommState { Linked, LinkError, None }
//    public enum TargetTrackState { Tracking, Lost, None }

//    public class Missile : INotifyPropertyChanged
//    {
//        private FlightState _flightState;
//        private CommState _commState;
//        private TargetTrackState _targetTrackState;

//        public string TypeName { get; set; }          // ex) "lsam" → 화면엔 "Isam"처럼 노출용 가공 가능
//        public ImageSource Photo { get; set; }        // 썸네일(없으면 null)
//        public string Id { get; set; }                // ex) "lsam-002"
//        public string TargetId { get; set; }          // ex) "pyo-001"
//        public double X { get; set; }                 // deg
//        public double Y { get; set; }                 // deg
//        public double Zkm { get; set; }               // km
//        public double SpeedMs { get; set; }           // m/s

//        public FlightState FlightState
//        {
//            get => _flightState;
//            set { if (_flightState != value) { _flightState = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanLaunch)); OnPropertyChanged(nameof(CanSelfDestruct)); } }
//        }
//        public CommState CommState
//        {
//            get => _commState;
//            set { if (_commState != value) { _commState = value; OnPropertyChanged(); } }
//        }
//        public TargetTrackState TargetTrackState
//        {
//            get => _targetTrackState;
//            set { if (_targetTrackState != value) { _targetTrackState = value; OnPropertyChanged(); } }
//        }

//        // 상태 기반 버튼 가능 여부
//        public bool CanLaunch => FlightState == FlightState.Ready;
//        public bool CanSelfDestruct
//        {
//            get => FlightState == FlightState.InitialGuidance ||
//                   FlightState == FlightState.Midcourse ||
//                   FlightState == FlightState.Terminal;
//        }

//        // 커맨드(필요 시 VM에서 주입해도 OK)
//        public ICommand LaunchCommand { get; set; }
//        public ICommand SelfDestructCommand { get; set; }

//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//    }

//    // 간단한 RelayCommand
//    public class RelayCommand : ICommand
//    {
//        private readonly Action _act;
//        private readonly Func<bool> _can;
//        public RelayCommand(Action act, Func<bool> can = null) { _act = act; _can = can; }
//        public bool CanExecute(object parameter) => _can?.Invoke() ?? true;
//        public void Execute(object parameter) => _act();
//        public event EventHandler CanExecuteChanged;
//        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
//    }
//}
