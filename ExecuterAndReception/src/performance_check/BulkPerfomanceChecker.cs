using ExecuterAndReception.src.model;
using ExecuterAndReception.src.service;
using ExecuterAndReception.src.util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ExecuterAndReception.src.service.ExecutionProcesser;

namespace ExecuterAndReception.src.performance_check
{
    /// <summary>
    /// 速度計測のための Main クラス
    /// </summary>
    internal class BulkPerfomanceChecker
    {
        public const int WAIT_MS_TO_ENTRY_CANCEL_ORDER = 1000;

        private static bool _isOutputDetail = false;    // default false
        private static int _nOrder = 100;   // default 100

        /// <summary>
        /// args[0]: true が指定されている場合、約定の詳細を表示する
        /// args[1]: 投入する注文の数。この数に加えて +10% のキャンセル注文が投入される。
        /// 
        /// e.g. ExecuterAndReception.exe false 1000000
        /// order は 最大 int32.MaxValue * 0.9 まで。
        /// (sequence が int32 としているため)
        /// これを超えるには customer_id を変えての投入や、メモリに注文を一度ため込む仕組みなどの修正を行う必要がある。
        /// 
        /// 詳細表示表示内容について：
        /// 2022/10/27 22:08:13  00000000:00000010 S 00000000:00000005 11366 48.00
        /// → Sequence:0x10 の売り注文が 0x05 の買い注文(価格:11366, 数量: 48) と約定した
        /// 
        /// 2022/10/27 22:08:13  00000000:00000001 C 3
        /// → Sequence:0x01 の注文が Reason:3(FOK) でキャンセルされた
        ///    (Reason の詳細は OrderByteObject class に定義)
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Console.WriteLine("usage: ExecuterAndReception.exe {detail = true/false} {number of order= int}");
            if (args.Length > 0)
            {
                _isOutputDetail = bool.Parse(args[0]);
            }
            if (args.Length > 1)
            {
                _nOrder = int.Parse(args[1]);
            }

            var orderReception = new OrderReception();
            var executionProcesser = new ExecutionProcesser(_isOutputDetail);
            var executer = new Executer(orderReception, executionProcesser);

            Console.WriteLine($"preparing: {DateTime.Now} number of orders: {_nOrder}");
            var orderByteBuilder = new OrderByteBuider(0, 1, true);
            CreateOrders(orderReception, orderByteBuilder);
            Console.WriteLine($"process started: {DateTime.Now}");

            var sw = new Stopwatch();
            sw.Start();

            var executerThread = new Thread(new ThreadStart(executer.Run));
            var executionProcesserThread = new Thread(new ThreadStart(executionProcesser.Run));

            executionProcesserThread.Start();
            executerThread.Start();

            // 一定時間をおいてキャンセル命令を投入し始める
            Thread.Sleep(WAIT_MS_TO_ENTRY_CANCEL_ORDER);
            CreateCancelOrders(orderReception, orderByteBuilder);
            orderReception.AddOrder(orderByteBuilder.BuildTerminate(), new byte[0]);

            executerThread.Join();
            sw.Stop();
            executionProcesserThread.Join();

            Console.WriteLine($"execution elapsed: {sw.Elapsed}");
        }

        private static void CreateOrders(OrderReception orderReception, OrderByteBuider orderByteBuilder)
        {
            var r = new Random();
            for (int i = 0; i < _nOrder; ++i)
            {
                // 以下の割合で発注する
                // 35% で指値買い注文
                // 35% で指値売り注文
                // 10% で成行買い注文
                // 10% で成行売り注文
                // 10% でOCO 注文(売り・買いともに指値)
                // 指値の場合、80% で GTC、10% で IOC、10% で FOK 注文とする             
                // 成行の場合、50% で IOC、50% で FOK 注文とする             
                // amount は 1.0 * 10^8 ～ 100.0 * 10^8 の一様乱数とする
                long amount = (r.Next(100) + 1) * 10000000;

                byte[] order;
                byte[] orderOCO = new byte[0];

                // 10000 から + 25 % ～ -25% を base price とする。
                double basePrice = 10000 + 2500 * Math.Sin(2 * Math.PI * i / _nOrder);

                int n = r.Next(100);
                if (n < 35)
                {   // buy
                    // 指値について、base price から買い注文は -10% ～ +5%、売り注文は -5% ～ +10% の指値とする(いずれも一様乱数)
                    double priceValue = basePrice * (0.9 + 0.15 * r.NextDouble());
                    int price = (int)(-1 * priceValue);
                    int type = GetTypeForLimitPrice(r);

                    order = orderByteBuilder.Build(amount, price, type);
                }
                else if (n < 70)
                {   // sell
                    double priceValue = basePrice * (0.95 + 0.15 * r.NextDouble());
                    int price = (int)(priceValue);
                    int type = GetTypeForLimitPrice(r);

                    order = orderByteBuilder.Build(amount, price, type);
                }
                else if (n < 80)
                {   // 成行 buy
                    int price = -1 * 20000;
                    int type = GetTypeForMarcketPrice(r);

                    order = orderByteBuilder.Build(amount, price, type);
                }
                else if (n < 90)
                {   // 成行 sell
                    int price = 0;
                    int type = GetTypeForMarcketPrice(r);

                    order = orderByteBuilder.Build(amount, price, type);
                }
                else
                {
                    // OCO の指値について、base price から買い注文は -3%、売り注文は +3% の指値とする
                    int buyPrice = (int)(basePrice * -0.97);
                    order = orderByteBuilder.Build(amount, buyPrice, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO);

                    int sellPrice = (int)(basePrice * 1.03);
                    long amountOCO = (r.Next(100) + 1) * 10000000;
                    orderOCO = orderByteBuilder.Build(amount, sellPrice, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO);
                }
                orderReception.AddOrder(order, orderOCO);
                if (_isOutputDetail)
                {
                    PrintOrder(order);
                    if (orderOCO.Length > 0)
                    {
                        PrintOrder(orderOCO);
                    }
                }
            }
        }

        private static int GetTypeForLimitPrice(Random r)
        {
            int typeProb = r.Next(10);
            int type;
            if (typeProb < 8)
            {
                type = OrderByteObject.TYPE_GTC;
            }
            else if (typeProb == 8)
            {
                type = OrderByteObject.TYPE_IOC;
            }
            else
            {
                type = OrderByteObject.TYPE_FOK;
            }
            return type;
        }

        private static int GetTypeForMarcketPrice(Random r)
        {
            return  (r.Next(2) == 0) ? OrderByteObject.TYPE_IOC : OrderByteObject.TYPE_FOK;
        }
        
        private static void PrintOrder(byte[] orderBytes)
        {
            var dt = DateTime.Now;
            var sb = new StringBuilder();
            sb.Append(dt);
            sb.Append($" {ToDisplay(OrderByteObject.Id(orderBytes))}");

            var price = OrderByteObject.Price(orderBytes);
            var amountString = ((double)OrderByteObject.Amount(orderBytes) / 10000000).ToString("F2");
            if (price < 0)
            {
                sb.Append($" B {-1 * price} {amountString}");
            }
            else
            {
                sb.Append($" S {-1 * price} {amountString}");
            }
            switch(OrderByteObject.Type(orderBytes))
            {
                case OrderByteObject.TYPE_GTC:
                    sb.Append(" GTC");
                    break;
                case OrderByteObject.TYPE_IOC:
                    sb.Append(" IOC");
                    break;
                case OrderByteObject.TYPE_FOK:
                    sb.Append(" FOK");
                    break;
            }
            if (OrderByteObject.IsOCO(orderBytes))
            {
                sb.Append(" OCO");
            }
            Console.WriteLine(sb.ToString());
        }

        private static string ToDisplay(long id)
        {
            // little endian なので long の後半4バイトが customer_id, 前半4バイトが sequence
            int customer_id = (int)id;
            int sequence = (int)(id >> 32);
            return customer_id.ToString("x8") + ":" + sequence.ToString("x8");
        }


        private static void CreateCancelOrders(OrderReception orderReception, OrderByteBuider orderByteBuilder)
        {
            // 全体の 10 % の注文をキャンセルする
            // キャンセル注文は優先処理されるため、約定処理開始後に投入する
            var r = new Random();
            for (int i = 0; i < _nOrder / 10; ++i)
            {
                int sequence = r.Next(_nOrder) + 1;
                byte[] order = orderByteBuilder.BuildCancel(sequence);

                byte[] orderOCO = new byte[0];
                orderReception.AddOrder(order, orderOCO);
                // Thread.Sleep(1);
            }
        }

        private static void CreateTerminateOrder(OrderReception orderReception)
        {
            // terminate message
            byte[] teaminateMessage = new byte[30];
            teaminateMessage[29] = (byte)0xff;
            orderReception.AddOrder(teaminateMessage, new byte[0]);
        }
    }
}
