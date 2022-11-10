using ExecuterAndReception.src.model;
using ExecuterAndReception.src.util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WBTrees;

namespace ExecuterAndReception.src.service.experimental
{
    internal class Executer
    {
        private OrderReception _orderReception;
        private ExecutionProcesser _executionProcesser;

        private ObjectPool<BoardPrice> _boardPricePool = new ObjectPool<BoardPrice>(new BoardPrice());
        private ObjectPool<Order> _orderPool = new ObjectPool<Order>(new Order());

        private WBMap<int, BoardPrice> _buyBoardPrices = new WBMap<int, BoardPrice>();
        private WBMap<int, BoardPrice> _sellBoardPrices = new WBMap<int, BoardPrice>();

        private IExecutableChecker _buyExecutableChecker = new BuyOrderExecutableChecker();
        private IExecutableChecker _sellExecutableChecker = new SellOrderExecutableChecker();

        private Dictionary<long, Order> _allOrders = new Dictionary<long, Order>();

        private long _executionCounter = 0;


        public Executer(OrderReception orderReception, ExecutionProcesser executionProcesser)
        {
            _orderReception = orderReception;
            _executionProcesser = executionProcesser;

            // add Sentinel
            var sentinel = new BoardPrice();
            sentinel.Init(int.MaxValue, null);
            _buyBoardPrices.Add(int.MaxValue, sentinel);
            _sellBoardPrices.Add(int.MaxValue, sentinel);
        }


        public void Run()
        {
            // TODO: ここでメンテナンス明けや障害復旧時などの板寄せの処理を実施する

            // 以降ザラ場処理
            while (true)
            {
                Span<byte> orderBytes = _orderReception.nextOrder();
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
                        // precondition failed.
                        // TODO: ログ出力して次の注文を処理する
                        break;

                }
            }
        }

        private void Cancel(long orderId)
        {
            RemoveOrder(orderId);
            _executionProcesser.Canceled(new CanceledOrder(orderId, CanceledOrder.REASON_CANCEL_ORDER));
        }

        /**
         * 同一価格の注文リストにおいて、キャンセル対象の注文は以下のパターンがある
         * * 注文リストの先頭である
         * * 途中である
         * * 末尾である
         * * 先頭かつ末尾である(= この価格にはキャンセル対象の注文のみ)
         */
        private void RemoveOrder(long orderId)
        {
            Order boardOrder;
            if (_allOrders.TryGetValue(orderId, out boardOrder) == false)
            {   // OCO 注文のキャンセルの場合やタイミング等の理由で約定済み注文にキャンセル注文が発行された場合
                // TODO: log 出力 or キャンセル不能メッセージの送出を検討 
                return;
            }
            if (boardOrder.Previous == null && boardOrder.Next == null)
            {   // 先頭かつ末尾である(= この価格にはキャンセル対象の注文のみ)
                // 注文価格自体を削除する
                int price = OrderByteObject.Price(boardOrder.OrderBytes);
                if (price <= 0)
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

            RemovePairOrderIfOCO(boardOrder);
        }

        private void RemovePairOrderIfOCO(Order order)
        {
            if (OrderByteObject.IsOCO(order.OrderBytes))
            {
                long id = OrderByteObject.Id(order.OrderBytes);
                int price = OrderByteObject.Price(order.OrderBytes);

                // 買い注文→売り注文の順番で id が振られている
                long orderIdOCO = price < 0 ? id + 1 : id - 1;

                RemoveOrder(orderIdOCO);
            }
        }

        private void ProcessOrder(Span<byte> orderBytes)
        {
            int price = OrderByteObject.Price(orderBytes);
            if (price < 0)
            {   // buy
                Execute(orderBytes, price, _sellBoardPrices, _sellExecutableChecker, _buyBoardPrices);
            }
            else
            {   // sell
                Execute(orderBytes, price, _buyBoardPrices, _buyExecutableChecker, _sellBoardPrices);
            }
        }

        private void Execute(Span<byte> orderBytes, int price, WBMap<int, BoardPrice> boardPrices, IExecutableChecker executableChecker, WBMap<int, BoardPrice> sameSideBoardPrices)
        {
            long orderId = OrderByteObject.Id(orderBytes);
            long amount = OrderByteObject.Amount(orderBytes);
            List<Execution> executions = new List<Execution>();
            List<Order> removeOrders = new List<Order>();

            for (int opIdx = 0; ; ++opIdx)
            {
                var boardPrice = boardPrices.GetAt(opIdx).Item.Value;
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
                        opIdx,
                        boardPrice,
                        sameSideBoardPrices);
                    return;
                }

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
                        // (板に残る注文は FAS である必要があり、FAK ではないという前提もありえる)

                        commitExecutions(executions, removeOrders, boardPrices, opIdx, boardPrice, nextHead);
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
                            commitExecutions(executions, removeOrders, boardPrices, opIdx, boardPrice, boardOrder.Next);
                        }
                        else
                        {
                            // この price に order が残っていないため、price 自体を削除するように指示する
                            commitExecutions(executions, removeOrders, boardPrices, opIdx + 1, null, null);
                        }

                        return;
                    }
                    else
                    {   // 今回の注文の数量が板の注文より多い
                        // 板の注文を約定し、次の板注文の処理につなげる
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

        /*
        private bool IsFinishExecution(int bpIdx, WBMap<int, OrderPrice> boardPrices, int price)
        {
            if (bpIdx == boardPrices.Count - 1)
            {   // これ以上板注文がない
                return true;
            }

            var nextPrice = boardPrices.GetAt(bpIdx + 1).Item.Value.Price;
            if (price < 0)
            {   // buy order
                if (-1 * price < nextPrice)
                {   // 次の売り boardPrice が買い注文価格より高い
                    return true;
                }
            }
            else
            {   // sell order
                if (-1 * price > nextPrice)
                {    // 次の買い boardPrice が売り注文価格より安い
                    return true;

                }
            }

            return false;
        }
        */

        private void FinishExecuion(
            Span<byte> orderBytes,
            long orderId,
            int price,
            long amount,
            List<Execution> executions,
            List<Order> removeBoardOrders,
            WBMap<int, BoardPrice> boardPrices,
            int bpIdx,
            BoardPrice boardPrice,
            WBMap<int, BoardPrice> sameSideBoardPrices
            )
        {
            int type = OrderByteObject.Type(orderBytes);
            if (type == OrderByteObject.TYPE_FOK)
            {   // Kill
                _executionProcesser.Canceled(new CanceledOrder(orderId, CanceledOrder.REASON_FOK));
            }
            else if (type == OrderByteObject.TYPE_IOC)
            {   //  現時点までの約定を完了し、残りの注文を Cancel
                if (executions.Count > 0)
                {
                    commitExecutions(executions, removeBoardOrders, boardPrices, bpIdx, null, null);
                }
                else
                {
                    _executionProcesser.Canceled(new CanceledOrder(orderId, CanceledOrder.REASON_IOC));
                }
            }
            else
            {   // // 現時点までの約定を完了し、残りの注文を板に残す
                if (executions.Count > 0)
                {
                    commitExecutions(executions, removeBoardOrders, boardPrices, bpIdx, null, null);
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

        /**
         * precondition: executions.Count > 0
         */
        private void commitExecutions(
            List<Execution> executions,
            List<Order> removeBoardOrders,
            WBMap<int, BoardPrice> boardPrices,
            int bpIdxUpToRemove,
            BoardPrice? boardPrice,
            Order? nextHead)
        {
            foreach (var boardOrder in removeBoardOrders)
            {
                _orderPool.Back(boardOrder);
                _allOrders.Remove(OrderByteObject.Id(boardOrder.OrderBytes));

                RemovePairOrderIfOCO(boardOrder);
            }

            if (boardPrice != null)
            {   // これまでの約定処理により、現在の boardPrice (= boardPrices.GetAt(bpIdx) の boardOrder のうち約定されたものを破棄するため、next を更新する
                boardPrice.Head = nextHead;
            }

            for (var i = 0; i < bpIdxUpToRemove; ++i)
            {
                _boardPricePool.Back(boardPrices.GetAt(0).Item.Value);
                boardPrices.RemoveAt(0);
            }

            foreach (var execution in executions)
            {
                _executionProcesser.Executed(execution);
            }

            _executionCounter += 1;
        }


        private void ProcessOrderOCO(Span<byte> orderBytesBuy)
        {
            Span<byte> orderBytesSell = _orderReception.nextOrderOCO();

            // 買い注文→売り注文の順番で投入される前提である
            long currentCounter = _executionCounter;

            int priceBuy = OrderByteObject.Price(orderBytesBuy);
            Execute(orderBytesBuy, priceBuy, _sellBoardPrices, _sellExecutableChecker, _buyBoardPrices);

            // 約定していない場合はもう一方の注文を処理する
            if (currentCounter == _executionCounter)
            {
                int priceSell = OrderByteObject.Price(orderBytesSell);
                Execute(orderBytesSell, priceSell, _sellBoardPrices, _sellExecutableChecker, _buyBoardPrices);

                // TODO: 要検討: 買い注文が FOK 注文で KILL された場合も売り注文は有効になるが、それでよいのか
            }
        }


        /*
        private int removeBoardOrder(Order boardOrder, BoardPrice boardPrice, WBMap<int, BoardPrice> boardPrices)
        {
            _orderPool.Back(boardOrder);

            if (boardOrder.Next != null)
            {   // 同じ価格で注文が残っている
                boardPrice.Head = boardOrder.Next;
                return boardPrice.Price;
            }
            else
            {   // 同じ価格の注文が残っていないため、板価格自体を削除する
                boardPrices.RemoveAt(0);
                _boardPricePool.Back(boardPrice);

                if (boardPrices.Count > 0)
                {
                    return boardPrices.GetAt(0).Item.Value.Price;
                }
                else
                {
                    return -1;
                }
            }
        }
        */

        /*
        private void addBuyOrder(byte[] boardOrder, int price)
        {
            var boardOrderObj = _orderPool.Borrow();
            boardOrderObj.Init(boardOrder);

            var idx = _buyOrderPrices.GetFirstIndex(p => p.Key <= price);

            if (idx < 0)
            {   // 今回の注文が最高値
                //var currentHighestBoardPrice = buyBoardPrices.GetAt(buyBoardPrices.Count - 1).Item.Value;
                var newBoardPrice = addNewBoardPrice(boardOrderObj, price);
                //currentHighestBoardPrice.Next = currentHighestBoardPrice;

                // 最高買値を更新
                _highestBuyPrice = price;

            }
            else if (idx == _buyOrderPrices.Count)
            {   // 今回の注文が最安値
                //var currentLowestBoardPrice = buyBoardPrices.GetAt(0).Item.Value;
                var newBoardPrice = addNewBoardPrice(boardOrderObj, price);
                //newBoardPrice.Next = currentLowestBoardPrice;
            }
            else
            {
                var boardPrice = _buyOrderPrices.GetAt(idx).Item.Value;
                if (boardPrice.Price == price)
                {   // 同じ価格の boardPrice があるため、その末尾に boardOrder を追加するのみ
                    boardPrice.AddBoardOrder(boardOrderObj);
                    boardOrderObj.BoardPrice = boardPrice;
                }
                else
                {
                    // 同じ価格の注文がないので、新しい価格の boardPrice を作成し、
                    // 一つ高い boardPrice の後に挿入する
                    var newBoardPrice = addNewBoardPrice(boardOrderObj, price);
                    /*
                    var nextBoardPrice = boardPrice.Next;

//                    newBoardPrice.Prevous = boardPrice;
                    newBoardPrice.Next = nextBoardPrice;

                    boardPrice.Next = newBoardPrice;
                    if (nextBoardPrice != null)
                    {
//                        nextBoardPrice.Prevous = newBoardPrice;
                    } 
                    
                }
            }
        }
        */

        /*
        private BoardPrice addNewBoardPrice(Order boardOrder, int price)
        {
            var boardPrice = _orderPricePool.Borrow();
            boardPrice.Init(price, boardOrder);
            boardOrder.BoardPrice = boardPrice;

            _buyOrderPrices[price] = boardPrice;

            return boardPrice;
        }
        */


        /*
        private void addSellOrder(byte[] boardOrder, int price)
        {
            throw new NotImplementedException();
        }
        */



        class BoardPrice : ICloneable
        {
            public int Price { get; private set; }

            public Order? Head { get; set; }
            public Order? Tail { get; set; }


            public void Init(int price, Order? order)
            {
                Price = price;
                Head = Tail = order;
            }

            public void AddOrder(Order order)
            {
                Tail.Next = order;
                order.Previous = Tail;
                Tail = order;
            }

            public bool IsSentinel()
            {
                return Price == int.MaxValue;
            }

            public object Clone()
            {
                return MemberwiseClone();
            }
        }

        class Order : ICloneable
        {
            public byte[] OrderBytes { get; private set; } = new byte[0];
            public long Amount { get; set; }

            public BoardPrice? BoardPrice { get; set; }
            public Order? Previous { get; set; }
            public Order? Next { get; set; }

            public void Init(Span<byte> orderBytes, long amount)
            {
                OrderBytes = orderBytes.ToArray();
                Amount = amount;
                Previous = Next = null;
            }

            public object Clone()
            {
                return MemberwiseClone();
            }
        }


        private interface IExecutableChecker
        {
            public bool IsExecutable(int orderPrice, int boardPrice);
        }

        private class BuyOrderExecutableChecker : IExecutableChecker
        {
            public bool IsExecutable(int buyPrice, int sellPrice)
            {
                return -1 * buyPrice >= sellPrice;
            }
        }

        private class SellOrderExecutableChecker : IExecutableChecker
        {
            public bool IsExecutable(int sellPrice, int buyPrice)
            {
                return -1 * sellPrice <= buyPrice;
            }
        }
    }
}
