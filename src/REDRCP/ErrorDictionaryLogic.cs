using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kliskatek.Driver.Rain.REDRCP
{
    public partial class REDRCP
    {
        private readonly object _lockErrorDictionary = new();
        private ConcurrentDictionary<MessageCode, ErrorCode> _lastErrorDictionary = new();

        private void AddUpdateMessageCodeError(MessageCode messageCode, ErrorCode errorCode)
        {
            lock (_lockErrorDictionary)
            {
                _lastErrorDictionary.Remove(messageCode, out var _);
                _lastErrorDictionary.TryAdd(messageCode, errorCode);
            }
        }

        public bool TryGetCommandErrorCode(MessageCode messageCode, out ErrorCode errorCode)
        {
            lock (_lockErrorDictionary)
            {
                errorCode = ErrorCode.OtherError;
                if (!_lastErrorDictionary.TryGetValue(messageCode, out errorCode))
                    return false;
                return false;
            }
        }
    }
}
