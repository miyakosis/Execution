using ExecuterAndReception.src.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.service.interfaces
{
    public interface IExecutionProcesser
    {
        public void Executed(Execution execution);
        public void Canceled(CanceledOrder canceledOrder);
        public void Run();
        public void Ternimate();
    }
}
