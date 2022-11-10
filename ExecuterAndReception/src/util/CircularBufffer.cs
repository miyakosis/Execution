using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecuterAndReception.src.util
{
    /**
     * 循環バッファ
     * 
     * thread safe について:
     * index(_head, _tail) のチェック → buffer の操作 → index の操作の順番で実施しているため、
     * Push() と Pop() は thread safe な操作(片方を実行中にもう片方を呼んでもよい)になっている。
     * Push() や Pop() 自体は thread safe ではないため、複数スレッドから同時に呼び出してはならない。     
     */
    internal class CircularBufffer
    {
        private byte[] _buffer;
        private int _bufferSize;
        private int _mask;
        private int _elementSize;
        private int _head = 0;
        private int _tail = 0;

        public CircularBufffer(int initialSize, int elementSize)
        {
            _bufferSize = initialSize;
            _mask = initialSize - 1;
            _elementSize = elementSize;
            _buffer = new byte[initialSize * elementSize];
        }

        public void Enqueue(byte[] element)
        {
            var nextTail = (_tail + 1) & _mask;
            if (nextTail == _head)               
            {   // TODO: 自動伸長などを検討する
                throw new NotImplementedException("CircularBufffer: buffer full");
            }

            Buffer.BlockCopy(element, 0, _buffer, _tail * _elementSize, _elementSize);
            _tail = nextTail;
        }

        public Span<byte> Deque()
        {
            if (_head == _tail)
            {
                return null;
            }

            var span = new Span<byte>(_buffer, _head * _elementSize, _elementSize);            
            _tail = (_tail + 1) & _mask;
            return span;
        }
    }
}
