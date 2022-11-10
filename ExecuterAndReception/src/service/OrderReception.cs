using ExecuterAndReception.src.model;
using ExecuterAndReception.src.service.interfaces;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.service
{
    /// <summary>
    /// 注文受付を管理するクラス
    /// Producer-Consumer pattern で設計しており、AddOrder() が Producer、NextOrder() および NextOrderOCO() が Consumer である。
    /// </summary>
    internal class OrderReception : IOrderReception
    {
        private ConcurrentQueue<byte[]> _orderList = new ConcurrentQueue<byte[]>();
        private ConcurrentQueue<byte[]> _cancelOrderList = new ConcurrentQueue<byte[]>();

        private int _waitTime = MIN_WAIT_TIME;
        private const int MIN_WAIT_TIME = 1;
        private const int MAX_WAIT_TIME = 1000;

        private byte[]? _teaminateOrder = null;

        private int _analysisOrderCount = 0;
        private int _analysisWaitingCount = 0;

        /// <summary>
        /// 注文を追加する。
        /// このメソッドは多数の client から呼ばれる可能性があるため、同期制御する。
        /// </summary>
        /// <param name="orderBytes"></param>
        /// <param name="orderBytesOCO"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddOrder(byte[] orderBytes, byte[] orderBytesOCO)
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
                    _orderList.Enqueue(orderBytesOCO);
                    break;

                default:
                    // precondition failed.
                    // TODO: ログ出力して次の注文を処理する
                    Console.WriteLine($"assertion: unexpectd order");
                    break;
            }
        }


        /// <summary>
        /// 次の注文を注文リストから除去して返す。
        /// キャンセル注文を通常の注文より優先して返す。
        /// </summary>
        /// <returns>orderBytes</returns>
        public byte[] NextOrder()
        {
            while(true)
            {
                _analysisOrderCount += 1;
                byte[] order;
                if (_cancelOrderList.TryDequeue(out order))
                {
                    _waitTime = MIN_WAIT_TIME;
                    return order;
                }
                if (_orderList.TryDequeue(out order))
                {
                    // 通常の注文では _waitTime 初期化の代入命令も省略する
                    return order;
                }
                // パフォーマンス計測のため、全注文の処理を終えてから終了するようにする
                if (_teaminateOrder != null)
                {
                    Console.WriteLine($"OrderReception: order: {_analysisOrderCount} waiting: {_analysisWaitingCount}");
                    return _teaminateOrder;
                }
                _analysisWaitingCount += 1;
                Thread.Sleep(_waitTime);
                _waitTime = Math.Min(_waitTime * 2, MAX_WAIT_TIME); // expnential backoff
            }
        }

        /// <summary>
        /// NextOrder() はパフォーマンス計測用の仕様となっている。(終了命令を最後に返す)
        /// Production の仕様としては、こちらの実装のように終了命令が最優先となるべきである。
        /// </summary>
        /// <returns>orderBytes</returns>
        internal byte[] NextOrderForUsual()
        {
            while (true)
            {
                byte[] order;
                if (_teaminateOrder != null)
                {
                    return _teaminateOrder;
                }
                if (_cancelOrderList.TryDequeue(out order))
                {
                    _waitTime = MIN_WAIT_TIME;
                    return order;
                }
                if (_orderList.TryDequeue(out order))
                {
                    // 通常の注文では _waitTime 初期化の代入命令も省略する
                    return order;
                }
                Thread.Sleep(_waitTime);
                _waitTime = Math.Min(_waitTime * 2, MAX_WAIT_TIME); // expnential backoff
            }
        }

        /// <summary>
        /// OCO 注文用に、次のキャンセルではない注文を返す。
        /// </summary>
        /// <returns>orderBytes</returns>
        public byte[] NextOrderOCO()
        {
            while (true)
            {
                byte[] order;
                if (_orderList.TryDequeue(out order))
                {
                    // 通常の注文では _waitTime 初期化の代入命令も省略する
                    return order;
                }
                Thread.Sleep(_waitTime);
                _waitTime = Math.Min(_waitTime * 2, MAX_WAIT_TIME); // expnential backoff
            }
        }
    }
}
