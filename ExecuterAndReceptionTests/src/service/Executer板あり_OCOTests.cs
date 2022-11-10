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
    ///  OCO 注文
    ///  * OCO 注文のうち買い注文が約定
    ///  * OCO 注文のうち売り注文が約定
    ///  * OCO 注文のどちらも未約定
    /// </summary>
    [TestClass()]
    public class Executer板あり_OCOTests : ExecuterTestsBase
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
        public void 約定_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(200, -21000, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO));
            _orderReceptionMock.AddOrder(builder.Build(200, 22000, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は 約定 が一つ
            Assert.AreEqual(1, _executionProcesserMock.Executions.Count);
            var expectedExecution = new Execution(OrderByteObject.ToId(0, 1), OrderByteObject.ToId(101, 1), 200, 20000);
            // キャンセルはない
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文は変わらない
            Assert.AreEqual(4, _buyBoard.Count);
            // 売り板の注文が減っている
            Assert.AreEqual(3, _sellBoard.Count);
            Assert.AreEqual(21000, _sellBoard.GetAt(0).Item.Value.Price);
        }

        [TestMethod()]
        public void 約定_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(200, -11000, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO));
            _orderReceptionMock.AddOrder(builder.Build(200, 12000, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は 約定 が一つ
            Assert.AreEqual(1, _executionProcesserMock.Executions.Count);
            var expectedExecution = new Execution(OrderByteObject.ToId(0, 2), OrderByteObject.ToId(99, 1), 200, -12000);
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
        public void 約定しない()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(200, -10500, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO));
            _orderReceptionMock.AddOrder(builder.Build(200, 20500, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 はなし
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文が増えている
            Assert.AreEqual(5, _buyBoard.Count);
            Assert.IsNotNull(_buyBoard[-10500]);
            // 売り板の注文が増えている
            Assert.AreEqual(5, _sellBoard.Count);
            Assert.IsNotNull(_sellBoard[20500]);
        }
    }
}
