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
using WBTrees;
using System.Reflection;

namespace ExecuterAndReceptionTests.src.service
{
    [TestClass()]
    public class ExecuterTestsBase
    {
        protected OrderReceptionMock _orderReceptionMock = new OrderReceptionMock();
        protected ExecutionProcesserMock _executionProcesserMock = new ExecutionProcesserMock();
        protected Executer _target;
        protected WBMap<int, Executer.BoardPrice> _buyBoard;
        protected WBMap<int, Executer.BoardPrice> _sellBoard;

        [TestInitialize]
        public void TestBaseMethodSetup()
        {
            _orderReceptionMock.Clear();
            _executionProcesserMock.Clear();

            _target = new Executer(_orderReceptionMock, _executionProcesserMock);

            var buyBoardField = _target.GetType().GetField("_buyBoardPrices",
                BindingFlags.GetField |
                BindingFlags.NonPublic |
                BindingFlags.Instance);
            _buyBoard = (WBMap<int, Executer.BoardPrice>)buyBoardField.GetValue(_target);
            var sellBoardField = _target.GetType().GetField("_sellBoardPrices",
                BindingFlags.GetField |
                BindingFlags.NonPublic |
                BindingFlags.Instance);
            _sellBoard = (WBMap<int, Executer.BoardPrice>)sellBoardField.GetValue(_target);
        }
    }
}
