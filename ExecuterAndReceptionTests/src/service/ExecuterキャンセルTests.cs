using ExecuterAndReception.src.model;
using ExecuterAndReception.src.service.interfaces;
using ExecuterAndReception.src.service;
using ExecuterAndReception.src.util;
using ExecuterAndReceptionTests.src.test_mock;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using WBTrees;
using System.Transactions;

namespace ExecuterAndReceptionTests.src.service
{
    /// <summary>
    ///  注文キャンセルのテスト
    ///  * 価格にある単独の買い注文をキャンセル(価格自体が削除される)
    ///  * 価格にある単独の売り注文をキャンセル(価格自体が削除される)
    ///  * 同一価格の先頭の買い注文をキャンセル
    ///  * 同一価格の先頭の売り注文をキャンセル
    ///  * 同一価格の中間の買い注文をキャンセル
    ///  * 同一価格の中間の売り注文をキャンセル
    ///  * 同一価格の末尾の買い注文をキャンセル
    ///  * 同一価格の末尾の売り注文をキャンセル
    /// </summary>
    [TestClass()]
    public class ExecuterキャンセルTests : ExecuterTestsBase
    {
        [TestInitialize]
        public void TestMethodSetup()
        {
            // 板の注文
            var builderBuy = new OrderByteBuider(99);
            var orderBuys = new byte[][] {
                builderBuy.Build(200, -12000),
                builderBuy.Build(110, -11000),
                builderBuy.Build(120, -11000),
                builderBuy.Build(130, -11000),
                builderBuy.Build(200, -10000),
            };
            foreach(var order in orderBuys)
            {
                _orderReceptionMock.AddOrder(order);
            }

            var builderSell = new OrderByteBuider(101);
            var orderSells = new byte[][] {
                builderSell.Build(200, 20000),
                builderSell.Build(110, 21000),
                builderSell.Build(120, 21000),
                builderSell.Build(130, 21000),
                builderSell.Build(200, 22000),
            };
            foreach (var order in orderSells)
            {
                _orderReceptionMock.AddOrder(order);
            }
        }


        [TestMethod()]
        public void 価格単独_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider(99);
            _orderReceptionMock.AddOrder(builder.BuildCancel(1));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文が減っている
            Assert.AreEqual(3, _buyBoard.Count);
            Assert.AreEqual(-11000, _buyBoard.GetAt(0).Item.Value.Price);
            // 売り板の注文は変わらない
            Assert.AreEqual(4, _sellBoard.Count);
        }

        [TestMethod()]
        public void 価格単独_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider(101);
            _orderReceptionMock.AddOrder(builder.BuildCancel(1));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文は変わらない
            Assert.AreEqual(4, _buyBoard.Count);
            // 売り板の注文が減っている
            Assert.AreEqual(3, _sellBoard.Count);
            Assert.AreEqual(21000, _sellBoard.GetAt(0).Item.Value.Price);
        }

        [TestMethod()]
        public void 同一価格_先頭_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider(99);
            _orderReceptionMock.AddOrder(builder.BuildCancel(2));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文は変わらないが、指定の価格の先頭注文が変わっている
            Assert.AreEqual(4, _buyBoard.Count);
            Assert.AreEqual(120, _buyBoard[-11000].Head.Amount);
            // 売り板の注文は変わらない
            Assert.AreEqual(4, _sellBoard.Count);
        }

        [TestMethod()]
        public void 同一価格_先頭_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider(101);
            _orderReceptionMock.AddOrder(builder.BuildCancel(2));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文は変わらない
            Assert.AreEqual(4, _buyBoard.Count);
            // 売り板の注文は変わらないが、指定の価格の先頭注文が変わっている
            Assert.AreEqual(4, _sellBoard.Count);
            Assert.AreEqual(120, _sellBoard[21000].Head.Amount);
        }

        [TestMethod()]
        public void 同一価格_中間_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider(99);
            _orderReceptionMock.AddOrder(builder.BuildCancel(3));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文は変わらないが、指定の価格の該当注文が無くなっている
            Assert.AreEqual(4, _buyBoard.Count);
            Assert.AreEqual(_buyBoard[-11000].Tail, _buyBoard[-11000].Head.Next);
            Assert.AreEqual(_buyBoard[-11000].Head, _buyBoard[-11000].Tail.Previous);
            // 売り板の注文は変わらない
            Assert.AreEqual(4, _sellBoard.Count);
        }

        [TestMethod()]
        public void 同一価格_中間_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider(101);
            _orderReceptionMock.AddOrder(builder.BuildCancel(3));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文は変わらない
            Assert.AreEqual(4, _buyBoard.Count);
            // 売り板の注文は変わらないが、指定の価格の該当注文が無くなっている
            Assert.AreEqual(4, _sellBoard.Count);
            Assert.AreEqual(_sellBoard[21000].Tail, _sellBoard[21000].Head.Next);
            Assert.AreEqual(_sellBoard[21000].Head, _sellBoard[21000].Tail.Previous);
        }

        [TestMethod()]
        public void 同一価格_末尾_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider(99);
            _orderReceptionMock.AddOrder(builder.BuildCancel(4));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文は変わらないが、指定の価格の末尾の注文が無くなっている
            Assert.AreEqual(4, _buyBoard.Count);
            Assert.AreEqual(_buyBoard[-11000].Tail, _buyBoard[-11000].Head.Next);
            // 売り板の注文は変わらない
            Assert.AreEqual(4, _sellBoard.Count);
        }

        [TestMethod()]
        public void 同一価格_末尾_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider(101);
            _orderReceptionMock.AddOrder(builder.BuildCancel(4));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文は変わらない
            Assert.AreEqual(4, _buyBoard.Count);
            // 売り板の注文は変わらないが、指定の価格の末尾の注文が無くなっている
            Assert.AreEqual(4, _sellBoard.Count);
            Assert.AreEqual(_sellBoard[21000].Tail, _sellBoard[21000].Head.Next);
        }
    }
}
