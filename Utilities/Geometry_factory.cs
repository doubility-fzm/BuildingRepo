using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Ifc4.Interfaces;

namespace Utilities
{
    public class Geometry_factory
    {
        private static readonly double Tolerance = 1e-9;
        public static bool isSIUnits = true;
    }
}
