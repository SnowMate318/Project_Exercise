using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Project_Exercise.Views
{

    public class MapLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsEnemy { get; set; }
        public string Name { get; set; } // 툴팁용
    }

    /// <summary>
    /// MAP_Exercise.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MAP_Exercise : UserControl
    {

        public MAP_Exercise()
        {
            var locations = new List<MapLocation>
            {
                new MapLocation { Latitude = 37.5665, Longitude = 126.9780, Name = "서울" , IsEnemy=true},
                new MapLocation { Latitude = 35.8714, Longitude = 128.6014, Name = "대구" , IsEnemy=false}
            };

            InitializeComponent();

            Loaded += (s, e) =>
            {
                // 네트워크/TLS 이슈 예방
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                // 기본 설정
                GMapProvider.WebProxy = null;
                GMaps.Instance.Mode = AccessMode.ServerAndCache;
                PART_Map.MapProvider = OpenStreetMapProvider.Instance;

                PART_Map.MinZoom = 2;
                PART_Map.MaxZoom = 18;

                // 바인딩된 초기값 반영
                PART_Map.Zoom = Zoom;
                PART_Map.Position = new PointLatLng(Latitude, Longitude);
                AddMarkers(locations);
            };
        }

        private void Map_Loaded(object sender, RoutedEventArgs e)
        {
            // 여기에 맵 초기화 코드 작성
        }

        #region Dependency Properties

        public static readonly DependencyProperty LatitudeProperty =
            DependencyProperty.Register(nameof(Latitude), typeof(double), typeof(MAP_Exercise),
                new PropertyMetadata(37.5665, OnCenterChanged)); // 기본: 서울

        public static readonly DependencyProperty LongitudeProperty =
            DependencyProperty.Register(nameof(Longitude), typeof(double), typeof(MAP_Exercise),
                new PropertyMetadata(126.9780, OnCenterChanged));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(int), typeof(MAP_Exercise),
                new PropertyMetadata(7, OnZoomChanged));

        public static readonly DependencyProperty IsDraggableProperty =
            DependencyProperty.Register(nameof(IsDraggable), typeof(bool), typeof(MAP_Exercise),
                new PropertyMetadata(true));

        public double Latitude
        {
            get => (double)GetValue(LatitudeProperty);
            set => SetValue(LatitudeProperty, value);
        }

        public double Longitude
        {
            get => (double)GetValue(LongitudeProperty);
            set => SetValue(LongitudeProperty, value);
        }

        public int Zoom
        {
            get => (int)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public bool IsDraggable
        {
            get => (bool)GetValue(IsDraggableProperty);
            set => SetValue(IsDraggableProperty, value);
        }

        #endregion

        private static void OnCenterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (MAP_Exercise)d;
            if (ctl.PART_Map == null) return;
            ctl.PART_Map.Position = new PointLatLng(ctl.Latitude, ctl.Longitude);
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (MAP_Exercise)d;
            if (ctl.PART_Map == null) return;
            ctl.PART_Map.Zoom = ctl.Zoom;
        }

        public void AddMarkers(IEnumerable<MapLocation> locations)
        {
            foreach (var loc in locations)
            {
                AddMarker(loc.Latitude, loc.Longitude, loc.IsEnemy, loc.Name);
            }
        }
        public void AddMarker(double latitude, double longitude, bool IsEnemy, string toolTip = null)
        {
            PointLatLng pos = new PointLatLng(latitude, longitude);

            // 원형 마커
            //var markerShape = new Ellipse
            //{
            //    Width = 24,
            //    Height = 24,
            //    Stroke = Brushes.Red,
            //    StrokeThickness = 2,
            //    Fill = Brushes.OrangeRed
            //};

            var markerShape = new Image
            {
                Width = 32,
                Height = 32,
                Source = new BitmapImage(new Uri("pack://application:,,,/Project_Exercise;component/Resources/msl.png")),

                ToolTip = toolTip
            };
            var markerShape2 = new Image
            {
                Width = 32,
                Height = 32,
                Source = new BitmapImage(new Uri("pack://application:,,,/Project_Exercise;component/Resources/airplane.jpeg")),

                ToolTip = toolTip
            };

            //var markerShape2 = new Image
            //{
            //    Width = 24,
            //    Height = 24,
            //    Source = new BitmapImage(new Uri("\"pack://application:,,,/Resources/msl.png")),
            //    ToolTip = toolTip
            //};

            //new Image
            //{
            //    Width = 24,
            //    Height = 32,
            //    Source = new BitmapImage(new Uri("pack://application:,,,/Resources/airplane.jpeg")),
            //    ToolTip = toolTip
            //};

            var marker = new GMapMarker(pos)
            {
                Shape = IsEnemy ? markerShape2 : markerShape,
                Offset = new System.Windows.Point(-6, -6) // 마커 중심 맞춤
            }; 

            if (!string.IsNullOrEmpty(toolTip))
            {
                markerShape.ToolTip = toolTip;
            }

            PART_Map.Markers.Add(marker);

            // 지도 중심을 해당 위치로 이동시키고 싶다면:
            PART_Map.Position = pos;
        }
    }
}
