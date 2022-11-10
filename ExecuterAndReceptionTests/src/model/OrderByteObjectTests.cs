using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExecuterAndReception.src.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.model.Tests
{
    [TestClass()]
    public class OrderByteObjectTests
    {
        private const int CUSTOMER_ID = 12345678;
        private const int SEQENCE = 2345678;
        private const long TIME = 11111111;
        private const long AMOUNT = 22222222;
        private const int PRICE = 33333333;
        private const int TYPE = OrderByteObject.TYPE_GTC;
        private const int PROCESS_TYPE = OrderByteObject.PROCESS_TYPE_ORDER;

        // little endian order
        private const long ID = (long)SEQENCE << 32 | CUSTOMER_ID;

        private byte[] _target;

        [TestInitialize]
        public void TestMethodSetup()
        {
            _target = BitConverter.GetBytes(CUSTOMER_ID)
                        .Concat(BitConverter.GetBytes(SEQENCE))
                        .Concat(BitConverter.GetBytes(TIME))
                        .Concat(BitConverter.GetBytes(AMOUNT))
                        .Concat(BitConverter.GetBytes(PRICE))
                        .Concat(new byte[] { (byte)TYPE })
                        .Concat(new byte[] { (byte)PROCESS_TYPE })
                        .ToArray();
        }


        [TestMethod()]
        public void ProcessTypeTest()
        {
            Assert.AreEqual(PROCESS_TYPE, OrderByteObject.ProcessType(_target));
        }

        [TestMethod()]
        public void TypeTest()
        {
            Assert.AreEqual(TYPE, OrderByteObject.Type(_target));
        }


        [TestMethod()]
        public void IsOCOTest()
        {
            Assert.AreEqual(false, OrderByteObject.IsOCO(_target));

            // case: IsOCO() == true
            _target[OrderByteObject.OFFSET_PROCESS_TYPE] = (byte)OrderByteObject.PROCESS_TYPE_OCO;
            Assert.AreEqual(true, OrderByteObject.IsOCO(_target));
        }

        [TestMethod()]
        public void IdTest()
        {
            Assert.AreEqual(ID, OrderByteObject.Id(_target));
        }

        [TestMethod()]
        public void AmountTest()
        {
            Assert.AreEqual(AMOUNT, OrderByteObject.Amount(_target));
        }

        [TestMethod()]
        public void PriceTest()
        {
            Assert.AreEqual(PRICE, OrderByteObject.Price(_target));
        }
    }
}