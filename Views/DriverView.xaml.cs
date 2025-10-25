using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using TaxiWPF.ViewModels;

namespace TaxiWPF.Views
{
    public partial class DriverView : Window
    {
        private DriverViewModel _viewModel;
        private GMapMarker _driverMarker;
        private GMapMarker _passengerMarker;
        private GMapRoute _currentRoute;

        public DriverView()
        {
            InitializeComponent();
            _viewModel = DataContext as DriverViewModel;
            if (_viewModel != null)
            {
                // Подписываемся на событие для построения маршрута
                _viewModel.OnRouteRequired += DrawRoute;
            }

            // Настройка карты
            MainMap.MapProvider = OpenStreetMapProvider.Instance;
            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            MainMap.Position = new PointLatLng(55.1599, 61.4026); // Челябинск
            MainMap.MinZoom = 5;
            MainMap.MaxZoom = 18;
            MainMap.Zoom = 12;
            MainMap.MouseWheelZoomType = MouseWheelZoomType.MousePositionWithoutCenter;
            MainMap.CanDragMap = true;
            MainMap.DragButton = MouseButton.Right;
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        private void DrawRoute(PointLatLng start, PointLatLng end)
        {
            // Очищаем старые маркеры и маршруты
            MainMap.Markers.Clear();

            if (start.IsEmpty || end.IsEmpty)
            {
                return; // Если точки пустые, просто очищаем карту
            }

            // Получаем маршрут от OpenStreetMap
            var route = OpenStreetMapProvider.Instance.GetRoute(start, end, false, false, 15);
            if (route != null)
            {
                _currentRoute = new GMapRoute(route.Points)
                {
                    Shape = new Path
                    {
                        Stroke = Brushes.Blue,
                        StrokeThickness = 4,
                        StrokeDashArray = new DoubleCollection { 2, 2 } // Пунктирная линия
                    }
                };

                MainMap.Markers.Add(_currentRoute);
            }



            // Добавляем маркер водителя (точка А)
            _driverMarker = new GMapMarker(start)
            {
                Shape = new Ellipse
                {
                    Width = 14,
                    Height = 14,
                    Fill = Brushes.Blue,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                }
            };
            MainMap.Markers.Add(_driverMarker);

            // Добавляем маркер пассажира (точка Б)
            _passengerMarker = new GMapMarker(end)
            {
                Shape = new Ellipse
                {
                    Width = 14,
                    Height = 14,
                    Fill = Brushes.Green,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                }
            };
            MainMap.Markers.Add(_passengerMarker);

            // Центрируем и масштабируем карту, чтобы показать оба маркера
            MainMap.ZoomAndCenterMarkers(null);
        }
    }
}
