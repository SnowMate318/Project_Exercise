//using Project_Exercise.Models;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Media.Animation;




//namespace Project_Exercise.ViewModels
//{
//    public class MissileCardViewModel
//    {
//        public Missile Missile { get; }

//        public MissileCardViewModel()
//        {
//            Missile = new Missile
//            {
//                TypeName = "Isam",
//                Id = "lsam-002",
//                TargetId = "pyo-001",
//                FlightState = FlightState.Midcourse,  // ← 바꿔가며 테스트
//                CommState = CommState.Linked,
//                TargetTrackState = TargetTrackState.Tracking,
//                X = 38.1111,
//                Y = 136.2222,
//                Zkm = 15,
//                SpeedMs = 1000
//            };

//            Missile.LaunchCommand = new RelayCommand(
//                act: () =>
//                {
//                    // 발사 커맨드   
//                },
//                can: () => Missile.CanLaunch
//            );

//            Missile.SelfDestructCommand = new RelayCommand(
//                act: () =>
//                {
//                    // 자폭 커맨드
//                },
//                can: () => Missile.CanSelfDestruct
//            );
//        }
//    }
//}
