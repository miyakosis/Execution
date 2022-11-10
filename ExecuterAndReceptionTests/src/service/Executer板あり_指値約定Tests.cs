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
    /// 板に注文がある状態で指値注文が約定
    /// * GTC の買い注文が全量約定
    /// * GTC の売り注文が全量約定
    /// * GTC の買い注文が全量約定
    /// * GTC の売り注文が全量約定
    /// 
    /// (IOC, FOK 注文は Executer板あり_成行約定Tests の price 違いでしかないためテスト省略)
    /// </summary>
    [TestClass()]
    public class Executer板あり_指値約定Tests : ExecuterTestsBase
    {
        [TestInitialize]
        public void TestMethodSetup()
        {
            // 板の注文
            var builderBuy = new OrderByteBuider(99);
            _orderReceptionMock.AddOrder(builderBuy.Build(200, -12000));
            _orderReceptionMock.AddOrder(builderBuy.Build(100, -11000));
            _orderReceptionMock.AddOrder(builderBuy.Build(200, -10000));
            _orderReceptionMock.AddOrder(builderBuy.Build(100, -11000));

            var builderSell = new OrderByteBuider(101);
            _orderReceptionMock.AddOrder(builderSell.Build(200, 20000));
            _orderReceptionMock.AddOrder(builderSell.Build(100, 21000));
            _orderReceptionMock.AddOrder(builderSell.Build(200, 22000));
            _orderReceptionMock.AddOrder(builderSell.Build(100, 21000));
        }


        [TestMethod()]
        public void 全量_GTC_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(200, -20000, OrderByteObject.TYPE_GTC));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は 約定 が一つ
            Assert.AreEqual(1, _executionProcesserMock.Executions.Count);
            var expectedExecution = new Execution(OrderByteObject.ToId(0, 1), OrderByteObject.ToId(101, 1), 200, 20000);
            Assert.AreEqual(expectedExecution, _executionProcesserMock.Executions[0]);
            // キャンセルはない
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文は変わらない
            Assert.AreEqual(4, _buyBoard.Count);
            // 売り板の注文が減っている
            Assert.AreEqual(3, _sellBoard.Count);
            Assert.AreEqual(21000, _sellBoard.GetAt(0).Item.Value.Price);
        }

        [TestMethod()]
        public void 全量_GTC_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(200, 10000, OrderByteObject.TYPE_GTC));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は 約定 が一つ
            Assert.AreEqual(1, _executionProcesserMock.Executions.Count);
            var expectedExecution = new Execution(OrderByteObject.ToId(0, 1), OrderByteObject.ToId(99, 1), 200, -12000);
            Assert.AreEqual(expectedExecution, _executionProcesserMock.Executions[0]);
            // キャンセルはない
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文が減っている
            Assert.AreEqual(3, _buyBoard.Count);
            Assert.AreEqual(-11000, _buyBoard.GetAt(0).Item.Value.Price);
            // 売り板の注文は変わらない
            Assert.AreEqual(4, _sellBoard.Count);
        }

        [TestMethod()]
        public void 一部_GTC_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(300, -20000, OrderByteObject.TYPE_GTC));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は 約定 が一つ
            Assert.AreEqual(1, _executionProcesserMock.Executions.Count);
            var expectedExecution = new Execution(OrderByteObject.ToId(0, 1), OrderByteObject.ToId(101, 1), 200, 20000);
            Assert.AreEqual(expectedExecution, _executionProcesserMock.Executions[0]);
            // キャンセルはない
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文が増えている
            Assert.AreEqual(5, _buyBoard.Count);
            Assert.AreEqual(-20000, _buyBoard.GetAt(0).Item.Value.Price);
            // 売り板の注文が減っている
            Assert.AreEqual(3, _sellBoard.Count);
            Assert.AreEqual(21000, _sellBoard.GetAt(0).Item.Value.Price);
        }

        [TestMethod()]
        public void 一部_GTC_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(300, 12000, OrderByteObject.TYPE_GTC));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は 約定 が一つ
            Assert.AreEqual(1, _executionProcesserMock.Executions.Count);
            var expectedExecution = new Execution(OrderByteObject.ToId(0, 1), OrderByteObject.ToId(99, 1), 200, -12000);
            Assert.AreEqual(expectedExecution, _executionProcesserMock.Executions[0]);
            // キャンセルはない
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文が減っている
            Assert.AreEqual(3, _buyBoard.Count);
            Assert.AreEqual(-11000, _buyBoard.GetAt(0).Item.Value.Price);
            // 売り板の注文が増えている
            Assert.AreEqual(5, _sellBoard.Count);
            Assert.AreEqual(12000, _sellBoard.GetAt(0).Item.Value.Price);
        }
    }
}
