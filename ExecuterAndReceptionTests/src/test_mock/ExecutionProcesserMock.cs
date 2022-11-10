using ExecuterAndReception.src.model;
using ExecuterAndReception.src.service.interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReceptionTests.src.test_mock
{
    public class ExecutionProcesserMock : IExecutionProcesser
    {
        public List<Execution> Executions = new List<Execution>();
        public List<CanceledOrder> CanceledOrders = new List<CanceledOrder>();

        public void Executed(Execution execution)
        {
            Executions.Add(execution);
        }

        public void Canceled(CanceledOrder canceledOrder)
        {
            CanceledOrders.Add(canceledOrder);
        }

        public void Clear()
        {
            Executions.Clear();
            CanceledOrders.Clear();
        }


        public void Run()
        {
            // not used
        }

        public void Ternimate()
        {
            // not used
        }
    }
}
