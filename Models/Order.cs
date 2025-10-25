using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiWPF.Models
{
    public class Order
    {
        public int order_id { get; set; }
        public string PointA { get; set; } // Откуда
        public string PointB { get; set; } // Куда
        public string Status { get; set; }
        public string Tariff { get; set; }
        public decimal TotalPrice { get; set; }
        public Client OrderClient { get; set; } // Клиент, который сделал заказ
        public Driver AssignedDriver { get; set; } // Водитель, который принял заказ
    }
}
