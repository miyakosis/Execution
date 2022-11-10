using ExecuterAndReception.src.model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.util
{
    public class OrderByteBuider
    {
        public int CustomerId { get; private set; }
        public int Sequence { get; private set; }
        public bool IsRealTime { get; private set; }

        public OrderByteBuider(int customerId = 0, int sequence = 1, bool isRealTime = false)
        {
            CustomerId = customerId;
            Sequence = sequence;
            IsRealTime = isRealTime;
        }

        public byte[] Build(
            long amount,
            int price,
            int type = OrderByteObject.TYPE_GTC,
            int processType = OrderByteObject.PROCESS_TYPE_ORDER)
        {
            byte[] order = new byte[30];

            Buffer.BlockCopy(BitConverter.GetBytes(CustomerId), 0, order, OrderByteObject.OFFSET_CUSTOMER_ID, OrderByteObject.SIZE_CUSTOMER_ID);
            Buffer.BlockCopy(BitConverter.GetBytes(Sequence), 0, order, OrderByteObject.OFFSET_ORDER_SEQUENCE, OrderByteObject.SIZE_ORDER_SEQUENCE);
            long time = IsRealTime ? DateTime.Now.Ticks : Sequence;
            Buffer.BlockCopy(BitConverter.GetBytes(time), 0, order, OrderByteObject.OFFSET_ITME, OrderByteObject.SIZE_TIME);
            Buffer.BlockCopy(BitConverter.GetBytes(amount), 0, order, OrderByteObject.OFFSET_AMOUNT, OrderByteObject.SIZE_AMOUNT);
            Buffer.BlockCopy(BitConverter.GetBytes(price), 0, order, OrderByteObject.OFFSET_PRICE, OrderByteObject.SIZE_PRICE);
            order[OrderByteObject.OFFSET_TYPE] = (byte)type;
            order[OrderByteObject.OFFSET_PROCESS_TYPE] = (byte)processType;

            Sequence += 1;
            return order;
        }

        public byte[] BuildCancel(int sequence)
        {
            byte[] order = new byte[30];

            Buffer.BlockCopy(BitConverter.GetBytes(CustomerId), 0, order, OrderByteObject.OFFSET_CUSTOMER_ID, OrderByteObject.SIZE_CUSTOMER_ID);
            Buffer.BlockCopy(BitConverter.GetBytes(sequence), 0, order, OrderByteObject.OFFSET_ORDER_SEQUENCE, OrderByteObject.SIZE_ORDER_SEQUENCE);
            long time = IsRealTime ? DateTime.Now.Ticks : Sequence;
            Buffer.BlockCopy(BitConverter.GetBytes(time), 0, order, OrderByteObject.OFFSET_ITME, OrderByteObject.SIZE_TIME);
            Buffer.BlockCopy(BitConverter.GetBytes(0L), 0, order, OrderByteObject.OFFSET_AMOUNT, OrderByteObject.SIZE_AMOUNT);
            // 本来はここで発注時の price も設定するのが望ましい
            Buffer.BlockCopy(BitConverter.GetBytes(0), 0, order, OrderByteObject.OFFSET_PRICE, OrderByteObject.SIZE_PRICE);
            order[OrderByteObject.OFFSET_TYPE] = (byte)OrderByteObject.TYPE_CANCEL;
            order[OrderByteObject.OFFSET_PROCESS_TYPE] = (byte)OrderByteObject.PROCESS_TYPE_CANCEL;

            return order;
        }

        public byte[] BuildTerminate()
        {
            byte[] order = new byte[30];
            order[OrderByteObject.OFFSET_PROCESS_TYPE] = (byte)OrderByteObject.PROCESS_TYPE_TERMINATE;

            return order;
        }
    }
}
