using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.model
{
    /// <summary>
    /// キャンセルされた注文を保持するオブジェクト
    /// </summary>
    public class CanceledOrder
    {
        public const int REASON_CANCEL_ORDER = 0;
        public const int REASON_IOC = 2;
        public const int REASON_FOK = 3;

        public long OrderId { get; init; }
        public int Reason { get; init; }

        public CanceledOrder(long orderId, int reason)
        {
            OrderId = orderId;
            Reason = reason;
        }

        public override bool Equals(object? obj)
        {
            if (obj is CanceledOrder)
            {
                var target = (CanceledOrder)obj;
                return target.OrderId == OrderId &&
                    target.Reason == Reason;
            }
            else
            {
                return false;
            }
        }
    }
}
