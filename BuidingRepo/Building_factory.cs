using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MaterialResource;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Ifc4.SharedComponentElements;
using Xbim.IO;

namespace BuildingRepo
{
    public class Building_factory:IDisposable
    {
        private readonly string _outputPath = "";
        private readonly string _projectName = "xx工程";
        private readonly string _buildingName = "xx楼板";
        private Dictionary<int, (List<double> spancing, List<double> span, double)> _placementMap = new Dictionary<int, (List<double> spancing, List<double> span, double)>();

        private readonly IfcStore _model;//using external Alignment model as refenrence

        private IfcStore CreateAndInitModel(string projectname)
        {
            //first we need register essential information for the project
            var credentials = new XbimEditorCredentials
            {
                ApplicationDevelopersName = "hsx",
                ApplicationFullName = "IFC Model_Alignment for Building",
                ApplicationIdentifier = "",
                ApplicationVersion = "1.0",
                EditorsFamilyName = "HE",
                EditorsGivenName = "Shixin",
                EditorsOrganisationName = "TJU"
            };
            //create model by using method in IfcStore class,using memory mode,and IFC4x1 format
            var model = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4x1, XbimStoreType.InMemoryModel);
                                                                                                                                                                                                             
            //begin a transition when do any change in a model
            using (var txn = model.BeginTransaction("Initialise Model"))
            {
                //add new IfcProject item to a certain container
                var project = model.Instances.New<IfcProject>
                    (p =>
                    {
                        //Set the units to SI (mm and metres)                      
                        p.Initialize(ProjectUnits.SIUnitsUK);
                        p.Name = projectname;
                    });
                // Now commit the changes, else they will be rolled back 
                // at the end of the scope of the using statement
                txn.Commit();
            }
            return model;
        }

        public Building_factory(string outputPath= "../../TestFiles/girder.ifc")
        {
            _model = CreateAndInitModel(_projectName);
            InitWCS();           
            _outputPath = outputPath;
        }

        private IfcCartesianPoint Origin3D { get; set; }
        private IfcDirection AxisX3D { get; set; }
        private IfcDirection AxisY3D { get; set; }
        private IfcDirection AxisZ3D { get; set; }
        private IfcAxis2Placement3D WCS { get; set; }
        private IfcCartesianPoint Origin2D { get; set; }
        private IfcDirection AxisX2D { get; set; }
        private IfcDirection AxisY2D { get; set; }
        private IfcAxis2Placement2D WCS2D { get; set; }
        private void InitWCS()
        {
            using (var txn = this._model.BeginTransaction("Initialise WCS"))
            {
                var context3D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
                if (context3D.WorldCoordinateSystem is IfcAxis2Placement3D wcs)
                {
                    WCS = wcs;
                    Origin3D = wcs.Location;
                    AxisZ3D = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    wcs.Axis = AxisZ3D;
                    AxisX3D = toolkit_factory.MakeDirection(_model, 1, 0, 0);
                    wcs.RefDirection = AxisX3D;
                    AxisY3D = toolkit_factory.MakeDirection(_model, 0, 1, 0);
                }

                var context2D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 2)
                    .FirstOrDefault();
                if (context2D.WorldCoordinateSystem is IfcAxis2Placement2D wcs2d)
                {
                    WCS2D = wcs2d;
                    Origin2D = wcs2d.Location;
                    AxisX2D = toolkit_factory.MakeDirection(_model, 1, 0);
                    wcs2d.RefDirection = AxisX2D;
                    AxisY2D = toolkit_factory.MakeDirection(_model, 0, 1);
                }

                txn.Commit();
            }
        }
        public void Dispose()
        {
            _model.Dispose();
        }



//         private List<double> Column_spacing { get; set; }
//         private List<double> Column_span { get; set; }

        private Dictionary<int,(List<double> spacing,List<double> span, double height)> ColumnPara { get; set; }
        public void PlateBuild()
        {
            //写创建过程
            var site = toolkit_factory.CreateSite(_model, "Structure Site");

            (double, double, double) profile_p1 = (0, 0, 0);
            (double, double, double) profile_p2 = (100, 100, 100);
            (double, double, double, double, double, double) plate_profile = (-150, 0, 0, 150, 0, 0); 
            var plate = CreatePlate(profile_p1, profile_p2, plate_profile);

            toolkit_factory.AddPrductIntoSpatial(_model, site, plate, "Add plate to site");

            _model.SaveAs(_outputPath, StorageType.Ifc);
        }

        private IfcPlate CreatePlate((double x, double y, double z)startPoint, (double x, double y, double z) endPoint,
            (double x1, double y1, double z1, double x2, double y2, double z2) LineProfile)
        {
            using (var txn = this._model.BeginTransaction("CreatePlate"))
            {
                var plate = this._model.Instances.New<IfcPlate>();
                plate.Name = "testPlate";
                plate.ObjectType = "Single_Plate";

                var point1 = toolkit_factory.MakeCartesianPoint(_model, startPoint.x, startPoint.y, startPoint.z);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, endPoint.x, endPoint.y, endPoint.z);

                var profile_point1 = toolkit_factory.MakeCartesianPoint(_model, LineProfile.x1, LineProfile.y1, LineProfile.z1);
                var profile_point2 = toolkit_factory.MakeCartesianPoint(_model, LineProfile.x2, LineProfile.y2, LineProfile.z2);
                var profile = toolkit_factory.MakeCenterLineProfile(_model, profile_point1, profile_point2, 20);
                                                                                  //the thickness of the plate is 20 !!!
        
                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>(); //extruded area solid:拉伸区域实体。
                                                                //有四个重要参数：SweptArea、ExtrudedDirection、Position、Depth
                solid.SweptArea = profile;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0,0,1);   //拉伸方向为z轴
                var solid_direction=toolkit_factory.MakeDirection(_model, point1, point2);
                solid.Position = toolkit_factory.MakeLocalAxisPlacement(_model, point1, solid_direction);
                solid.Depth = toolkit_factory.GetLength(point1, point2);

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                plate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                plate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;

                txn.Commit();
                return plate;
            }
        }

        //private double GetLength(IfcCartesianPoint p1, IfcCartesianPoint p2)
        //{
        //    return Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y) + (p1.Z - p2.Z) * (p1.Z - p2.Z));
        //}
        //挪到了toolkit中使用。


        public void BeamBuild()
        {
            var site = toolkit_factory.CreateSite(_model, "Structure Site");

            //600*300*3000的梁
            //建立梁所需参数：形心点、拉伸方向上的一点、梁截面参数（梁宽w，梁高h)
            (double, double, double) shape_heart = (0, 0, 0);
            (double, double, double) extruded_point = (3000, 0, 0);
            double width = 300;
            double height = 600;

            var beamR = CreateBeam(shape_heart, extruded_point, width, height);
            toolkit_factory.AddPrductIntoSpatial(_model, site, beamR ,"Add plate to site");

            ////以300*300*10*15的工字型截面为例（w1=w3=300,w2=10,t1=t3=15,t2=300-15*2=270）
            ////建立工字型截面梁所需参数：形心点、拉伸方向上的点、梁截面参数（上翼缘宽度w1 & 高度t1、腹板宽度w2 & 高度t2、下翼缘宽度w3 & 高度t3）
            //double w1 = 300, w2 = 10, w3 = 300;
            //double t1 = 15, t2 = 270, t3 = 15;
            //var beamI = CreateBeam(shape_heart, extruded_point, w1, w2, w3, t1, t2, t3);


            _model.SaveAs(_outputPath, StorageType.Ifc);
        }


        private IfcBeam CreateBeam((double x, double y, double z) shape_heart, (double x, double y, double z) extruded_point,double width, double height)
        {
            using (var txn = this._model.BeginTransaction("CreateBeam"))
            {
                var beam = this._model.Instances.New<IfcBeam>();
                beam.Name = "testBeam";
                beam.ObjectType = "Single_Beam";

                var point1 = toolkit_factory.MakeCartesianPoint(_model, shape_heart);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, extruded_point);

                var profile_point1 = toolkit_factory.MakeCartesianPoint(_model, shape_heart.x-width/2, shape_heart.y-height/2, shape_heart.z);
                var profile_point2 = toolkit_factory.MakeCartesianPoint(_model, shape_heart.x+width/2, shape_heart.y-height/2, shape_heart.z);
                var profile = toolkit_factory.MakeCenterLineProfile(_model, profile_point1, profile_point2, height);

                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
                solid.SweptArea = profile;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                var solid_direction = toolkit_factory.MakeDirection(_model, point1, point2);
                solid.Position = toolkit_factory.MakeLocalAxisPlacement(_model, point1, solid_direction);
                solid.Depth = toolkit_factory.GetLength(point1, point2);

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                beam.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                beam.PredefinedType = IfcBeamTypeEnum.USERDEFINED;
                                
                txn.Commit();
                return beam;
            }

        }



        //private IfcBeam CreateBeam((double x, double y, double z) shape_heart, (double x, double y, double z) extruded_point, double w1, double w2, double w3, double t1, double t2, double t3)
        //{
        //    using (var txn = this._model.BeginTransaction("CreateBeam"))
        //    {
        //        var beam = this._model.Instances.New<IfcBeam>();
        //        beam.Name = "testBeam";
        //        beam.ObjectType = "Single_Beam";

        //        var point1 = toolkit_factory.MakeCartesianPoint(_model, shape_heart);
        //        var point2 = toolkit_factory.MakeCartesianPoint(_model, extruded_point);



        //    }
        //}
        public void GeneratePlacementMap(List<List<double>> Column_spacing, List<List<double>> Column_span,List<double> height)
        {
            if (Column_spacing.Count != Column_span.Count)
                throw new InvalidOperationException("Must Pair the spacing and span");
            //这里还要判断那个高度是不是匹配
            for(int i=0;i<Column_spacing.Count;i++)
            {
                _placementMap[i] = (Column_spacing[i], Column_span[i], height[i]);
            }
        }

        public List<List<(IfcCartesianPoint,double height)>> ParsePlacementMap(Dictionary<int, (List<double> spancing, List<double> span, double height)> placementMap)
        {
            var placementSet = new List<List<(IfcCartesianPoint, double height)>>();
            double x = 0;
            double y = 0;
            for (int i=0;i<placementMap.Count;i++)
            {
                var singlePlacementSet = new List<(IfcCartesianPoint, double height)>();
                for(int j=0;j<placementMap[i].spancing.Count;j++)
                {
                    x = x + placementMap[i].spancing[j];
                    for(int k=0;k<placementMap[i].span.Count;k++)
                    {
                        y = y + placementMap[i].span[k];
                        using (var txn = this._model.BeginTransaction("Generate Placemment Point"))
                        {
                            var point = toolkit_factory.MakeCartesianPoint(_model, x, y, 0);
                            singlePlacementSet.Add((point, placementMap[i].height));
                            txn.Commit();
                        }
                    }
                }
                placementSet.Add(singlePlacementSet);
            }
            return placementSet;
        }

        public void ColumnBuild()
        {
            var site = toolkit_factory.CreateSite(_model, "Structrue Site");

            //(double, double, double) profile_p1 = (0, 0, 0);
            //(double, double, double) profile_p2 = (0, 0, 3000);
            (double, double, double, double, double, double) column_profile = (-200, 0, 0, 200, 0, 0);


            var Ccolumn = new List<IfcColumn>();

            var Map = ParsePlacementMap(_placementMap);

          //  double Layer_height = 3000;          //层高3m

            //for(int j=0;j<Column_span.Count;j++)
            //{
            //    for (int i = 0; i <Column_spacing.Count; i++)
            //    {
            //        Ccolumn[n++] = CreateColumn((Column_spacing[i] * i, Column_span[j]*j, 0), (Column_spacing[i] * (i+1), Column_span[j] * j, Layer_height), column_profile);

            //        toolkit_factory.AddPrductIntoSpatial(_model, site, Ccolumn[n], "Add column to site");
            //    }
            //}

            //for (int i = 1; i <= Column_spacing.Count; i++)
            //{
            //    Ccolumn[i] = CreateColumn((Layer_height * (i - 1), 0, 0), (3000 * (i - 1), 0, Layer_height), column_profile);

            //    toolkit_factory.AddPrductIntoSpatial(_model, site, Ccolumn[i], "Add column to site");
            //}

            //for (int i =4; i<=6;i++)
            //{
            //    Ccolumn[i] = CreateColumn((Layer_height * (i - 4), 3000, 0), (3000 * (i - 4), 3000, Layer_height), column_profile);
            //    toolkit_factory.AddPrductIntoSpatial(_model, site, Ccolumn[i], "Add column to site");
            //}

            //var column1 = CreateColumn((0, 0, 0), (0, 0, 3000), column_profile);
            //var column2 = CreateColumn((3000, 0, 0), (3000, 0, 3000), column_profile);
            //var column3 = CreateColumn((6000, 0, 0), (6000, 0, 3000), column_profile);
            //var column4 = CreateColumn((0, 3000, 0), (0, 3000, 3000), column_profile);
            //var column5 = CreateColumn((3000, 3000, 0), (3000, 3000, 3000), column_profile);
            //var column6 = CreateColumn((6000, 3000, 0), (6000, 3000, 3000), column_profile);

            //toolkit_factory.AddPrductIntoSpatial(_model, site, column1, "Add column to site");
            //toolkit_factory.AddPrductIntoSpatial(_model, site, column2, "Add column to site");
            //toolkit_factory.AddPrductIntoSpatial(_model, site, column3, "Add column to site");
            //toolkit_factory.AddPrductIntoSpatial(_model, site, column4, "Add column to site");
            //toolkit_factory.AddPrductIntoSpatial(_model, site, column5, "Add column to site");
            //toolkit_factory.AddPrductIntoSpatial(_model, site, column6, "Add column to site");

            _model.SaveAs(_outputPath, StorageType.Ifc);

        }

        private IfcColumn CreateColumn((double x, double y, double z) startPoint, (double x, double y, double z) endPoint,
            (double x1, double y1, double z1, double x2, double y2, double z2) LineProfile)
        {
            using (var txn = this._model.BeginTransaction("CreateColumn"))
            {
                var column = this._model.Instances.New<IfcColumn>();
                column.Name = "testColumn";
                column.ObjectType = "Single_Column";

                var point1 = toolkit_factory.MakeCartesianPoint(_model, startPoint.x, startPoint.y,startPoint.z);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, endPoint.x, endPoint.y, endPoint.z);

                var profile_point1 = toolkit_factory.MakeCartesianPoint(_model, LineProfile.x1, LineProfile.y1, LineProfile.z1);
                var profile_point2 = toolkit_factory.MakeCartesianPoint(_model, LineProfile.x2, LineProfile.y2, LineProfile.z2);
                var profile = toolkit_factory.MakeCenterLineProfile(_model, profile_point1, profile_point2, 400);


                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
                solid.SweptArea = profile;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                var solid_direction = toolkit_factory.MakeDirection(_model, point1, point2);
                solid.Position = toolkit_factory.MakeLocalAxisPlacement(_model, point1, solid_direction);
                solid.Depth = toolkit_factory.GetLength(point1, point2);


                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                column.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                column.PredefinedType = IfcColumnTypeEnum.USERDEFINED;


                txn.Commit();
                return column;

            }

        }
    }
}
