using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiWPF.Models
{
    public class PaymentCard
    {
        public string CardNumber { get; set; }
        public string CardExpiry { get; set; }
        public string CardCVV { get; set; } // В БД не храним, используется только для транзакции

        // ==== ДОБАВЬ ЭТО СВОЙСТВО ====
        // "Visa **** 1234"
        public string MaskedName
        {
            get
            {
                if (string.IsNullOrEmpty(CardNumber) || CardNumber.Length < 4)
                    return "Новая карта";

                string prefix = "Карта";
                if (CardNumber.StartsWith("4")) prefix = "Visa";
                if (CardNumber.StartsWith("5")) prefix = "Mastercard";

                return $"{prefix} **** {CardNumber.Substring(CardNumber.Length - 4)}";
            }
        }
    }
}