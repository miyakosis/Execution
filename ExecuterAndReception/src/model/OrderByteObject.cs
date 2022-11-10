using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.model
{
    /**
     * 注文の操作を管理する。
     * 注文はオブジェクト化する方が望ましいが、パフォーマンスを考慮して 固定長の byte[] によって実装する。
     * このようにして扱うことで、シーケンシャルバッファにおいて連続した読み込み・書き込みを実現することができる。
     *   (TODO: アライメントのことを考慮すると構造体の方がアクセスが速い可能性あり)
     * 
     * このクラスは注文データを操作する関数を提供する。
     * 
     * 注文のフォーマット(バイト数 フィールド名 説明):
     * 8 id customer_id + customer_order_sequence であり、一意になるID
     *   4 customer_id 顧客のID
     *   4 customer_order_sequence 顧客ごとの板注文の連番。1からの昇順
     * 8 time 注文を受け付けた時刻
     * 8 amount 注文する暗号資産の量。0以上の値
     * 4 price 買値または売値(yen)。0以上の値
     * 1 type 注文タイプ
     * 1 execution_process_type 約定処理用タイプ
     * 
     * amount:
     * 単位を 10^-8 とする固定小数点数で表現する。すなわち amount が 1 の場合の注文の量は 0.00000001 である。
     * 
     * price:
     * 買い注文は負の値、売り注文は 0 または正の値とする。
     * 昇順でソートした際にそれぞれ最も高い買い注文、最も安い売り注文が先頭に来るようにするため。
     * 
     * type:
     * 1: GTC 注文 (FAS注文)
     * 2: IOC 注文 (FAK注文)
     * 3: FOK 注文
     * FAS 注文で一部約定した場合、注文の数量を変更して保持し続ける。
     * すなわち内部的には一つの注文 id に対して複数の約定が発生することがある。
     * 約定プロセスではこの部分の管理は行わず、締結プロセスでこれら複数の約定を区別して管理する。
     * 
     * execution_process_type:
     * Executer に対する処理のタイプ。
     * 255: Executer 自体を終了する
     * 0: 注文のキャンセル　(指定された id の注文をキャンセルする)
     * 1: 単独の注文
     * 2: OCO 注文
     * 
     * OCO注文について:
     * 注文を固定長 byte[] で表現するため、OCO 注文は二つの注文のセットで表現する。
     * 買い注文→売り注文の順番のセットとすること、それに伴い customer_order_sequence も連番になっていることを前提とする。
     * (パフォーマンスの向上を目的とし、この前提の下でコードを実装できるようにする)
     * 必ず二つセットとなっている必要があるため、もし execution_process_type: oco が設定されている注文が奇数個ある場合、データ不整合となる。
     * (このような前提を置いてしまうためデータ不整合に弱い実装となる。しかし注文は別プロセスで検証も可能であるため、不整合そちらで検出することとする)
     * 
     * execution_process_type と type は一つのバイトに統合もできるが、その場合は bit 演算が必要になり複雑になるため、パフォーマンスとのトレードオフもあるが分けておく設計にした。
     */
    public class OrderByteObject
    {
        public const int OFFSET_CUSTOMER_ID = 0;
        public const int SIZE_CUSTOMER_ID = 4;
        public const int OFFSET_ORDER_SEQUENCE = OFFSET_CUSTOMER_ID + SIZE_CUSTOMER_ID;
        public const int SIZE_ORDER_SEQUENCE = 4;
        public const int OFFSET_ITME = OFFSET_ORDER_SEQUENCE + SIZE_ORDER_SEQUENCE;
        public const int SIZE_TIME = 8;
        public const int OFFSET_AMOUNT = OFFSET_ITME + SIZE_TIME;
        public const int SIZE_AMOUNT = 8;
        public const int OFFSET_PRICE = OFFSET_AMOUNT + SIZE_AMOUNT;
        public const int SIZE_PRICE = 4;
        public const int OFFSET_TYPE = OFFSET_PRICE + SIZE_PRICE;
        public const int SIZE_TYPE = 1;
        public const int OFFSET_PROCESS_TYPE = OFFSET_TYPE + SIZE_TYPE;
        public const int SIZE_PROCESS_TYPE = 1;

        public const int OFFSET_ID = OFFSET_CUSTOMER_ID;
        public const int SIZE_ID = SIZE_CUSTOMER_ID + SIZE_ORDER_SEQUENCE;
        public const int SIZE_MESSAGE = OFFSET_PROCESS_TYPE + SIZE_PROCESS_TYPE;

        public const int TYPE_CANCEL = 0;
        public const int TYPE_GTC = 1;
        public const int TYPE_IOC = 2;
        public const int TYPE_FOK = 3;

        public const int PROCESS_TYPE_TERMINATE = 255;
        public const int PROCESS_TYPE_CANCEL = 0;
        public const int PROCESS_TYPE_ORDER = 1;
        public const int PROCESS_TYPE_OCO = 2;

        public static int ProcessType(byte[] boardOrder)
        {
            return boardOrder[OFFSET_PROCESS_TYPE];
        }
        public static int ProcessType(Span<byte> boardOrder)
        {
            return boardOrder[OFFSET_PROCESS_TYPE];
        }

        public static int Type(byte[] boardOrder)
        {
            return boardOrder[OFFSET_TYPE];
        }
        public static int Type(Span<byte> boardOrder)
        {
            return boardOrder[OFFSET_TYPE];
        }

        public static bool IsOCO(byte[] boardOrder)
        {
            return (boardOrder[OFFSET_PROCESS_TYPE]) == PROCESS_TYPE_OCO;
        }
        public static bool IsOCO(Span<byte> boardOrder)
        {
            return (boardOrder[OFFSET_PROCESS_TYPE]) == PROCESS_TYPE_OCO;
        }

        public static long Id(byte[] boardOrder)
        {
            return BitConverter.ToInt64(boardOrder, OFFSET_CUSTOMER_ID);
        }
        public static long Id(Span<byte> boardOrder)
        {
            return BitConverter.ToInt64(boardOrder.Slice(OFFSET_ID, SIZE_ID));
        }

        public static long Amount(byte[] boardOrder)
        {
            return BitConverter.ToInt64(boardOrder, OFFSET_AMOUNT);
        }
        public static long Amount(Span<byte>boardOrder)
        {
            return BitConverter.ToInt64(boardOrder.Slice(OFFSET_AMOUNT, SIZE_AMOUNT));
        }

        public static int Price(byte[] boardOrder)
        {
            return BitConverter.ToInt32(boardOrder, OFFSET_PRICE);
        }
        public static int Price(Span<byte> boardOrder)
        {
            return BitConverter.ToInt32(boardOrder.Slice(OFFSET_PRICE, SIZE_PRICE));
        }


        public static long  ToId(int customerId, int sequence)
        {
            // little endian
            return ((long)sequence) << 32 | (long)customerId;
        }
    }
}
