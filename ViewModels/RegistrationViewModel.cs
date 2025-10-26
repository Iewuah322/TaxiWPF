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
using Microsoft.Win32;

namespace TaxiWPF.ViewModels
{
    public class RegistrationViewModel : INotifyPropertyChanged
    {
        private readonly UserRepository _userRepository;
        private string _username;
        private string _password;
        private string _email;
        private bool _isDriverRegistration = false;
        private string _licensePhotoPath;
        private string _profilePhotoPath;

        public string Username { get => _username; set { _username = value; OnPropertyChanged(nameof(Username)); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(nameof(Password)); } }
        public string Email { get => _email; set { _email = value; OnPropertyChanged(nameof(Email)); } }

        public bool IsDriverRegistration
        {
            get => _isDriverRegistration;
            set
            {
                _isDriverRegistration = value;
                OnPropertyChanged(nameof(IsDriverRegistration));
                // Обновляем состояние кнопки RegisterCommand
                (RegisterCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string LicensePhotoPath
        {
            get => _licensePhotoPath;
            set { _licensePhotoPath = value; OnPropertyChanged(nameof(LicensePhotoPath)); (RegisterCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
        }

        public string ProfilePhotoPath
        {
            get => _profilePhotoPath;
            set { _profilePhotoPath = value; OnPropertyChanged(nameof(ProfilePhotoPath)); (RegisterCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
        }

        public ICommand ToggleRegistrationModeCommand { get; }
        public ICommand SelectLicensePhotoCommand { get; }
        public ICommand SelectProfilePhotoCommand { get; }

        public ICommand RegisterCommand { get; }

        public RegistrationViewModel()
        {
            _userRepository = new UserRepository();
            RegisterCommand = new RelayCommand(Register, CanRegister);

            ToggleRegistrationModeCommand = new RelayCommand(() => IsDriverRegistration = !IsDriverRegistration);
            SelectLicensePhotoCommand = new RelayCommand(SelectLicensePhoto);
            SelectProfilePhotoCommand = new RelayCommand(SelectProfilePhoto);
        }

        private bool CanRegister()
        {
            // Общие поля
            bool baseValid = !string.IsNullOrWhiteSpace(Username) &&
                             !string.IsNullOrWhiteSpace(Password) &&
                             !string.IsNullOrWhiteSpace(Email) && Email.Contains("@");

            // Если режим водителя, проверяем и фото
            if (IsDriverRegistration)
            {
                return baseValid &&
                       !string.IsNullOrWhiteSpace(LicensePhotoPath) &&
                       !string.IsNullOrWhiteSpace(ProfilePhotoPath);
            }

            // Иначе достаточно базовых полей
            return baseValid;
        }

        private void Register()
        {
            var newUser = new User
            {
                username = this.Username,
                password = this.Password, // В реальном приложении пароль надо хешировать!
                email = this.Email,
                // --- ИЗМЕНЕНИЕ: Роль зависит от режима ---
                role = IsDriverRegistration ? "Driver" : "Client"
            };

            // --- ДОПОЛНИТЕЛЬНО (для водителя) ---
            // Здесь ты бы передал пути к фото (LicensePhotoPath, ProfilePhotoPath)
            // в метод AddUser или сохранил бы их отдельно, связав с user_id.
            // В нашей заглушке UserRepository.AddUser это пока не учитывает.
            // string licensePath = this.LicensePhotoPath;
            // string profilePath = this.ProfilePhotoPath;
            // ---------------------------------------

            if (_userRepository.AddUser(newUser))
            {
                MessageBox.Show("Регистрация прошла успешно! Теперь вы можете войти.", "Успех");
                // Закрываем окно регистрации
                foreach (Window window in Application.Current.Windows) { /* ... */ }
            }
            else
            {
                MessageBox.Show("Пользователь с таким именем или email уже существует.", "Ошибка регистрации");
            }
        }

        private void SelectLicensePhoto()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                LicensePhotoPath = openFileDialog.FileName;
            }
        }

        private void SelectProfilePhoto()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                ProfilePhotoPath = openFileDialog.FileName;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
