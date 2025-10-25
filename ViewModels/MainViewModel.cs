using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TaxiWPF.Models;
using TaxiWPF.Repositories;

namespace TaxiWPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DriverRepository _repository;
        private Driver _selectedDriver;

        public ObservableCollection<Driver> Drivers { get; set; }
        public Driver SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                _selectedDriver = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }

        public MainViewModel()
        {
            _repository = new DriverRepository();
            Drivers = new ObservableCollection<Driver>();
            LoadDrivers();

            AddCommand = new RelayCommand(AddDriver);
            SaveCommand = new RelayCommand(SaveDriver, CanSave);
            DeleteCommand = new RelayCommand(DeleteDriver, CanDelete);
        }

        private void LoadDrivers()
        {
            var drivers = _repository.GetAllDrivers();
            Drivers.Clear();
            foreach (var driver in drivers)
            {
                Drivers.Add(driver);
            }
        }

        private void AddDriver()
        {
            SelectedDriver = new Driver
            {
                full_name = "",
                car_model = "",
                license_plate = "",
                status = "Свободен",
                phone = ""
            };
        }

        private void SaveDriver()
        {
            if (SelectedDriver.driver_id == 0)
            {
                
                _repository.AddDriver(SelectedDriver);
            }
            else
            {
                
                _repository.UpdateDriver(SelectedDriver);
            }
            LoadDrivers(); 
        }

        private bool CanSave() => SelectedDriver != null &&
                                  !string.IsNullOrWhiteSpace(SelectedDriver.full_name) &&
                                  !string.IsNullOrWhiteSpace(SelectedDriver.car_model) &&
                                  !string.IsNullOrWhiteSpace(SelectedDriver.license_plate) &&
                                  !string.IsNullOrWhiteSpace(SelectedDriver.status) &&
                                  !string.IsNullOrWhiteSpace(SelectedDriver.phone);

        private void DeleteDriver()
        {
            if (SelectedDriver != null && SelectedDriver.driver_id > 0)
            {
                _repository.DeleteDriver(SelectedDriver.driver_id);
                LoadDrivers();
                SelectedDriver = null;
            }
        }

        private bool CanDelete() => SelectedDriver != null && SelectedDriver.driver_id > 0;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
