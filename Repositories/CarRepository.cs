﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiWPF.Models;
using System.Data.SqlClient; 
using System.Configuration; 
using System.Windows;

namespace TaxiWPF.Repositories
{
    public class CarRepository
    {
        private readonly string _connectionString; // <-- Добавлено

        // --- ЗАГЛУШКА БД УДАЛЕНА ---

        public CarRepository() // <-- Добавлен конструктор
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaxiDB"].ConnectionString;
        }

        // Получаем все машины водителя
        public List<Car> GetCarsByDriverId(int driverId)
        {
            var cars = new List<Car>();
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // Выбираем машины конкретного водителя
                    var command = new SqlCommand("SELECT * FROM Cars WHERE DriverId = @driverId", connection);
                    command.Parameters.AddWithValue("@driverId", driverId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var car = new Car
                            {
                                CarId = (int)reader["CarId"],
                                DriverId = (int)reader["DriverId"],
                                ModelName = reader["ModelName"].ToString(),
                                LicensePlate = reader["LicensePlate"].ToString(),
                                MainImageUrl = reader["MainImageUrl"] as string,
                                EngineInfo = reader["EngineInfo"] as string,
                                InsuranceInfo = reader["InsuranceInfo"] as string,
                                PhotoGallery = new List<string>() // Инициализируем пустой список
                            };
                            cars.Add(car);
                        }
                    } // reader закроется здесь

                    // --- Загружаем галерею для КАЖДОЙ машины ОТДЕЛЬНО ---
                    // (Можно оптимизировать одним запросом, но так проще для понимания)
                    foreach (var car in cars)
                    {
                        var galleryCommand = new SqlCommand("SELECT PhotoUrl FROM CarPhotoGallery WHERE CarId = @carId", connection);
                        galleryCommand.Parameters.AddWithValue("@carId", car.CarId);
                        using (var galleryReader = galleryCommand.ExecuteReader())
                        {
                            while (galleryReader.Read())
                            {
                                car.PhotoGallery.Add(galleryReader["PhotoUrl"].ToString());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при получении машин: {ex.Message}");
                    MessageBox.Show($"Ошибка при загрузке списка автомобилей: {ex.Message}", "Ошибка БД");
                }
            }
            return cars;
        }

        public Car AddCar(Car car)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                SqlTransaction transaction = null;
                try
                {
                    connection.Open();
                    transaction = connection.BeginTransaction();

                    // 1. Добавляем основную информацию о машине
                    //    OUTPUT INSERTED.CarId вернет нам ID новой машины
                    var command = new SqlCommand(
                        @"INSERT INTO Cars (DriverId, ModelName, LicensePlate, MainImageUrl, EngineInfo, InsuranceInfo) 
                          OUTPUT INSERTED.CarId 
                          VALUES (@DriverId, @ModelName, @LicensePlate, @MainImageUrl, @EngineInfo, @InsuranceInfo)",
                        connection, transaction);

                    command.Parameters.AddWithValue("@DriverId", car.DriverId);
                    command.Parameters.AddWithValue("@ModelName", car.ModelName);
                    command.Parameters.AddWithValue("@LicensePlate", car.LicensePlate);
                    command.Parameters.AddWithValue("@MainImageUrl", (object)car.MainImageUrl ?? DBNull.Value);
                    command.Parameters.AddWithValue("@EngineInfo", (object)car.EngineInfo ?? DBNull.Value);
                    command.Parameters.AddWithValue("@InsuranceInfo", (object)car.InsuranceInfo ?? DBNull.Value);

                    // Выполняем команду и получаем ID новой машины
                    car.CarId = (int)command.ExecuteScalar();

                    // 2. Добавляем фото из галереи (если они есть)
                    if (car.PhotoGallery != null && car.PhotoGallery.Count > 0)
                    {
                        foreach (var photoUrl in car.PhotoGallery)
                        {
                            var galleryCommand = new SqlCommand(
                                "INSERT INTO CarPhotoGallery (CarId, PhotoUrl) VALUES (@CarId, @PhotoUrl)",
                                connection, transaction);
                            galleryCommand.Parameters.AddWithValue("@CarId", car.CarId);
                            galleryCommand.Parameters.AddWithValue("@PhotoUrl", photoUrl);
                            galleryCommand.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                    return car; // Возвращаем машину с присвоенным ID
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при добавлении машины: {ex.Message}");
                    MessageBox.Show($"Ошибка при добавлении автомобиля: {ex.Message}", "Ошибка БД");
                    try { transaction?.Rollback(); } catch { }
                    return null; // Возвращаем null в случае ошибки
                }
            }
        }

        public bool UpdateCar(Car car)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                SqlTransaction transaction = null;
                try
                {
                    connection.Open();
                    transaction = connection.BeginTransaction();

                    // 1. Обновляем основную информацию
                    var command = new SqlCommand(
                        @"UPDATE Cars SET 
                            ModelName = @ModelName, 
                            LicensePlate = @LicensePlate, 
                            MainImageUrl = @MainImageUrl, 
                            EngineInfo = @EngineInfo, 
                            InsuranceInfo = @InsuranceInfo 
                          WHERE CarId = @CarId AND DriverId = @DriverId", // Добавили DriverId для безопасности
                        connection, transaction);

                    command.Parameters.AddWithValue("@CarId", car.CarId);
                    command.Parameters.AddWithValue("@DriverId", car.DriverId); // Убедимся, что водитель меняет свою машину
                    command.Parameters.AddWithValue("@ModelName", car.ModelName);
                    command.Parameters.AddWithValue("@LicensePlate", car.LicensePlate);
                    command.Parameters.AddWithValue("@MainImageUrl", (object)car.MainImageUrl ?? DBNull.Value);
                    command.Parameters.AddWithValue("@EngineInfo", (object)car.EngineInfo ?? DBNull.Value);
                    command.Parameters.AddWithValue("@InsuranceInfo", (object)car.InsuranceInfo ?? DBNull.Value);

                    int rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        // Машина не найдена или принадлежит другому водителю
                        transaction.Rollback();
                        return false;
                    }

                    // 2. Полностью перезаписываем галерею (проще, чем искать разницу)
                    // 2.1 Удаляем старые фото
                    var deleteGalleryCmd = new SqlCommand("DELETE FROM CarPhotoGallery WHERE CarId = @CarId", connection, transaction);
                    deleteGalleryCmd.Parameters.AddWithValue("@CarId", car.CarId);
                    deleteGalleryCmd.ExecuteNonQuery();

                    // 2.2 Добавляем новые фото
                    if (car.PhotoGallery != null && car.PhotoGallery.Count > 0)
                    {
                        foreach (var photoUrl in car.PhotoGallery)
                        {
                            var galleryCommand = new SqlCommand(
                                "INSERT INTO CarPhotoGallery (CarId, PhotoUrl) VALUES (@CarId, @PhotoUrl)",
                                connection, transaction);
                            galleryCommand.Parameters.AddWithValue("@CarId", car.CarId);
                            galleryCommand.Parameters.AddWithValue("@PhotoUrl", photoUrl);
                            galleryCommand.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении машины: {ex.Message}");
                    MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка БД");
                    try { transaction?.Rollback(); } catch { }
                    return false;
                }
            }
        }

        public bool DeleteCar(Car carToDelete) // Принимаем объект Car для безопасности (чтобы точно знать DriverId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    // Используем ON DELETE CASCADE, поэтому достаточно удалить машину из Cars
                    // Добавляем проверку DriverId, чтобы водитель не удалил чужую машину
                    var command = new SqlCommand("DELETE FROM Cars WHERE CarId = @CarId AND DriverId = @DriverId", connection);
                    command.Parameters.AddWithValue("@CarId", carToDelete.CarId);
                    command.Parameters.AddWithValue("@DriverId", carToDelete.DriverId);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0; // Вернет true, если машина была найдена и удалена
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении машины: {ex.Message}");
                    MessageBox.Show($"Ошибка при удалении автомобиля: {ex.Message}", "Ошибка БД");
                    return false;
                }
            }
        }
    }
}
