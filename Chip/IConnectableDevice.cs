using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESharp.Components
{
    public interface IConnectableDevice : IIODevice
    {
        IIODevice Bus { get; }

        void ConnectBus(IIODevice bus);
    }
}
