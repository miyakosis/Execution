using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ExecuterAndReception.src.service.experimental;
using ExecuterAndReception.src.service.interfaces;

namespace ExecuterAndReception.src.service
{
    internal class OrderReceptionServer
    {
        public const int SERVER_PORT = 10000;
        private const int SIZE_BUF = 1024;

        private IOrderReception _orderReception;

        internal OrderReceptionServer(IOrderReception orderReception)
        {
            _orderReception = orderReception;
        }

        internal void RunServer()
        {
            try
            { 
                TcpListener listener = new TcpListener(IPAddress.Any, SERVER_PORT);
                listener.Start();
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();

                    Thread thread = new Thread(processConnection);
                    thread.Start(client);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
        }

        internal void processConnection(Object obj)
        {
            TcpClient client = (TcpClient)obj;
            
            byte[] buf = new byte[SIZE_BUF];
            NetworkStream stream = client.GetStream();
            byte[] empty = new byte[0];

            int size;
            while ((size = stream.Read(buf, 0, SIZE_BUF)) > 0)
            {
                // TODO: validation
                
                // Executer の稼働している CPU の byteorder でデータを受け取る前提
                if (size == 30)
                {
                    byte[] order = new byte[30];
                    Buffer.BlockCopy(buf, 0, order, 0, 30);
                    _orderReception.AddOrder(order, empty);
                }
                else if (size == 60)
                {
                    byte[] order = new byte[30];
                    byte[] orderOCO = new byte[30];
                    Buffer.BlockCopy(buf, 0, order, 0, 30);
                    Buffer.BlockCopy(buf, 30, orderOCO, 0, 30);
                    _orderReception.AddOrder(order, orderOCO);
                }
                else
                {
                    Console.WriteLine("invalid order");
                }
            }
        }
    }
}
