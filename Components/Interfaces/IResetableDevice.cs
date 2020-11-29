using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESharp.Components.Interfaces
{
    public interface IResetableDevice
    {
        void Reset(bool hardReset = false);
    }
}
