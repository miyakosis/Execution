using ExecuterAndReception.src.model;
using ExecuterAndReception.src.util;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.service.experimental
{
    internal class OrderReception
    {
        private CircularBufffer _orderList = new CircularBufffer(65536, OrderByteObject.SIZE_MESSAGE);
        private CircularBufffer _cancelOrderList = new CircularBufffer(2048, OrderByteObject.SIZE_MESSAGE);

        private int _waitTime = MIN_WAIT_TIME;
        private const int MIN_WAIT_TIME = 1;
        private const int MAX_WAIT_TIME = 1000;

        private byte[]? _teaminateOrder = null;
        private int _counter = 0;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddOrder(byte[] orderBytes, byte[] prderBytesOCO)
        {
            int type = OrderByteObject.ProcessType(orderBytes);
            switch (type)
            {
                case OrderByteObject.PROCESS_TYPE_TERMINATE:
                    _teaminateOrder = orderBytes;
                    return;

                case OrderByteObject.PROCESS_TYPE_CANCEL:
                    _cancelOrderList.Enqueue(orderBytes);
                    break;

                case OrderByteObject.PROCESS_TYPE_ORDER:
                    _orderList.Enqueue(orderBytes);
                    break;

                case OrderByteObject.PROCESS_TYPE_OCO:
                    _orderList.Enqueue(orderBytes);
                    _orderList.Enqueue(prderBytesOCO);
                    break;

                default:
                    // precondition failed.
                    // TODO: ログ出力して次の注文を処理する
                    break;
            }
        }

        internal Span<byte> nextOrder()
        {
            while (true)
            {
                Span<byte> order;
                // パフォーマンス計測のため、全注文の処理を終えてから終了するようにする
                // if (_teaminateOrder != null)
                // {
                //     return _teaminateOrder;
                // }
                if ((order = _cancelOrderList.Deque()) != null)
                {
                    _waitTime = MIN_WAIT_TIME;
                    return order;
                }
                if ((order = _orderList.Deque()) != null)
                {
                    // 通常の注文では _waitTime 初期化の代入命令も省略する
                    return order;
                }
                if (_teaminateOrder != null)
                {
                    Console.WriteLine($"counter: {_counter}");
                    return _teaminateOrder;
                }
                _counter += 1;
                Thread.Sleep(_waitTime);
                // _waitTime = Math.Min(_waitTime * 2, MAX_WAIT_TIME); // expnential backoff
            }
        }



        internal Span<byte> nextOrderOCO()
        {
            while (true)
            {
                Span<byte> order;
                if ((order = _orderList.Deque()) != null)
                {
                    return order;
                }
                Thread.Sleep(_waitTime);
                _waitTime = Math.Min(_waitTime * 2, MAX_WAIT_TIME); // expnential backoff
            }
        }
    }
}
