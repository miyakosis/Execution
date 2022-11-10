using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.util
{
    internal class ObjectPool<T> where T: ICloneable
    {
        const int DEFAULT_SIZE = 1024;
        private Stack<T> _stack = new Stack<T>(DEFAULT_SIZE);
        private T _template;

        public ObjectPool(T obj)
        {
            _template = obj;
        }

        public T Borrow()
        {
            // なぜか下記コードでは null が返ることがある模様であるため、loop で複数回取得を繰り返す。
            // 一つのスレッドからしかこれらのメソッドは呼ばれないはずなので thread safe の問題はないはずだが。
            // return (_stack.Count != 0) ? _stack.Pop() : (T)_template.Clone();

            T obj;
            while((obj = (_stack.Count != 0) ? _stack.Pop() : (T)_template.Clone()) == null)
            {
                ;
            }
            return obj;
        }

        public void Back(T obj)
        {
            _stack.Push(obj);
        }
    }
}
