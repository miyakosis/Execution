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
    /// 指値注文を追加
    /// * 新しい価格で買い注文を追加
    /// * 新しい価格で売り注文を追加
    /// * 既存の価格に買い注文を追加
    /// * 既存の価格に売り注文を追加
    /// </summary>
    [TestClass()]
    public class Executer板あり_指値追加Tests : ExecuterTestsBase
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
        public void 新規価格追加_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(500, -10500));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 なし
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文が増えている
            Assert.AreEqual(5, _buyBoard.Count);
            Assert.IsNotNull(_buyBoard[-10500]);
            // 売り板の注文は変わらない
            Assert.AreEqual(4, _sellBoard.Count);
        }

        [TestMethod()]
        public void 新規価格追加_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(500, 20500));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 なし
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文は変わらない
            Assert.AreEqual(4, _buyBoard.Count);
            // 売り板の注文が増えている
            Assert.AreEqual(5, _sellBoard.Count);
            Assert.IsNotNull(_sellBoard[20500]);
        }

        [TestMethod()]
        public void 既存価格に追加_B()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(500, -11000));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 なし
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文の数は変わらず末尾に追加されている
            Assert.AreEqual(4, _buyBoard.Count);
            Assert.AreEqual(500, _buyBoard[-11000].Tail.Amount);
            // 売り板の注文は変わらない
            Assert.AreEqual(4, _sellBoard.Count);
        }

        [TestMethod()]
        public void 既存価格に追加_S()
        {
            // テスト対象の注文
            var builder = new OrderByteBuider();
            _orderReceptionMock.AddOrder(builder.Build(500, 21000));
            _orderReceptionMock.AddOrder(builder.BuildTerminate());

            _target.Run();

            // 約定後処理 なし
            Assert.AreEqual(0, _executionProcesserMock.Executions.Count);
            Assert.AreEqual(0, _executionProcesserMock.CanceledOrders.Count);

            // 買い板の注文は変わらない
            Assert.AreEqual(4, _buyBoard.Count);
            // 売り板の注文の数は変わらず末尾に追加されている
            Assert.AreEqual(4, _sellBoard.Count);
            Assert.AreEqual(500, _sellBoard[21000].Tail.Amount);
        }
    }
}
