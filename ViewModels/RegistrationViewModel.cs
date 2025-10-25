using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TaxiWPF.Models;
using TaxiWPF.Repositories;
using System.ComponentModel;

namespace TaxiWPF.ViewModels
{
    public class RegistrationViewModel : INotifyPropertyChanged
    {
        private readonly UserRepository _userRepository;
        private string _username;
        private string _password;
        private string _email;

        public string Username { get => _username; set { _username = value; OnPropertyChanged(nameof(Username)); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(nameof(Password)); } }
        public string Email { get => _email; set { _email = value; OnPropertyChanged(nameof(Email)); } }

        public ICommand RegisterCommand { get; }

        public RegistrationViewModel()
        {
            _userRepository = new UserRepository();
            RegisterCommand = new RelayCommand(Register, CanRegister);
        }

        private bool CanRegister()
        {
            return !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Password) &&
                   !string.IsNullOrWhiteSpace(Email) && Email.Contains("@");
        }

        private void Register()
        {
            var newUser = new User
            {
                username = this.Username,
                password = this.Password,
                email = this.Email,
                role = "Client" // По умолчанию все новые пользователи - клиенты
            };

            if (_userRepository.AddUser(newUser))
            {
                MessageBox.Show("Регистрация прошла успешно! Теперь вы можете войти.", "Успех");
                // Закрываем окно регистрации
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        window.Close();
                        break;
                    }
                }
            }
            else
            {
                MessageBox.Show("Пользователь с таким именем или email уже существует.", "Ошибка регистрации");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
