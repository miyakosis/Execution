using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.model
{
    /// <summary>
    /// 約定結果を保持するオブジェクト
    /// </summary>
    public class Execution
    {
        public long OrderId { get; init; }
        public long BoardOrderId { get; init; }
        public long Amount { get; init; }

        /**
         * 約定時の板にある注文の価格。そのため
         * 正の値: この値で OrderId 側が売り、BoardOrderId 側が買い
         * 0 または負の値: 絶対値の値で OrderId 側が買い、BoardOrderId 側が売り
         */
        public int Price { get; init; }


        public Execution(long orderId, long boardOrderId, long amount, int price)
        {
            OrderId = orderId;
            BoardOrderId = boardOrderId;
            Amount = amount;
            Price = price;
        }

        public override bool Equals(object? obj)
        {
            if (obj is Execution)
            {
                var target = (Execution)obj;
                return target.OrderId == OrderId &&
                    target.BoardOrderId == BoardOrderId &&
                    target.Amount == Amount;
            }
            else
            {
                return false;
            }
        }
    }
}
