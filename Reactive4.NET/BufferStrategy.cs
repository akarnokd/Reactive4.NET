using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    public enum BufferStrategy
    {
        ERROR,
        DROP_OLDEST,
        DROP_NEWEST,
        ALL
    }
}
