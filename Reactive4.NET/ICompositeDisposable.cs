using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    public interface ICompositeDisposable : IDisposable
    {
        bool Add(IDisposable d);

        bool Remove(IDisposable d);

        bool Delete(IDisposable d);

        void Clear();

        bool IsDisposed();
    }
}
