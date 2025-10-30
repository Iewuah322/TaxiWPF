using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using TaxiWPF.Models;


namespace TaxiWPF.Views
{
    public partial class ClientView : Window
    {
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private GMapMarker _markerA;
        private GMapMarker _markerB;
        private GMapRoute _currentRoute;
        private ClientViewModel _viewModel;

        public ClientView()
        {
            InitializeComponent();

            GMap.NET.MapProviders.OpenStreetMapProvider.UserAgent = "MyTaxiApp/1.0";



            this.DataContextChanged += (sender, args) =>
            {
                // Отписываемся от старого ViewModel (на всякий случай)
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                }

                // Получаем НОВЫЙ ViewModel, который нам передал LoginViewModel
                _viewModel = DataContext as ClientViewModel;

                // Подписываемся на его события
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                }
            };



            _viewModel = DataContext as ClientViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            // Используем самый стабильный бесплатный провайдер
            MainMap.MapProvider = GMap.NET.MapProviders.OpenStreetMapProvider.Instance;

            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            MainMap.Position = new PointLatLng(55.1599, 61.4026); // Челябинск
            MainMap.MinZoom = 5;
            MainMap.MaxZoom = 18;
            MainMap.Zoom = 12;
            MainMap.MouseWheelZoomType = MouseWheelZoomType.MousePositionWithoutCenter;
            MainMap.CanDragMap = true;
            MainMap.DragButton = MouseButton.Right;

            MainMap.MouseLeftButtonUp += MainMap_MouseLeftButtonUp;
            MainMap.MouseMove += MainMap_MouseMove;
            // Переименуем старый обработчик
            MainMap.MouseLeftButtonDown += MainMap_PreviewMouseLeftButtonDown;
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }
        private void RatingButton_Checked(object sender, RoutedEventArgs e)
        {
            // Проверяем ViewModel
            if (_viewModel == null) return;

            if (sender is RadioButton rb && rb.Tag is string ratingStr && int.TryParse(ratingStr, out int rating))
            {
                // Напрямую устанавливаем рейтинг в ViewModel
                _viewModel.CurrentRating = rating;
            }
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

        

        private void MainMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            if (_viewModel == null || _viewModel.CurrentOrderState != OrderState.Idle)
            {
                return; // Не даем ставить точки, если мы не в режиме Idle
            }

            var point = e.GetPosition(MainMap);
            var latLng = MainMap.FromLocalToLatLng((int)point.X, (int)point.Y);

            if (RbPointA.IsChecked == true)
            {
                // ... (код для маркера А)
                _viewModel.PointA = latLng;
            }
            else if (RbPointB.IsChecked == true)
            {
                // ... (код для маркера Б)
                _viewModel.PointB = latLng;
            }

            // Если обе точки установлены, строим и рисуем маршрут
            if (!_viewModel.PointA.IsEmpty && !_viewModel.PointB.IsEmpty)
            {
                DrawRoute(_viewModel.PointA, _viewModel.PointB);
            }
        }



        private void DrawRoute(PointLatLng start, PointLatLng end)
        {
            var route = OpenStreetMapProvider.Instance.GetRoute(start, end, false, false, 15);
            if (route != null)
            {
                if (_currentRoute != null)
                {
                    MainMap.Markers.Remove(_currentRoute);
                }

                _currentRoute = new GMapRoute(route.Points) { /* ... */ };
                MainMap.Markers.Add(_currentRoute);

                // Убираем автоматический зум
                // MainMap.ZoomAndCenterMarkers(null);
            }
        }
       

        private void MainMap_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _dragStartPoint = e.GetPosition(MainMap);
        }

        private void MainMap_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point currentPoint = e.GetPosition(MainMap);
                // Если мышь сдвинулась на 3 пикселя, считаем это перетаскиванием
                if (Math.Abs(currentPoint.X - _dragStartPoint.X) > 3 ||
                    Math.Abs(currentPoint.Y - _dragStartPoint.Y) > 3)
                {
                    _isDragging = true;
                }
            }
        }

        private void MainMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {

            if (_viewModel == null || _viewModel.CurrentOrderState != OrderState.Idle)
            {
                _isDragging = false; // Сбрасываем флаг перетаскивания
                return; // Выходим из метода
            }

            // Если это не было перетаскивание, то это клик
            if (!_isDragging)
            {
                var latLng = MainMap.FromLocalToLatLng((int)_dragStartPoint.X, (int)_dragStartPoint.Y);

                if (RbPointA.IsChecked == true)
                {
                    _viewModel.PointA = latLng;
                }
                else if (RbPointB.IsChecked == true)
                {
                    _viewModel.PointB = latLng;
                }

                // После установки точки, переключим RadioButton
                // если была выбрана точка А, следующий клик будет для точки Б
                if (RbPointA.IsChecked == true) RbPointB.IsChecked = true;
            }
            _isDragging = false;
        }


        

        // Добавим подписку на изменение точек в ViewModel, чтобы перерисовывать карту
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == nameof(ClientViewModel.PointA) || e.PropertyName == nameof(ClientViewModel.PointB)))
            {
                // Обновляем маркеры
                UpdateMarkers();

                if (!_viewModel.PointA.IsEmpty && !_viewModel.PointB.IsEmpty)
                {
                    // Рисуем маршрут
                    DrawRoute(_viewModel.PointA, _viewModel.PointB);
                }
            }
        }

        private void UpdateMarkers()
        {
            // Удаляем старые маркеры
            if (_markerA != null) MainMap.Markers.Remove(_markerA);
            if (_markerB != null) MainMap.Markers.Remove(_markerB);

            // Добавляем маркер А, если есть координата
            if (!_viewModel.PointA.IsEmpty)
            {
                _markerA = new GMapMarker(_viewModel.PointA)
                {
                    Shape = new Ellipse { Width = 12, Height = 12, Fill = Brushes.Green, Stroke = Brushes.White, StrokeThickness = 2 }
                };
                MainMap.Markers.Add(_markerA);
            }

            // Добавляем маркер Б
            if (!_viewModel.PointB.IsEmpty)
            {
                _markerB = new GMapMarker(_viewModel.PointB)
                {
                    Shape = new Ellipse { Width = 12, Height = 12, Fill = Brushes.Red, Stroke = Brushes.White, StrokeThickness = 2 }
                };
                MainMap.Markers.Add(_markerB);
            }
        }


    }



}
