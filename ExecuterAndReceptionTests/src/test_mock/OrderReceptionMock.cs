using ExecuterAndReception.src.service.interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReceptionTests.src.test_mock
{
    public class OrderReceptionMock : IOrderReception
    {
        public Queue<byte[]> Orders = new Queue<byte[]>();

        public void AddOrder(byte[] orderBytes, byte[] orderBytesOCO)
        {
            Orders.Enqueue(orderBytes);
            if (orderBytesOCO.Length != 0)
            {
                Orders.Enqueue(orderBytesOCO);
            }
        }

        public void AddOrder(byte[] orderBytes)
        {
            Orders.Enqueue(orderBytes);
        }

        public byte[] NextOrder()
        {
            if (Orders.Count == 0)
            {
                throw new InvalidOperationException();
            }
            return Orders.Dequeue();
        }

        public byte[] NextOrderOCO()
        {
            if (Orders.Count == 0)
            {
                throw new InvalidOperationException();
            }
            return Orders.Dequeue();
        }

        public void Clear()
        {
            Orders.Clear();
        }
    }
}
