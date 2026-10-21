using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructuralToolbox.Core;

namespace StructuralToolbox.Commands
{
    internal static class PanelRectangleReader
    {
        private const double DuplicatePointTolerance = 0.01;
        private const double RightAngleToleranceDegrees = 2.0;

        public static bool TryRead(Polyline polyline, out PanelRect panel, out string reason)
        {
            panel = null;
            reason = String.Empty;

            if (polyline == null)
            {
                reason = "对象为空";
                return false;
            }
            if (!polyline.Closed)
            {
                reason = "多段线未闭合";
                return false;
            }
            if (Math.Abs(polyline.Normal.X) > 1e-9 ||
                Math.Abs(polyline.Normal.Y) > 1e-9 ||
                polyline.Normal.Z < 0.999999999)
            {
                reason = "多段线不是正Z法向的二维对象";
                return false;
            }

            List<Vec2> vertices = new List<Vec2>();
            for (int index = 0; index < polyline.NumberOfVertices; index++)
            {
                if (Math.Abs(polyline.GetBulgeAt(index)) > 1e-9)
                {
                    reason = "多段线包含圆弧段";
                    return false;
                }

                Point2d point = polyline.GetPoint2dAt(index);
                Vec2 vertex = new Vec2(point.X, point.Y);
                if (vertices.Count == 0 ||
                    Vec2.Distance(vertices[vertices.Count - 1], vertex) > DuplicatePointTolerance)
                {
                    vertices.Add(vertex);
                }
            }

            if (vertices.Count > 1 &&
                Vec2.Distance(vertices[0], vertices[vertices.Count - 1]) <= DuplicatePointTolerance)
            {
                vertices.RemoveAt(vertices.Count - 1);
            }

            if (vertices.Count != 4)
            {
                reason = "闭合多段线不是四边形";
                return false;
            }

            Vec2[] edges = new Vec2[4];
            double[] lengths = new double[4];
            for (int index = 0; index < 4; index++)
            {
                edges[index] = vertices[(index + 1) % 4] - vertices[index];
                lengths[index] = edges[index].Length;
                if (lengths[index] <= DuplicatePointTolerance)
                {
                    reason = "多段线包含零长度边";
                    return false;
                }
            }

            double rightAngleSine = Math.Sin(RightAngleToleranceDegrees * Math.PI / 180.0);
            for (int index = 0; index < 4; index++)
            {
                Vec2 first = edges[index].Normalized();
                Vec2 second = edges[(index + 1) % 4].Normalized();
                if (Math.Abs(Vec2.Dot(first, second)) > rightAngleSine)
                {
                    reason = "四边形存在非直角";
                    return false;
                }
            }

            if (!NearlySameLength(lengths[0], lengths[2]) ||
                !NearlySameLength(lengths[1], lengths[3]))
            {
                reason = "四边形对边长度不一致";
                return false;
            }

            Vec2 diagonalCenter1 = (vertices[0] + vertices[2]) / 2.0;
            Vec2 diagonalCenter2 = (vertices[1] + vertices[3]) / 2.0;
            if (Vec2.Distance(diagonalCenter1, diagonalCenter2) > 2.0)
            {
                reason = "四边形对角线中心不重合";
                return false;
            }

            double firstLength = (lengths[0] + lengths[2]) / 2.0;
            double secondLength = (lengths[1] + lengths[3]) / 2.0;
            if (Math.Abs(firstLength - secondLength) <= 2.0)
            {
                reason = "组件接近正方形，无法判断长轴";
                return false;
            }

            Vec2 longAxis;
            Vec2 shortAxis;
            double longLength;
            double shortLength;
            if (firstLength > secondLength)
            {
                longAxis = edges[0].Normalized();
                shortAxis = edges[1].Normalized();
                longLength = firstLength;
                shortLength = secondLength;
            }
            else
            {
                longAxis = edges[1].Normalized();
                shortAxis = edges[0].Normalized();
                longLength = secondLength;
                shortLength = firstLength;
            }

            Vec2 center = (vertices[0] + vertices[1] + vertices[2] + vertices[3]) / 4.0;
            panel = new PanelRect(center, longAxis, shortAxis, longLength, shortLength,
                polyline.Elevation, polyline.Handle.ToString());
            return true;
        }

        private static bool NearlySameLength(double first, double second)
        {
            double tolerance = Math.Max(2.0, Math.Max(first, second) * 0.005);
            return Math.Abs(first - second) <= tolerance;
        }
    }
}
