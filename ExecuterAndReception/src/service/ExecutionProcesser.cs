using ExecuterAndReception.src.model;
using ExecuterAndReception.src.service.interfaces;
using ExecuterAndReception.src.util;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.service
{
    /// <summary>
    /// 約定後処理を管理するクラス。
    /// Producer-Consumer pattern で設計しており、Executed() および Canceled() が Producer、Run() が Consumer である。
    /// 
    /// </summary>
    internal class ExecutionProcesser : IExecutionProcesser
    {
        private bool _isTerminated = false;                
        private ConcurrentQueue<ExecutionProcess> _queue = new ConcurrentQueue<ExecutionProcess>();
        private ObjectPool<ExecutionProcess> _executionProcessPool = new ObjectPool<ExecutionProcess>(new ExecutionProcess());

        private int _waitTime = MIN_WAIT_TIME;
        private const int MIN_WAIT_TIME = 1;
        private const int MAX_WAIT_TIME = 1000;

        // (分析のため)約定結果の詳細を表示するかどうか。true: 表示する false: しない
        private bool _isAnalysisDetailOutput;
        // 約定数
        private int _analysisExecutionCount = 0;
        // キャンセル数
        private int _analysisCancelCount = 0;

        internal ExecutionProcesser(bool isAnalysisDetailOutput = false)
        {
            _isAnalysisDetailOutput = isAnalysisDetailOutput;
        }

        public void Ternimate()
        {
            _isTerminated = true;
        }

        public void Executed(Execution execution)
        {
            var executionProcess = _executionProcessPool.Borrow();
            executionProcess._execution = execution;
            executionProcess._canceledOrder = null;

            _queue.Enqueue(executionProcess);
        }

        public void Canceled(CanceledOrder canceledOrder)
        {
            var executionProcess = _executionProcessPool.Borrow();
            executionProcess._execution = null;
            executionProcess._canceledOrder = canceledOrder;

            _queue.Enqueue(executionProcess);
        }

        public void Run()
        {
            while(true)
            {
                ExecutionProcess? executionProcess;
                if (_queue.TryDequeue(out executionProcess))
                {
                    _waitTime = MIN_WAIT_TIME;
                    ProcessExecution(executionProcess);
                }
                else if (_isTerminated)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(_waitTime);
                    _waitTime = Math.Min(_waitTime * 2, MAX_WAIT_TIME); // expnential backoff
                }
            }
            Console.WriteLine($"terminated: {DateTime.Now} execution: {_analysisExecutionCount} cancel: {_analysisCancelCount}");
        }

        
        private void ProcessExecution(ExecutionProcess executionProcess)
        {
            var dt = DateTime.Now;  // 約定時刻

            // こちらのプロセスでも注文情報および約定情報を元に、Frontend からの参照用の板情報を作ることを想定している
            // (注文情報をどう受け取るかは未検討)

            // 今後、ここで締結プロセスに処理を振り分ける
            // 締結プロセスでは、顧客資産情報の更新を含む永続化や顧客通知などを行う

            // 現時点での実装はとりあえずログ出力するのみ
            if (_isAnalysisDetailOutput == false)
            {
                if (executionProcess._execution != null)
                {
                    _analysisExecutionCount += 1;
                }
                if (executionProcess._canceledOrder != null)
                {
                    _analysisCancelCount += 1;
                }
                _executionProcessPool.Back(executionProcess);
                return;
            }

            var sb = new StringBuilder();
            sb.Append(dt);

            if (executionProcess._execution != null)
            {
                _analysisExecutionCount += 1;

                var obj = executionProcess._execution;
                var amountString = ((double)obj.Amount / 10000000).ToString("F2");
                if (obj.Price < 0)
                {
                    sb.Append($" {ToDisplay(obj.OrderId)} S {ToDisplay(obj.BoardOrderId)}");
                    sb.Append($" {-1 * obj.Price} {amountString}");
                }
                else
                {
                    sb.Append($" {ToDisplay(obj.OrderId)} B {ToDisplay(obj.BoardOrderId)}");
                    sb.Append($" {obj.Price} {amountString}");
                }
            }
            if (executionProcess._canceledOrder != null)
            {
                _analysisCancelCount += 1;

                var obj = executionProcess._canceledOrder;
                sb.Append($" {ToDisplay(obj.OrderId)} C {obj.Reason}");
            }
            Console.WriteLine(sb.ToString());
            _executionProcessPool.Back(executionProcess);
        }

        /// <summary>
        /// OrderId を customerId(HEX) : sequence(HEX) に成形して返す
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string ToDisplay(long id)
        {
            // little endian なので long の後半4バイトが customer_id, 前半4バイトが sequence
            int customer_id = (int)id;
            int sequence = (int)(id >> 32);
            return customer_id.ToString("x8") + ":" + sequence.ToString("x8");
        }

        internal class ExecutionProcess: ICloneable
        {
            public Execution? _execution = null;
            public CanceledOrder? _canceledOrder = null;

            public object Clone()
            {
                return this.MemberwiseClone();
            }
        }
    }
}
