using ExecuterAndReception.src.model;
using ExecuterAndReception.src.service.interfaces;
using ExecuterAndReception.src.util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WBTrees;

namespace ExecuterAndReception.src.service
{
    /// <summary>
    ///  約定を行うプロセス
    /// </summary>
    public class Executer : IExecution
    {
        // 注文受付プロセス
        private IOrderReception _orderReception;
        // 約定後処理プロセス
        private IExecutionProcesser _executionProcesser;

        // Object pool
        private ObjectPool<BoardPrice> _boardPricePool = new ObjectPool<BoardPrice>(new BoardPrice());
        private ObjectPool<Order> _orderPool = new ObjectPool<Order>(new Order());

        // 買い注文板
        // weight-balanced tree を用いて、買い価格は負の値で格納することで、最高買い価格から順に探索できるようにする
        // sentinel として key = int32.MaxValue の BoardPrice を格納しておく
        // key: price, value: 板価格オブジェクト
        private WBMap<int, BoardPrice> _buyBoardPrices = new WBMap<int, BoardPrice>();
        // 売り注文板 key: price, value: 板価格オブジェクト
        // weight-balanced tree を用いて、売り価格は正の値で格納することで、最低売り価格から順に探索できるようにする
        // sentinel として key = int32.MaxValue の BoardPrice を格納しておく
        // key: price, value: 板価格オブジェクト
        private WBMap<int, BoardPrice> _sellBoardPrices = new WBMap<int, BoardPrice>();

        // 買い注文約定可能判定
        private IExecutableChecker _buyExecutableChecker = new BuyOrderExecutableChecker();
        // 売り注文約定判定
        private IExecutableChecker _sellExecutableChecker = new SellOrderExecutableChecker();
        
        // 全注文 Dictionary
        // 注文キャンセルの探索に使用する。
        // key: OrderId, value: 注文オブジェクト
        private Dictionary<long, Order> _allOrders = new Dictionary<long, Order>();

        // 内部的な約定回数カウンタ。OCO 注文処理で片方の注文が約定したかどうかの判定に使用する。
        private long _executionCounter = 0;


        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="orderReception">注文受付プロセス</param>
        /// <param name="executionProcesser">約定後処理プロセス</param>
        public Executer(IOrderReception orderReception, IExecutionProcesser executionProcesser)
        {
            _orderReception = orderReception;
            _executionProcesser = executionProcesser;

            // add Sentinel
            var sentinel = new BoardPrice();
            sentinel.Init(Int32.MaxValue, null);
            _buyBoardPrices.Add(Int32.MaxValue, sentinel);
            _sellBoardPrices.Add(Int32.MaxValue, sentinel);
        }

        /// <summary>
        /// 約定処理を行うメインループ
        /// </summary>
        public void Run()
        {
            // TODO: ここでメンテナンス明けや障害復旧時などの板寄せの処理を実施する

            // 以降ザラ場処理
            while (true)
            {
                byte[] orderBytes = _orderReception.NextOrder();
                int type = OrderByteObject.ProcessType(orderBytes);
                switch (type)
                {
                    case OrderByteObject.PROCESS_TYPE_TERMINATE:
                        _executionProcesser.Ternimate();
                        return;

                    case OrderByteObject.PROCESS_TYPE_CANCEL:
                        long orderId = OrderByteObject.Id(orderBytes);
                        Cancel(orderId);
                        break;

                    case OrderByteObject.PROCESS_TYPE_ORDER:
                        ProcessOrder(orderBytes);
                        break;

                    case OrderByteObject.PROCESS_TYPE_OCO:
                        ProcessOrderOCO(orderBytes);
                        break;

                    default:
                        // precondition failed: unexpected order.
                        // TODO: ログ出力して次の注文を処理する
                        break;                        
                }
            }
        }

        /// <summary>
        /// 注文キャンセルする
        /// </summary>
        /// <param name="orderId">キャンセル対象の orderId</param>
        private void Cancel(long orderId)
        {
            if (RemoveOrder(orderId))
            {
                _executionProcesser.Canceled(new CanceledOrder(orderId, CanceledOrder.REASON_CANCEL_ORDER));
            }
        }

        /// <summary>
        /// 板にある Order を削除する。
        /// 同一価格(BoardPrice)の注文リストにおいて、キャンセル対象の注文は以下のパターンがある。
        /// * 注文リストの先頭である
        /// * 途中である
        /// * 末尾である
        /// * 先頭かつ末尾である(= この価格にはキャンセル対象の注文のみ = BoardPrice も削除する)
        /// </summary>
        /// <param name="orderId">キャンセル対象の orderId</param>
        /// <param name="isOCO">true: この Order が OCO 注文かどうかを判定し、そうであればもう一方の Order を削除する false: この Order が OCO 注文の他方であることが分かっているため、OCOチェックを行わない</param>
        /// <returns>true: 注文のキャンセルを実行 false: 該当注文が存在しない</returns>
        private bool RemoveOrder(long orderId, bool isRemoveIfOCO = true)
        {
            Order boardOrder; 
            if (_allOrders.TryGetValue(orderId, out boardOrder) == false)
            {   // OCO 注文のキャンセルの場合やタイミング等の理由で約定済み注文にキャンセル注文が発行された場合
                // TODO: log 出力 or キャンセル不能メッセージの送出を検討
                return false;
            }
            if (boardOrder.Previous == null && boardOrder.Next == null)
            {   // 先頭かつ末尾である(= この価格にはキャンセル対象の注文のみ)
                // 注文価格自体を削除する
                int price = OrderByteObject.Price(boardOrder.OrderBytes);
                if (price < 0)
                {   // buy
                    _buyBoardPrices.Remove(price);
                }
                else
                {   // sell
                    _sellBoardPrices.Remove(price);
                }

                _boardPricePool.Back(boardOrder.BoardPrice);
            }
            else if (boardOrder.Previous == null)
            {
                boardOrder.Next.Previous = null;
                boardOrder.BoardPrice.Head = boardOrder.Next;
            }
            else if (boardOrder.Next == null)
            {
                boardOrder.Previous.Next = null;
                boardOrder.BoardPrice.Tail = boardOrder.Previous;
            }
            else
            {   // 前の注文と次の注文をつなげる。
                boardOrder.Previous.Next = boardOrder.Next;
                boardOrder.Next.Previous = boardOrder.Previous;
            }
            _orderPool.Back(boardOrder);

            _allOrders.Remove(orderId);

            if (isRemoveIfOCO)
            {
                RemovePairOrderIfOCO(boardOrder);
            }
            return true;
        }

        /// <summary>
        /// Order が OCO 注文かどうかを判定し、OCO注文の場合はペアとなる注文を板から削除する
        /// </summary>
        /// <param name="order">キャンセル対象の Order</param>
        private void RemovePairOrderIfOCO(Order order)
        {
            if (OrderByteObject.IsOCO(order.OrderBytes))
            {
                long id = OrderByteObject.Id(order.OrderBytes);
                int price = OrderByteObject.Price(order.OrderBytes);

                // 買い注文→売り注文の順番で id が振られている前提である
                // id は little endian なので sequence は 1 << 32 の増減
                long orderIdOCO = (price < 0) ? id + (1L << 32) : id - (1L << 32);

                RemoveOrder(orderIdOCO, false);
            }
        }

        /// <summary>
        /// 注文を処理する
        /// </summary>
        /// <param name="orderBytes">orderBytes</param>
        private void ProcessOrder(byte[] orderBytes)
        {            
            int price = OrderByteObject.Price(orderBytes);
            if (price < 0)
            {   // buy
                Execute(orderBytes, price, _sellBoardPrices, _buyExecutableChecker, _buyBoardPrices);
            }
            else
            {   // sell
                Execute(orderBytes, price, _buyBoardPrices, _sellExecutableChecker, _sellBoardPrices);
            }
        }

        /// <summary>
        /// 注文を処理する。
        /// 注文の価格と、最安値の売り注文 or 最高値の買い注文から順番に価格が合致するかをチェックしていく。
        /// 値段が合致しない場合は、その注文を板に追加する。
        /// </summary>
        /// <param name="orderBytes">orderBytes</param>
        /// <param name="price">注文の price</param>
        /// <param name="boardPrices">買い注文の場合は売り注文板、売り注文の場合は買い注文板</param>
        /// <param name="executableChecker">買い注文の場合は買い注文約定可能チェッカー、売り注文の場合は売り注文約定可能チェッカー</param>
        /// <param name="sameSideBoardPrices">買い注文の場合は買い注文板、売り注文の場合は売り注文板</param>
        private void Execute(byte[] orderBytes, int price, WBMap<int, BoardPrice> boardPrices, IExecutableChecker executableChecker, WBMap<int, BoardPrice> sameSideBoardPrices)
        {
            long orderId = OrderByteObject.Id(orderBytes);
            long amount = OrderByteObject.Amount(orderBytes);
            List<Execution> executions = new List<Execution>();
            List<Order> removeOrders = new List<Order>();
               
            // 板の注文を先頭から順番に突き合わせていく
            for (int bpIdx = 0;; ++bpIdx)
            {
                var boardPrice = boardPrices.GetAt(bpIdx).Item.Value;
                if (boardPrice.IsSentinel() || executableChecker.IsExecutable(price, boardPrice.Price) == false)
                {
                    // 今回の注文価格が板にある価格と合致しなくなったのでここまでの約定結果を確定させる
                    FinishExecuion(
                        orderBytes,
                        orderId,
                        price,
                        amount,
                        executions,
                        removeOrders,
                        boardPrices,
                        boardPrice.Price,
                        sameSideBoardPrices);
                    return;
                }

                // 今回の注文に対して、この価格の板の注文を順番に突き合わせていく
                for (var boardOrder = boardPrice.Head; boardOrder != null; boardOrder = boardOrder.Next)
                {
                    if (amount < boardOrder.Amount)
                    {   // 今回の注文の数量が板の注文より少ない
                        // 今回の注文を約定し、板の注文の数量を減らす
                        executions.Add(
                            new Execution(
                                orderId,
                                OrderByteObject.Id(boardOrder.OrderBytes),
                                amount,
                                boardPrice.Price
                           ));

                        // 数量を減少させて板の注文を残す
                        boardOrder.Amount -= amount;
                        Order nextHead = boardOrder;
                        // TODO: 要検討：boardOrder が IOC の場合、残りをキャンセルする必要があるか?
                        // (板に登録される注文は FAS である必要があり、FAK ではないという前提がありえるか?)

                        CommitExecutions(executions, removeOrders, boardPrices, boardPrice.Price, boardPrice, nextHead);
                        return;
                    }
                    else if (amount == boardOrder.Amount)
                    {   // 今回の注文と板の注文の数量が一致している
                        executions.Add(
                            new Execution(
                                orderId,
                                OrderByteObject.Id(boardOrder.OrderBytes),
                                amount,
                                boardPrice.Price
                           ));

                        removeOrders.Add(boardOrder);

                        Order? nextHead = boardOrder.Next;
                        if (nextHead != null)
                        {
                            CommitExecutions(executions, removeOrders, boardPrices, boardPrice.Price, boardPrice, boardOrder.Next);
                        }
                        else
                        {   // この price に order が残っていないため、price 自体を削除するように指定する
                            var nextBoardPrice = boardPrices.GetAt(bpIdx + 1).Item.Value;
                            CommitExecutions(executions, removeOrders, boardPrices, nextBoardPrice.Price);
                        }

                        return;
                    }
                    else
                    {   // 今回の注文の数量が板の注文より多い
                        // 板の注文を約定し、次の板注文とつきあわせる
                        executions.Add(
                            new Execution(
                                orderId,
                                OrderByteObject.Id(boardOrder.OrderBytes),
                                boardOrder.Amount,
                                boardPrice.Price
                           ));

                        removeOrders.Add(boardOrder);

                        amount -= boardOrder.Amount;
                    }
                }
                // この価格の注文が全て約定対象となったので、板にある次の価格の注文を見に行く
            }
        }

        /// <summary>
        /// ここまでの約定結果を確定できるかチェックし、必要であれば今回の注文を板に追加する。
        /// </summary>
        /// <param name="orderBytes">orderBytes</param>
        /// <param name="orderId">orderId</param>
        /// <param name="price">注文の price</param>
        /// <param name="amount">注文の(約定していない残りの) amount</param>
        /// <param name="executions">ここまでの約定結果</param>
        /// <param name="removeBoardOrders">約定が確定した場合に、板から除外される板の Order</param>
        /// <param name="boardPrices">買い注文の場合は売り注文板、売り注文の場合は買い注文板</param>
        /// <param name="bpIdxUpToRemove">約定が確定した場合に、板から除外される BoardPrice の index</param>
        /// <param name="sameSideBoardPrices">買い注文の場合は買い注文板、売り注文の場合は売り注文板</param>
        private void FinishExecuion(
            byte[] orderBytes,
            long orderId,
            int price,
            long amount,
            List<Execution> executions,
            List<Order> removeBoardOrders,
            WBMap<int, BoardPrice> boardPrices,
            int boardPriceUpToRemove,
            WBMap<int, BoardPrice> sameSideBoardPrices
            )
        {
            int type = OrderByteObject.Type(orderBytes);
            if (type == OrderByteObject.TYPE_FOK)
            {   // Kill
                _executionProcesser.Canceled(new CanceledOrder(orderId, CanceledOrder.REASON_FOK));
            }
            else if (type == OrderByteObject.TYPE_IOC)
            {   //  現時点までの約定を確定し、残りの注文を Cancel
                if (executions.Count > 0)
                {
                    CommitExecutions(executions, removeBoardOrders, boardPrices, boardPriceUpToRemove);
                }
                else
                {
                    _executionProcesser.Canceled(new CanceledOrder(orderId, CanceledOrder.REASON_IOC));
                }
            }
            else
            {   // // 現時点までの約定を確定し、残りの注文を板に残す
                if (executions.Count > 0)
                {
                    CommitExecutions(executions, removeBoardOrders, boardPrices, boardPriceUpToRemove);
                }

                var newOrder = _orderPool.Borrow();
                newOrder.Init(orderBytes, amount);

                if (sameSideBoardPrices.ContainsKey(price))
                {   // 同じ価格の boardPrice があるため、その末尾に boardOrder を追加するのみ
                    BoardPrice addBoardPrice = sameSideBoardPrices[price];
                    addBoardPrice.AddOrder(newOrder);
                    newOrder.BoardPrice = addBoardPrice;
                }
                else
                {   // 同じ価格の boardPrice がないので、その価格の boardPrice を作成
                    var newBoardPrice = _boardPricePool.Borrow();
                    newBoardPrice.Init(price, newOrder);
                    newOrder.BoardPrice = newBoardPrice;

                    sameSideBoardPrices[price] = newBoardPrice;
                }

                _allOrders[orderId] = newOrder;
            }
        }

        /// <summary>
        /// 約定を確定する。
        /// 約定した板の Order および BoardPrice を削除し、約定メッセージを約定後処理プロセスに送る。
        /// precondition: executions.Count > 0
        /// </summary>
        /// <param name="executions">ここまでの約定結果</param>
        /// <param name="removeBoardOrders">約定が確定した場合に、板から除外される板の Order</param>
        /// <param name="boardPrices">買い注文の場合は売り注文板、売り注文の場合は買い注文板</param>
        /// <param name="bpIdxUpToRemove">約定が確定した場合に、板から除外される BoardPrice の index</param>
        /// <param name="boardPrice">約定が確定した場合に Order のメンテナンスを行う必要のある BoardPrice。その必要がない場合は null</param>
        /// <param name="nextHead">約定が確定した場合に BoardPrice の先頭となる Order。その必要がない場合は null</param>
        private void CommitExecutions(
            List<Execution> executions,
            List<Order> removeBoardOrders,
            WBMap<int, BoardPrice> boardPrices,
            int boardPriceUpToRemove,
            BoardPrice? boardPrice = null,
            Order? nextHead = null)
        {
            foreach (var boardOrder in removeBoardOrders)
            {
                _orderPool.Back(boardOrder);
                _allOrders.Remove(OrderByteObject.Id(boardOrder.OrderBytes));

                RemovePairOrderIfOCO(boardOrder);
            }

            if (boardPrice != null)
            {   // これまでの約定処理により、現在の boardPrice (= boardPrices.GetAt(bpIdx) の boardOrder のうち約定されたものを破棄されるため、next を更新する
                boardPrice.Head = nextHead;
            }

            for (var i = 0;; ++i)
            {
                var boardPriceRemove = boardPrices.GetAt(0).Item.Value;
                if (boardPriceRemove.Price >= boardPriceUpToRemove)
                {
                    break;
                }

                /*
                if (boardPrices.GetAt(0).Item.Value.Price == Int32.MaxValue)
                {
                    Console.WriteLine("unexpected");
                }
                */

                _boardPricePool.Back(boardPriceRemove);
                boardPrices.RemoveAt(0);
            }

            foreach (var execution in executions)
            {
                _executionProcesser.Executed(execution);
            }

            _executionCounter += 1;
        }

        /// <summary>
        /// OCO 注文を処理する。
        /// precondition: OCO 注文は連続して投入され、かつ買い注文→売り注文の順番で投入される
        /// </summary>
        /// <param name="orderBytesBuy"></param>
        private void ProcessOrderOCO(byte[] orderBytesBuy)
        {
            // (キャンセル注文などではない) 次の注文を取得する
            byte[] orderBytesSell = _orderReception.NextOrderOCO();

            // 買い注文→売り注文の順番で投入される前提である
            long currentCounter = _executionCounter;

            int priceBuy = OrderByteObject.Price(orderBytesBuy);
            Execute(orderBytesBuy, priceBuy, _sellBoardPrices, _sellExecutableChecker, _buyBoardPrices);

            // 前の注文が約定していない場合はもう一方の注文を処理する
            if (currentCounter != _executionCounter)
            {
                return;
            }

            // TODO: 要検討: 買い注文が FOK 注文で KILL された場合も売り注文は有効になる実装になっているが、どのような仕様が適切か
            int priceSell = OrderByteObject.Price(orderBytesSell);
            Execute(orderBytesSell, priceSell, _buyBoardPrices, _sellExecutableChecker, _sellBoardPrices);
            if (currentCounter != _executionCounter)
            {   // 売り注文が約定されたので、買い注文をキャンセルする
                RemoveOrder(OrderByteObject.Id(orderBytesBuy));
            }
        }


        /// <summary>
        /// 板の注文価格を表現するオブジェクト。
        /// 同一価格の先頭 Order および 末尾の Order へのポインタを保持しておき、Queue として追加削除ができるようにする。
        /// 一つしか Order が無い場合は Head == Tail
        /// </summary>
        public class BoardPrice : ICloneable
        {
            // 価格
            public int Price { get; private set; }

            // 先頭 Order へのポインタ
            public Order? Head { get; set; }
            // 末尾 Order へのポインタ
            public Order? Tail { get; set; }


            /// <summary>
            /// BoardPrice を order を保持する状態で初期化する
            /// </summary>
            /// <param name="price">price</param>
            /// <param name="order">order</param>
            public void Init(int price, Order? order)
            {
                Price = price;
                Head = Tail = order;
            }

            /// <summary>
            /// 末尾に order を追加する
            /// </summary>
            /// <param name="order">追加する order</param>
            public void AddOrder(Order order)
            {
                Tail.Next = order;  // precondition: Tail != null
                order.Previous = Tail;
                Tail = order;
            }

            /// <summary>
            /// この BoardPrice が Sentinel かどうかを返す
            /// </summary>
            /// <returns>true; Sentinel である false: Sentinel ではない</returns>
            public bool IsSentinel()
            {
                return Price == Int32.MaxValue;
            }

            /// <summary>
            /// 複製する
            /// </summary>
            /// <returns></returns>
            public object Clone()
            {
                return this.MemberwiseClone();
            }
        }

        /// <summary>
        /// 板の注文を表現するオブジェクト。
        /// (注文キャンセル時の処理のため) LinkedList で前後の Order へのポインタを保持する 
        /// </summary>
        public class Order : ICloneable
        {
            // original order bytes
            public byte[] OrderBytes { get; private set; } = new byte[0];
            // 未約定の数量。(一部約定される度に減少する)
            public long Amount { get; set; }

            // BoardPrice へのポインタ
            public BoardPrice? BoardPrice { get; set; }
            // 前要素へのポインタ
            public Order? Previous { get; set; }
            // 後要素へのポインタ
            public Order? Next { get; set; }

            /// <summary>
            /// orderBytes および amount で初期化する
            /// </summary>
            /// <param name="orderBytes">orderBytes</param>
            /// <param name="amount">amount</param>
            public void Init(byte[] orderBytes, long amount)
            {
                OrderBytes = orderBytes;
                Amount = amount;
                Previous = Next = null;
            }

            /// <summary>
            /// 複製する
            /// </summary>
            /// <returns></returns>
            public object Clone()
            {
                return this.MemberwiseClone();
            }
        }

        /// <summary>
        /// Order の価格と board の価格で約定が成立するかどうかを判定するための Interface
        /// </summary>
        private interface IExecutableChecker
        {
            /// <summary>
            /// 約定成立判定
            /// </summary>
            /// <param name="orderPrice">今回の注文の価格</param>
            /// <param name="boardPrice">板にある注文の価格</param>
            /// <returns>true: 約定が成立する false: 成立しない</returns>
            public bool IsExecutable(int orderPrice, int boardPrice);
        }

        /// <summary>
        /// 買い注文のための約定成立判定
        /// </summary>
        private class BuyOrderExecutableChecker : IExecutableChecker
        {
            public bool IsExecutable(int buyPrice, int sellPrice)
            {
                return -1 * buyPrice >= sellPrice;
            }
        }

        /// <summary>
        /// 売り注文のための約定成立判定
        /// </summary>
        private class SellOrderExecutableChecker : IExecutableChecker
        {
            public bool IsExecutable(int sellPrice, int buyPrice)
            {
                return sellPrice <= -1 * buyPrice;
            }
        }
    }
}
