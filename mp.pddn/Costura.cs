using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mp.pddn
{
    /// <summary>
    /// Costura assembly merger initializer
    /// </summary>
    public static class Costura
    {
        static Costura()
        {
            CosturaUtility.Initialize();
        }
    }
}
