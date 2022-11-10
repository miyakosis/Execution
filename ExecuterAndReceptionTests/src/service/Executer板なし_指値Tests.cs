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

namespace ExecuterAndReceptionTests.src.service
{
    /// <summary>
    /// 注文が存在しない状態で指値注文が発注
    /// * GTC の買い注文
    /// * GTC の売り注文
    /// * IOC の買い注文
    /// * IOC の売り注文
    /// * FOK の買い注文
    /// * FOK の売り注文
    /// </summary>
    [TestClass()]
    public class Executer板なし_指値Tests : ExecuterTestsBase
    {
        [TestMethod()]
        public void GTC_B()
        {
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(100, -10000, OrderByteObject.TYPE_GTC));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 なし
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板に注文が追加されている
            Assert.AreEqual(2, _buyBoard.Count);
            var boardPrice = _buyBoard.GetAt(0).Item.Value;
            Assert.AreEqual(-10000, boardPrice.Price);
            Assert.AreEqual(true, boardPrice.Head == boardPrice.Tail);

            Assert.AreEqual(1, _sellBoard.Count);
        }

        [TestMethod()]
        public void GTC_S()
        {
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(100, 10000, OrderByteObject.TYPE_GTC));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 なし
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 売り板に注文が追加されている
            Assert.AreEqual(1, _buyBoard.Count);

            Assert.AreEqual(2, _sellBoard.Count);
            var boardPrice = _sellBoard.GetAt(0).Item.Value;
            Assert.AreEqual(10000, boardPrice.Price);
            Assert.AreEqual(true, boardPrice.Head == boardPrice.Tail);
        }

        [TestMethod()]
        public void IOC_B()
        {
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(100, -10000, OrderByteObject.TYPE_IOC));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は CancelOrder(IOC)のみ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_IOC, _executionProcesserMock.CanceledOrders[0].Reason);

            // 板に注文が追加されていない
            Assert.AreEqual(1, _buyBoard.Count);
            Assert.AreEqual(1, _sellBoard.Count);
        }

        [TestMethod()]
        public void IOC_S()
        {
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(100, 10000, OrderByteObject.TYPE_IOC));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は CancelOrder(IOC)のみ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_IOC, _executionProcesserMock.CanceledOrders[0].Reason);

            // 板に注文が追加されていない
            Assert.AreEqual(1, _buyBoard.Count);
            Assert.AreEqual(1, _sellBoard.Count);
        }

        [TestMethod()]
        public void FOK_B()
        {
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(100, -10000, OrderByteObject.TYPE_FOK));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は CancelOrder(FOK)のみ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_FOK, _executionProcesserMock.CanceledOrders[0].Reason);

            // 板に注文が追加されていない
            Assert.AreEqual(1, _buyBoard.Count);
            Assert.AreEqual(1, _sellBoard.Count);
        }

        [TestMethod()]
        public void FOK_S()
        {
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(100, 10000, OrderByteObject.TYPE_FOK));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 は CancelOrder(FOK)のみ
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(1, _executionProcesserMock.CanceledOrders.Count);
            Assert.AreEqual(CanceledOrder.REASON_FOK, _executionProcesserMock.CanceledOrders[0].Reason);

            // 板に注文が追加されていない
            Assert.AreEqual(1, _buyBoard.Count);
            Assert.AreEqual(1, _sellBoard.Count);
        }
    }
}
