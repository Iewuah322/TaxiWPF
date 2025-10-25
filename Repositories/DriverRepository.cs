using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiWPF.Models;
using System.Configuration;

namespace TaxiWPF.Repositories
{
    public class DriverRepository
    {
        private readonly string _connectionString;

        public DriverRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaxiDB"].ConnectionString;
        }

        
        public List<Driver> GetAllDrivers()
        {
            var drivers = new List<Driver>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand("SELECT * FROM Driver", connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        drivers.Add(new Driver
                        {
                            driver_id = (int)reader["driver_id"],
                            full_name = reader["full_name"].ToString(),
                            car_model = reader["car_model"].ToString(),
                            license_plate = reader["license_plate"].ToString(),
                            status = reader["status"].ToString(),
                            geo_position = reader["geo_position"] as string,
                            rating = reader["rating"] as decimal?,
                            phone = reader["phone"].ToString()
                        });
                    }
                }
            }
            return drivers;
        }

        public void AddDriver(Driver driver)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand(
                    @"INSERT INTO Driver (full_name, car_model, license_plate, status, geo_position, rating, phone) 
                      VALUES (@full_name, @car_model, @license_plate, @status, @geo_position, @rating, @phone)", connection);

                command.Parameters.AddWithValue("@full_name", driver.full_name);
                command.Parameters.AddWithValue("@car_model", driver.car_model);
                command.Parameters.AddWithValue("@license_plate", driver.license_plate);
                command.Parameters.AddWithValue("@status", driver.status);
                command.Parameters.AddWithValue("@geo_position", driver.geo_position ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@rating", driver.rating ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@phone", driver.phone);

                command.ExecuteNonQuery();
            }
        }

        public void UpdateDriver(Driver driver)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand(
                    @"UPDATE Driver SET 
                        full_name = @full_name, 
                        car_model = @car_model, 
                        license_plate = @license_plate, 
                        status = @status, 
                        geo_position = @geo_position, 
                        rating = @rating, 
                        phone = @phone 
                      WHERE driver_id = @driver_id", connection);

                command.Parameters.AddWithValue("@driver_id", driver.driver_id);
                command.Parameters.AddWithValue("@full_name", driver.full_name);
                command.Parameters.AddWithValue("@car_model", driver.car_model);
                command.Parameters.AddWithValue("@license_plate", driver.license_plate);
                command.Parameters.AddWithValue("@status", driver.status);
                command.Parameters.AddWithValue("@geo_position", driver.geo_position ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@rating", driver.rating ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@phone", driver.phone);

                command.ExecuteNonQuery();
            }
        }

        
        public void DeleteDriver(int driverId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand("DELETE FROM Driver WHERE driver_id = @driver_id", connection);
                command.Parameters.AddWithValue("@driver_id", driverId);
                command.ExecuteNonQuery();
            }
        }
    }
}
