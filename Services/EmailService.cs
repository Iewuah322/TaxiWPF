using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;
using System.Windows; // Для MessageBox в заглушке

namespace TaxiWPF.Services
{
    public class EmailService
    {
        // ВАЖНО: Замените на свои реальные данные, полученные от Google
        private const string FromEmail = "tyttagame@gmail.com";
        private const string FromName = "TaxiWPF";
        private const string AppPassword = "ruhk nczt tkpe odqo"; // Пароль приложения
        private const string SmtpServer = "smtp.gmail.com";
        private const int SmtpPort = 587;

        public async Task SendPasswordResetEmailAsync(string toEmail, string token)
        {
            // --- ЗАГЛУШКА ---
            // Вместо реальной отправки просто покажем MessageBox с токеном.
            // Это позволит вам тестировать без настройки почты.
            MessageBox.Show($"Письмо для сброса пароля отправлено на {toEmail}.\nВаш токен: {token}", "Email Service (Заглушка)");
            await Task.CompletedTask; // Имитируем асинхронную операцию
            // ------------------

            /*
            // --- РЕАЛЬНЫЙ КОД ДЛЯ ОТПРАВКИ (раскомментировать, когда будет доступ) ---
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(FromName, FromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = "Восстановление пароля в TaxiWPF";
            message.Body = new TextPart("plain")
            {
                Text = $"Здравствуйте!\n\nДля сброса пароля используйте следующий токен в приложении:\n\n{token}\n\nЕсли вы не запрашивали сброс пароля, просто проигнорируйте это письмо."
            };

            try
            {
                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(SmtpServer, SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(FromEmail, AppPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Ошибка отправки письма: " + ex.Message);
            }
            */
        }
    }
}
