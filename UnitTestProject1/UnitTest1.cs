using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.ProductExtension;
using BuildingRepo;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            using (var plateconst = new Building_factory())
            {
                plateconst.PlateBuild();
            }
        }
        [TestMethod]
        public void TestMethod2()
        {
            using (var beamconst = new Building_factory())
            {
                beamconst.BeamBuild();
            }
        }

        [TestMethod]
        public void TextMethod3()
        {
            using (var columnconst = new Building_factory())
            {
                var Column_spacing = new List<List<double>>() { new List<double>(){ 8000, 8000, 8000, 8000, 8000, 8000},
                    new List<double>(){ 8000, 8000, 8000, 8000, 8000, 8000}};//柱距8m

                var Column_span = new List<List<double>>() { new List<double>() { 7200, 7200, 7200 },
                new List<double>(){ 7200, 7200, 7200 } };
                var Layer_height = new List<double>() { 3000, 3000 };

                columnconst.GeneratePlacementMap(Column_spacing, Column_span, Layer_height);
                columnconst.ColumnBuild();
            }
        }
    }
}

