using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.service
{
    internal class ServiceMain
    {
        static void Main(string[] args)
        {
            var orderReception = new OrderReception();
            var orderReceptionServer = new OrderReceptionServer(orderReception);
            var executionProcesser = new ExecutionProcesser();
            var executer = new Executer(orderReception, executionProcesser);

            var orderReceptionServerThread = new Thread(new ThreadStart(orderReceptionServer.RunServer));
            var executerThread = new Thread(new ThreadStart(executer.Run));
            var executionProcesserThread = new Thread(new ThreadStart(executionProcesser.Run));

            orderReceptionServerThread.Start();
            executerThread.Start();
            executionProcesserThread.Start();

            executionProcesserThread.Join();
            executerThread.Join();
            orderReceptionServerThread.Join();
        }
    }
}
