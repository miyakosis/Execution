using ExecuterAndReception.src.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.service.interfaces
{
    public interface IOrderReception
    {
        public void AddOrder(byte[] orderBytes, byte[] orderBytesOCO);
        public byte[] NextOrder();
        public byte[] NextOrderOCO();
    }
}
