using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    /// <summary>
    /// Strategy of buffering events until the downstream
    /// is ready to receive them.
    /// </summary>
    public enum BufferStrategy
    {
        /// <summary>
        /// Signal an OnError if the downstream can't keep up.
        /// </summary>
        ERROR,
        /// <summary>
        /// Drop the oldest entry in the bounded buffer.
        /// </summary>
        DROP_OLDEST,
        /// <summary>
        /// Drop the newest entry in the bounded buffer.
        /// </summary>
        DROP_NEWEST,
        /// <summary>
        /// Buffer everything in an unbounded manner.
        /// </summary>
        ALL
    }
}
