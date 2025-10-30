﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TaxiWPF.Converters
{
    public class RelativePathToAbsoluteConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверяем, что нам передали непустой путь (string)
            if (value is string relativePath && !string.IsNullOrEmpty(relativePath))
            {
                try
                {
                    // Соединяем базовую директорию .exe с относительным путем
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string fullPath = Path.Combine(baseDirectory, relativePath);

                    // Если файл существует, создаем и возвращаем изображение
                    if (File.Exists(fullPath))
                    {
                        return new BitmapImage(new Uri(fullPath));
                    }
                }
                catch
                {
                    // В случае ошибки возвращаем null
                    return null;
                }
            }
            // Если путь пустой или неверный, не показываем ничего
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
