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
using TaxiWPF.ViewModels;
using System.Windows.Media.Animation;


namespace TaxiWPF.Views
{
    /// <summary>
    /// Логика взаимодействия для LoginView.xaml
    /// </summary>
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
            // Подписываемся на событие изменения пароля
            PasswordBox.PasswordChanged += OnPasswordChanged;
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

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                viewModel.Password = (sender as PasswordBox).Password;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, включен ли наш ToggleButton
            if (CrazyModeToggle.IsChecked == true)
            {
                // Если да - запускаем анимацию,
                // а она УЖЕ ПОСЛЕ СЕБЯ запустит логин
                AnimateAndLogin();
            }
            else
            {
                // Если нет - просто логинимся как обычно
                ExecuteLoginLogic();
            }
        }


        private void AnimateAndLogin()
        {
            // Настраиваем анимацию
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 720, // Два полных оборота
                Duration = TimeSpan.FromSeconds(4.0), // 1.5 секунды
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // ==== ВАЖНО ====
            // Мы "подписываемся" на событие ЗАВЕРШЕНИЯ анимации.
            animation.Completed += (s, e) =>
            {
                // Когда анимация закончилась - выполняем вход
                ExecuteLoginLogic();
            };

            // Находим наш RotateTransform
            var transform = RootBorder.RenderTransform as RotateTransform;

            // ==== ВОТ ИСПРАВЛЕНИЕ ====
            // Добавляем проверку, что transform не null
            if (transform != null)
            {
                // Запускаем анимацию на свойстве "Angle" (Угол)
                transform.BeginAnimation(RotateTransform.AngleProperty, animation);
            }
            else
            {
                // Если transform почему-то null, просто логинимся без анимации
                ExecuteLoginLogic();
            }
        }

        private void ExecuteLoginLogic()
        {
            // Получаем наш ViewModel из DataContext
            if (DataContext is LoginViewModel viewModel)
            {
                // ...и просто выполняем команду, 
                // которую мы убрали из XAML
                if (viewModel.LoginCommand.CanExecute(null))
                {
                    viewModel.LoginCommand.Execute(null);
                }
            }
        }

    }
}
