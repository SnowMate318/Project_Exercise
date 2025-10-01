using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes; // Path
using System.Windows.Input; // Cursors, Mouse args

namespace Project_Exercise.Views
{
    /// <summary>
    /// 사용자가 찍을 "시작점/종료점" 셋을 만들 때 참고하는 간단 모델.
    /// 실제 이동 엔티티는 MovingMarker(내부 클래스)가 담당.
    /// </summary>
    public class MapLocation
    {
        public double Latitude { get; set; }    // 위도
        public double Longitude { get; set; }   // 경도
        public bool IsEnemy { get; set; }       // true=적군, false=아군
        public string Name { get; set; }        // 툴팁/디버깅용
    }

    /// <summary>
    /// 런타임 클릭으로 "시작→종료"를 지정해 이동하는 마커를 생성 (등속, 마하3) + 경로 라인 표시.
    /// - 지도 클릭 2번(시작/종료)로 1개의 이동 마커 생성
    /// - 적/아군 체크박스로 아이콘 분기
    /// - DispatcherTimer로 매 프레임 위치 갱신 (등속, 대권(Slerp) 보간)
    /// - C# 7.3 호환 / 구버전 GMap.NET 호환(특정 신버전 API 미사용)
    /// </summary>
    public partial class MAP_Exercise : UserControl
    {
        // =========================
        // 1) 상수/설정
        // =========================

        // "마하 3" 속도(단순 해면 음속 가정): 340.29 m/s * 3
        private const double MACH3_MS = 340.29 * 3.0;        // ≈ 1020.87 m/s
        // 경로 폴리라인을 몇 분할로 샘플링할지(값이 클수록 곡선이 부드러움)
        private const int ROUTE_SEGMENTS = 64;
        // 애니메이션 업데이트 주기(≈60 FPS)
        private static readonly TimeSpan TIMER_INTERVAL = TimeSpan.FromMilliseconds(16);

        // =========================
        // 2) 내부 상태
        // =========================

        // 현재 화면에 존재하는 "이동 마커" 목록
        private readonly List<MovingMarker> _moving = new List<MovingMarker>();
        // 프레임 타이머 및 시간 측정기
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private long _lastTicksMs = 0; // 이전 틱 타임(ms)

        // 배치 모드 관련 상태
        private bool _placementActive = false;  // 배치 모드 On/Off
        private bool _awaitingStart = true;     // true: 다음 클릭은 시작점, false: 다음 클릭은 종료점
        private PointLatLng _pendingStart;      // 시작점 임시 저장
        private bool _pendingIsEnemy = true;    // 시작점 클릭 시의 체크박스 상태(적/아군)

        private Cursor _prevCursor;             // 배치 모드 진입 전에 사용자의 커서

        // 실제 이동을 수행하는 엔티티(내부 표현)
        private class MovingMarker
        {
            public PointLatLng Start;           // 시작 위경도
            public PointLatLng End;             // 종료 위경도
            public double SpeedMs;              // 속도(m/s) : 여기선 고정=마하3
            public double DistanceMeters;       // 총 거리(하버사인)
            public double DurationSeconds;      // 총 소요 시간 = 거리/속도
            public double ElapsedSeconds;       // 경과 시간(초)
            public GMapMarker Marker;           // 실제 지도에 표시되는 아이콘 마커
            public GMapRoute Route;             // 경로(폴리라인). Shape=Path로 스타일 지정
            public string Name;                 // 디버그/툴팁 용도
        }

        public MAP_Exercise()
        {
            InitializeComponent();

            // 프레임 타이머 구성
            _timer = new DispatcherTimer { Interval = TIMER_INTERVAL };
            _timer.Tick += OnTick;

            Loaded += (s, e) =>
            {
                // (네트워크 보안) 구형 환경에서 TLS 문제 예방
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                // GMap 기본 설정
                GMapProvider.WebProxy = null;                        // 직접 프록시 사용 안 함
                GMaps.Instance.Mode = AccessMode.ServerAndCache;     // 서버에서 타일 수신 + 로컬 캐시 사용
                PART_Map.MapProvider = OpenStreetMapProvider.Instance; // 지도 제공자: OpenStreetMap

                PART_Map.MinZoom = 2;    // 줌 하한
                PART_Map.MaxZoom = 18;   // 줌 상한

                // 초기 카메라(서울 인근)
                PART_Map.Zoom = Zoom;
                PART_Map.Position = new PointLatLng(Latitude, Longitude);
                PART_Map.CanDragMap = IsDraggable;

                // 지도 클릭 이벤트로 시작/종료를 받는다
                PART_Map.MouseLeftButtonDown += PART_Map_MouseLeftButtonDown;

                // 애니메이션 시작(항상 켜 두고, 움직일 대상이 없으면 그냥 idle)
                _timer.Start();
                _stopwatch.Restart();
                _lastTicksMs = 0;

                UpdateStatus("상태: 대기");
            };
        }

        // =========================
        // 3) 의존성 속성 (외부 바인딩용)
        //    - 뷰모델/상위 컨트롤에서 지도 중심, 줌, 드래그 허용 등을 동적으로 조절 가능
        // =========================

        public static readonly DependencyProperty LatitudeProperty =
            DependencyProperty.Register(nameof(Latitude), typeof(double), typeof(MAP_Exercise),
                new PropertyMetadata(37.5665, OnCenterChanged)); // 기본값: 서울 위도

        public static readonly DependencyProperty LongitudeProperty =
            DependencyProperty.Register(nameof(Longitude), typeof(double), typeof(MAP_Exercise),
                new PropertyMetadata(126.9780, OnCenterChanged));  // 기본값: 서울 경도

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(MAP_Exercise),
                new PropertyMetadata(7.0, OnZoomChanged));         // GMap WPF의 Zoom은 double

        public static readonly DependencyProperty IsDraggableProperty =
            DependencyProperty.Register(nameof(IsDraggable), typeof(bool), typeof(MAP_Exercise),
                new PropertyMetadata(true, OnDragChanged));        // 지도 드래그 허용 여부

        public double Latitude
        {
            get { return (double)GetValue(LatitudeProperty); }
            set { SetValue(LatitudeProperty, value); }
        }

        public double Longitude
        {
            get { return (double)GetValue(LongitudeProperty); }
            set { SetValue(LongitudeProperty, value); }
        }

        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        public bool IsDraggable
        {
            get { return (bool)GetValue(IsDraggableProperty); }
            set { SetValue(IsDraggableProperty, value); }
        }

        private static void OnCenterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 외부에서 Latitude/Longitude 변경 시 지도 중심을 즉시 반영
            var ctl = (MAP_Exercise)d;
            if (ctl.PART_Map == null) return;
            ctl.PART_Map.Position = new PointLatLng(ctl.Latitude, ctl.Longitude);
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 외부에서 Zoom 변경 시 지도에 반영
            var ctl = (MAP_Exercise)d;
            if (ctl.PART_Map == null) return;
            ctl.PART_Map.Zoom = ctl.Zoom;
        }

        private static void OnDragChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 외부에서 드래그 허용 여부를 바꾸면 지도에 반영
            var ctl = (MAP_Exercise)d;
            if (ctl.PART_Map == null) return;
            ctl.PART_Map.CanDragMap = ctl.IsDraggable;
        }

        // =========================
        // 4) 우측 상단 조작 패널 이벤트
        // =========================

        // 배치 모드: On
        private void TglPlacementMode_Checked(object sender, RoutedEventArgs e)
        {
            _placementActive = true;
            _awaitingStart = true;                         // 첫 클릭은 시작점
            _pendingIsEnemy = ChkIsEnemy.IsChecked == true;

            // 배치 중에는 지도 드래그 잠금 + 시인성을 위한 십자 커서
            PART_Map.CanDragMap = false;
            _prevCursor = Cursor;
            Cursor = Cursors.Cross;

            UpdateStatus("배치 모드: 시작점을 클릭하세요");
        }

        // 배치 모드: Off
        private void TglPlacementMode_Unchecked(object sender, RoutedEventArgs e)
        {
            _placementActive = false;

            // 지도 드래그 원복 + 커서 원복
            PART_Map.CanDragMap = IsDraggable;
            if (_prevCursor != null) Cursor = _prevCursor; else Cursor = Cursors.Arrow;

            UpdateStatus("상태: 대기");
        }

        // 모두 지우기: 현재 지도상의 이동 마커/경로 전부 제거
        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _moving.Clear();           // 내부 목록 비우기
            PART_Map.Markers.Clear();  // 지도에서 전부 제거
            UpdateStatus("상태: 모두 삭제됨");
        }

        // 간단한 상태 표시 텍스트 업데이트
        private void UpdateStatus(string text)
        {
            if (TxtStatus != null) TxtStatus.Text = text;
        }

        // =========================
        // 5) 지도 클릭 처리 (배치 모드일 때만 동작)
        // =========================
        private void PART_Map_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_placementActive) return; // 배치 모드 아닐 때는 무시

            // 마우스 좌표(화면좌표) → 지도 위경도로 변환
            var pt = e.GetPosition(PART_Map);
            // 일부 GMap 버전은 (int,int) 오버로드만 존재할 수 있으므로 int 캐스팅 사용
            PointLatLng latLng = PART_Map.FromLocalToLatLng((int)pt.X, (int)pt.Y);

            if (_awaitingStart)
            {
                // (1) 시작점 저장
                _pendingStart = latLng;
                // 시작점 클릭 시점의 체크박스 상태를 고정해 둠(적/아군)
                _pendingIsEnemy = ChkIsEnemy.IsChecked == true;
                _awaitingStart = false; // 다음 클릭은 종료점
                UpdateStatus("배치 모드: 종료점을 클릭하세요");
            }
            else
            {
                // (2) 종료점 결정 → 여기서 이동 마커 생성
                var end = latLng;
                CreateMovingMarker(_pendingStart, end, _pendingIsEnemy, "Runtime");

                // 다음 배치를 위해 다시 시작점 대기 상태로
                _awaitingStart = true;
                UpdateStatus("배치 모드: 시작점을 클릭하세요");
            }
        }

        // =========================
        // 6) 이동 마커 생성 (경로 라인 + 애니메이션 세팅)
        // =========================
        private void CreateMovingMarker(PointLatLng start, PointLatLng end, bool isEnemy, string name)
        {
            // --- 아이콘 이미지 로드 ---
            // 리소스 경로:
            //   pack://application:,,,/Project_Exercise;component/Resources/파일명
            // 이미지 파일은 프로젝트의 Resources 폴더에 두고
            //    "Build Action = Resource" 로 설정해야 함.
            Image shapeFriend, shapeEnemy;

            try
            {
                shapeFriend = new Image
                {
                    Width = 32,
                    Height = 32,
                    Source = new BitmapImage(new Uri("pack://application:,,,/Project_Exercise;component/Resources/msl.png")),
                    ToolTip = name + (isEnemy ? " (적군)" : " (아군)")
                };
            }
            catch
            {
                // 리소스 누락 시 대비: 단색 원으로 대체
                shapeFriend = new Image { Width = 32, Height = 32, ToolTip = name + " (아군)" };
            }

            try
            {
                shapeEnemy = new Image
                {
                    Width = 32,
                    Height = 32,
                    Source = new BitmapImage(new Uri("pack://application:,,,/Project_Exercise;component/Resources/airplane.jpeg")),
                    ToolTip = name + (isEnemy ? " (적군)" : " (아군)")
                };
            }
            catch
            {
                // 리소스 누락 시 대비
                shapeEnemy = new Image { Width = 32, Height = 32, ToolTip = name + " (적군)" };
            }

            var shapeChosen = isEnemy ? shapeEnemy : shapeFriend;

            // --- GMapMarker(아이콘) 생성/등록 ---
            var gm = new GMapMarker(start)
            {
                Shape = shapeChosen,
                Offset = new System.Windows.Point(-16, -16) // 이미지를 중앙 기준으로 맞춤
            };
            PART_Map.Markers.Add(gm);

            // --- 경로 라인(GMapRoute) 생성/등록 ---
            // 대권(Slerp)으로 보간한 포인트들을 폴리라인으로 만든다.
            var routePoints = BuildGreatCirclePolyline(start, end, ROUTE_SEGMENTS);

            var route = new GMapRoute(routePoints);
            // 구버전 GMap.NET에서는 Stroke 속성이 없을 수 있으므로,
            // Shape에 WPF Path를 직접 넣어 선 속성(색/두께)을 지정한다.
            route.Shape = new Path
            {
                Stroke = isEnemy ? Brushes.OrangeRed : Brushes.LimeGreen, // 적=주황빨강, 아군=라임
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            PART_Map.Markers.Add(route);

            // --- 이동 시간 계산(등속) ---
            // 거리(미터) / 속도(마하3m/s) → 총 소요 시간(초)
            double meters = HaversineMeters(start, end);
            double duration = Math.Max(0.001, meters / MACH3_MS);

            // --- 내부 이동 엔티티 구성 후 목록에 추가 ---
            var mv = new MovingMarker
            {
                Start = start,
                End = end,
                SpeedMs = MACH3_MS,
                DistanceMeters = meters,
                DurationSeconds = duration,
                ElapsedSeconds = 0,
                Marker = gm,
                Route = route,
                Name = name
            };
            _moving.Add(mv);
        }

        // (선택) 외부에서 수동으로 애니메이션 제어할 때 사용 가능
        public void StartAnimation()
        {
            if (!_timer.IsEnabled)
            {
                _lastTicksMs = 0;
                _stopwatch.Restart();
                _timer.Start();
            }
        }

        public void StopAnimation()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _stopwatch.Stop();
            }
        }

        // =========================
        // 7) 타이머 틱: 위치 갱신(등속 + 대권 보간)
        // =========================
        private void OnTick(object sender, EventArgs e)
        {
            // 프레임 간 경과시간(sec) 계산
            long now = _stopwatch.ElapsedMilliseconds;
            double dt = (_lastTicksMs == 0) ? 0 : (now - _lastTicksMs) / 1000.0;
            _lastTicksMs = now;
            if (dt <= 0) return;

            bool anyRunning = false;

            // 활성 이동 엔티티들 반복
            for (int i = 0; i < _moving.Count; i++)
            {
                var mv = _moving[i];

                // 경과 시간 업데이트
                if (mv.ElapsedSeconds >= mv.DurationSeconds)
                {
                    mv.ElapsedSeconds = mv.DurationSeconds; // 도착 고정
                }
                else
                {
                    mv.ElapsedSeconds += dt;
                }

                // 진행도 t:[0,1]
                double t = mv.ElapsedSeconds / mv.DurationSeconds;
                if (t < 1.0) anyRunning = true;
                if (t > 1.0) t = 1.0;

                // 현재 위치 = 대권 보간(Slerp)으로 계산
                var curr = SlerpLatLng(mv.Start, mv.End, t);
                mv.Marker.Position = curr;

                // (옵션) 도착 후 루프시키고 싶다면:
                // if (t >= 1.0) { mv.ElapsedSeconds = 0; }
            }

            // 모두 정지 상태일 때 타이머를 멈추고 싶다면 주석 해제
            // if (!anyRunning) { _timer.Stop(); _stopwatch.Stop(); }
        }

        // =========================
        // 8) 위경도/구면 보간 유틸리티
        // =========================

        private static double ToRad(double deg) { return deg * Math.PI / 180.0; }
        private static double ToDeg(double rad) { return rad * 180.0 / Math.PI; }

        /// <summary>
        /// 두 위경도 사이의 구면거리(미터) - Haversine 공식
        /// (지구 반경을 6371km로 고정한 근사)
        /// </summary>
        private static double HaversineMeters(PointLatLng a, PointLatLng b)
        {
            const double R = 6371000.0; // 지구 반경(m)
            double dLat = ToRad(b.Lat - a.Lat);
            double dLon = ToRad(b.Lng - a.Lng);
            double lat1 = ToRad(a.Lat);
            double lat2 = ToRad(b.Lat);

            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
            return R * c;
        }

        // 위경도 → 단위 구면 좌표(x,y,z)
        private static void LatLngToUnit(PointLatLng p, out double x, out double y, out double z)
        {
            double lat = ToRad(p.Lat);
            double lon = ToRad(p.Lng);
            double cl = Math.Cos(lat);
            x = cl * Math.Cos(lon);
            y = cl * Math.Sin(lon);
            z = Math.Sin(lat);
        }

        // 단위 구면 좌표(x,y,z) → 위경도
        private static PointLatLng UnitToLatLng(double x, double y, double z)
        {
            double hyp = Math.Sqrt(x * x + y * y);
            double lat = Math.Atan2(z, hyp);
            double lon = Math.Atan2(y, x);
            return new PointLatLng(ToDeg(lat), ToDeg(lon));
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>
        /// 대권 보간(Slerp)으로 a→b 사이의 지점(t:[0,1])을 계산
        /// - 장거리에서도 '직선처럼 보이는' 대권경로를 따라 이동
        /// - 구면 선형보간: 3D 단위벡터 상에서 각도 비율로 보간
        /// </summary>
        private static PointLatLng SlerpLatLng(PointLatLng a, PointLatLng b, double t)
        {
            double x1, y1, z1, x2, y2, z2;
            LatLngToUnit(a, out x1, out y1, out z1);
            LatLngToUnit(b, out x2, out y2, out z2);

            double dot = x1 * x2 + y1 * y2 + z1 * z2;
            dot = Clamp(dot, -1.0, 1.0);
            double theta = Math.Acos(dot);

            if (theta < 1e-9)
                return a; // 거의 동일 좌표인 경우: 시작점 반환

            double sinTheta = Math.Sin(theta);
            double w1 = Math.Sin((1 - t) * theta) / sinTheta;
            double w2 = Math.Sin(t * theta) / sinTheta;

            double x = w1 * x1 + w2 * x2;
            double y = w1 * y1 + w2 * y2;
            double z = w1 * z1 + w2 * z2;

            return UnitToLatLng(x, y, z);
        }

        /// <summary>
        /// 경로 라인 표시용: a→b 대권 경로를 일정 분할로 샘플링한 점 목록
        /// </summary>
        private static List<PointLatLng> BuildGreatCirclePolyline(PointLatLng a, PointLatLng b, int segmentCount)
        {
            var list = new List<PointLatLng>(segmentCount + 1);
            for (int i = 0; i <= segmentCount; i++)
            {
                double t = (double)i / segmentCount;
                list.Add(SlerpLatLng(a, b, t));
            }
            return list;
        }
    }
}
