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
    ///  * OCO 注文のうち買い注文をキャンセル
    ///  * OCO 注文のうち売り注文をキャンセル
    /// </summary>
    [TestClass()]
    public class Executerキャンセル_OCOTests : ExecuterTestsBase
    {
        [TestInitialize]
        public void TestMethodSetup()
        {
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(100, -10000, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO));
            _orderReceptionMock.AddOrder(builder.Build(100, 20000, OrderByteObject.TYPE_GTC, OrderByteObject.PROCESS_TYPE_OCO));
        }

        [TestMethod()]
        public void キャンセル_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.BuildCancel(1));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文が残っていない
            Assert.AreEqual(1, _buyBoard.Count);
            // 売り板の注文が残っていない
            Assert.AreEqual(1, _sellBoard.Count);
        }

        [TestMethod()]
        public void キャンセル_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.BuildCancel(2));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は キャンセルが1つ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_CANCEL_ORDER, _executionProcesserMock.CanceledOrders[0].Reason);

            // 買い板の注文が残っていない
            Assert.AreEqual(1, _buyBoard.Count);
            // 売り板の注文が残っていない
            Assert.AreEqual(1, _sellBoard.Count);
        }
    }
}
