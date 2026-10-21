using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructuralToolbox.ArrayRecognition.Core;

namespace StructuralToolbox.ArrayRecognition.Commands
{
    public static class ArrayComponentReader
    {
        private const double PointTolerance = 0.01;
        private const double ClosureTolerance = 0.1;
        private const double AngleToleranceDegrees = 0.05;

        public static bool TryRead(Entity entity, Transaction transaction,
            out ArrayComponent component, out string reason)
        {
            component = null;
            reason = String.Empty;
            if (entity == null)
            {
                reason = "对象为空";
                return false;
            }
            Polyline lightweight = entity as Polyline;
            if (lightweight != null)
                return TryReadLightweight(lightweight, out component, out reason);
            Polyline2d legacy = entity as Polyline2d;
            if (legacy != null)
                return TryReadLegacy(legacy, transaction, out component, out reason);
            BlockReference block = entity as BlockReference;
            if (block != null)
                return TryReadBlock(block, transaction, out component, out reason);
            reason = "不支持对象类型 " + entity.GetType().Name;
            return false;
        }

        private static bool TryReadLightweight(Polyline polyline,
            out ArrayComponent component, out string reason)
        {
            component = null;
            reason = String.Empty;
            if (Math.Abs(polyline.Normal.X) > 1e-9 ||
                Math.Abs(polyline.Normal.Y) > 1e-9 ||
                polyline.Normal.Z < 0.999999999)
            {
                reason = "轻量多段线不是正 Z 法向的二维对象";
                return false;
            }
            List<ArrayVec2> vertices = new List<ArrayVec2>();
            for (int index = 0; index < polyline.NumberOfVertices; index++)
            {
                if (Math.Abs(polyline.GetBulgeAt(index)) > 1e-9)
                {
                    reason = "轻量多段线包含圆弧段";
                    return false;
                }
                Point2d point = polyline.GetPoint2dAt(index);
                vertices.Add(new ArrayVec2(point.X, point.Y));
            }
            return TryCreate(vertices, polyline.Closed, polyline.Elevation,
                polyline.Handle.ToString(), polyline.Layer,
                ArrayComponentKind.LightweightPolyline, out component, out reason);
        }

        private static bool TryReadLegacy(Polyline2d polyline,
            Transaction transaction, out ArrayComponent component, out string reason)
        {
            component = null;
            reason = String.Empty;
            if (Math.Abs(polyline.Normal.X) > 1e-9 ||
                Math.Abs(polyline.Normal.Y) > 1e-9 ||
                polyline.Normal.Z < 0.999999999)
            {
                reason = "二维多段线不是正 Z 法向对象";
                return false;
            }
            List<ArrayVec2> vertices = new List<ArrayVec2>();
            double elevation = 0.0;
            foreach (ObjectId id in polyline)
            {
                Vertex2d vertex = transaction.GetObject(id, OpenMode.ForRead, false)
                    as Vertex2d;
                if (vertex == null)
                    continue;
                if (Math.Abs(vertex.Bulge) > 1e-9)
                {
                    reason = "二维多段线包含圆弧段";
                    return false;
                }
                vertices.Add(new ArrayVec2(vertex.Position.X, vertex.Position.Y));
                elevation = vertex.Position.Z;
            }
            return TryCreate(vertices, polyline.Closed, elevation,
                polyline.Handle.ToString(),
                polyline.Layer, ArrayComponentKind.TwoDimensionalPolyline,
                out component, out reason);
        }

        private static bool TryReadBlock(BlockReference block,
            Transaction transaction, out ArrayComponent component, out string reason)
        {
            component = null;
            reason = String.Empty;
            BlockTableRecord definition = transaction.GetObject(
                block.BlockTableRecord, OpenMode.ForRead, false) as BlockTableRecord;
            if (definition == null)
            {
                reason = "块定义不可读";
                return false;
            }
            if (definition.IsFromExternalReference || definition.IsFromOverlayReference)
            {
                reason = "外部参照 Xref 不展开";
                return false;
            }
            Extents3d bounds = new Extents3d();
            bool hasBounds = false;
            foreach (ObjectId childId in definition)
            {
                Entity child = transaction.GetObject(childId, OpenMode.ForRead, false)
                    as Entity;
                if (child == null || child is AttributeDefinition)
                    continue;
                if (child is BlockReference)
                {
                    reason = "块定义包含嵌套块，不递归展开";
                    return false;
                }
                try
                {
                    Extents3d childBounds = child.GeometricExtents;
                    if (!hasBounds)
                    {
                        bounds = childBounds;
                        hasBounds = true;
                    }
                    else
                        bounds.AddExtents(childBounds);
                }
                catch
                {
                }
            }
            if (!hasBounds)
            {
                reason = "块定义没有可用外包框";
                return false;
            }
            double localX = bounds.MaxPoint.X - bounds.MinPoint.X;
            double localY = bounds.MaxPoint.Y - bounds.MinPoint.Y;
            if (localX <= PointTolerance || localY <= PointTolerance)
            {
                reason = "块定义外包框尺寸无效";
                return false;
            }
            Point3d localCenter = new Point3d(
                (bounds.MinPoint.X + bounds.MaxPoint.X) / 2.0,
                (bounds.MinPoint.Y + bounds.MaxPoint.Y) / 2.0,
                (bounds.MinPoint.Z + bounds.MaxPoint.Z) / 2.0);
            Point3d worldCenter = localCenter.TransformBy(block.BlockTransform);
            Vector3d worldX = Vector3d.XAxis.TransformBy(block.BlockTransform);
            Vector3d worldY = Vector3d.YAxis.TransformBy(block.BlockTransform);
            if (Math.Abs(worldX.Z) > 1e-8 || Math.Abs(worldY.Z) > 1e-8)
            {
                reason = "块参照不在世界 XY 平面内";
                return false;
            }
            double xLength = localX * worldX.Length;
            double yLength = localY * worldY.Length;
            if (Math.Abs(xLength - yLength) <= 2.0)
            {
                reason = "块外包框接近正方形，无法判断组件长轴";
                return false;
            }
            Vector3d longVector = xLength > yLength ? worldX : worldY;
            Vector3d shortVector = xLength > yLength ? worldY : worldX;
            component = new ArrayComponent
            {
                SourceId = block.Handle.ToString(),
                Kind = ArrayComponentKind.BlockReference,
                LayerName = block.Layer,
                Center = new ArrayVec2(worldCenter.X, worldCenter.Y),
                LongAxis = new ArrayVec2(longVector.X, longVector.Y).Normalized(),
                ShortAxis = new ArrayVec2(shortVector.X, shortVector.Y).Normalized(),
                LongLength = Math.Max(xLength, yLength),
                ShortLength = Math.Min(xLength, yLength),
                Elevation = worldCenter.Z
            };
            return true;
        }

        private static bool TryCreate(List<ArrayVec2> source, bool closedFlag,
            double elevation, string sourceId, string layer,
            ArrayComponentKind kind,
            out ArrayComponent component, out string reason)
        {
            component = null;
            reason = String.Empty;
            List<ArrayVec2> vertices;
            if (!ArrayComponentBoundaryReader.TryNormalize(source, closedFlag,
                PointTolerance, ClosureTolerance, out vertices, out reason))
                return false;
            if (vertices.Count != 4)
            {
                reason = "闭合轮廓不是四边形";
                return false;
            }
            ArrayVec2[] edges = new ArrayVec2[4];
            double[] lengths = new double[4];
            for (int index = 0; index < 4; index++)
            {
                edges[index] = vertices[(index + 1) % 4] - vertices[index];
                lengths[index] = edges[index].Length;
                if (lengths[index] <= PointTolerance)
                {
                    reason = "轮廓包含零长度边";
                    return false;
                }
            }
            double sine = Math.Sin(AngleToleranceDegrees * Math.PI / 180.0);
            for (int index = 0; index < 4; index++)
                if (Math.Abs(ArrayVec2.Dot(edges[index].Normalized(),
                    edges[(index + 1) % 4].Normalized())) > sine)
                {
                    reason = "四边形存在非直角";
                    return false;
                }
            if (Math.Abs(lengths[0] - lengths[2]) > 0.5 ||
                Math.Abs(lengths[1] - lengths[3]) > 0.5)
            {
                reason = "四边形对边长度不一致";
                return false;
            }
            double first = (lengths[0] + lengths[2]) / 2.0;
            double second = (lengths[1] + lengths[3]) / 2.0;
            if (Math.Abs(first - second) <= 2.0)
            {
                reason = "组件接近正方形，无法判断长轴";
                return false;
            }
            ArrayVec2 center = (vertices[0] + vertices[1] +
                vertices[2] + vertices[3]) / 4.0;
            component = new ArrayComponent
            {
                SourceId = sourceId, Kind = kind, LayerName = layer,
                Center = center, Elevation = elevation,
                LongAxis = first > second ? edges[0].Normalized() :
                    edges[1].Normalized(),
                ShortAxis = first > second ? edges[1].Normalized() :
                    edges[0].Normalized(),
                LongLength = Math.Max(first, second),
                ShortLength = Math.Min(first, second)
            };
            return true;
        }

    }
}
