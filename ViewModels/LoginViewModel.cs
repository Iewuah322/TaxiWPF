using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TaxiWPF.Models;
using TaxiWPF.Repositories;
using TaxiWPF.Views;

namespace TaxiWPF.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly UserRepository _userRepository;
        private string _username;
        private string _password;

        public string Username { get => _username; set { _username = value; OnPropertyChanged(nameof(Username)); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(nameof(Password)); } }

        public ICommand LoginCommand { get; }
        public ICommand GoToRegisterCommand { get; }
        public ICommand GoToRecoveryCommand { get; }

        public LoginViewModel()
        {
            _userRepository = new UserRepository();
            LoginCommand = new RelayCommand(Login, () => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password));
            GoToRegisterCommand = new RelayCommand(GoToRegister);
            GoToRecoveryCommand = new RelayCommand(GoToRecovery);
        }

        private void Login()
        {
            var user = _userRepository.GetUserByUsername(Username);
            if (user != null && user.password == Password)
            {
                MessageBox.Show($"Добро пожаловать, {user.username}!");

                if (user.role == "Driver")
                {
                    // --- ИЗМЕНЕНИЕ ---
                    // Открываем новый дашборд вместо карты
                    var dashboardVM = new DriverDashboardViewModel(user);
                    var dashboardView = new DriverDashboardView();
                    dashboardView.DataContext = dashboardVM;
                    dashboardView.Show();
                    // -----------------
                }
                else // "Client"
                {
                    var clientVM = new ClientViewModel(user);
                    var clientView = new ClientView();
                    clientView.DataContext = clientVM;
                    clientView.Show();
                }

                // Закрываем окно входа
                //foreach (Window window in Application.Current.Windows)
                //{
                //  if (window.DataContext == this)
                // {
                //    window.Close();
                //    break;
                // }
                //}
            }
            else
            {
                MessageBox.Show("Неверное имя пользователя или пароль.");
            }
        }

        private void GoToRegister()
        {
            var registrationView = new RegistrationView();
            registrationView.ShowDialog();
        }

        private void GoToRecovery()
        {
            var recoveryView = new PasswordRecoveryView();
            recoveryView.ShowDialog();
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
