using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    internal interface IWorkerServices
    {
        bool AddAction(InterruptibleAction action);

        void DeleteAction(InterruptibleAction action);
    }
}
