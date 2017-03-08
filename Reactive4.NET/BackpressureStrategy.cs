using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET
{
    public enum BackpressureStrategy
    {
        MISSING,
        ERROR,
        BUFFER,
        LATEST
    }
}
