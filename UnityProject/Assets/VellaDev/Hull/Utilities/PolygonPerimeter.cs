using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace VellaDev.Hull
{
    public struct PolygonPerimeter
    {
        public struct Edge
        {
            public int StartIndex;
            public int EndIndex;
        }

        private static readonly List<Edge> OutsideEdges = new List<Edge>();

        public static List<Edge> CalculatePerimeter(int[] indices, ref List<float3> verts)
        {
            OutsideEdges.Clear();

            for (int i = 0; i < indices.Length - 1; i += 3)
            {
                int v3;
                int v2;
                int v1;

                v1 = indices[i];
                v2 = indices[i + 1];
                v3 = indices[i + 2];
               
                AddOutsideEdge(v1, v2);
                AddOutsideEdge(v2, v3);
                AddOutsideEdge(v3, v1);
            }

            // Check for crossed edges.
            for (int i = 0; i < OutsideEdges.Count; i++)
            {
                var edge = OutsideEdges[i];
                var nextIdx = i + 1 > OutsideEdges.Count - 1 ? 0 : i + 1;
                var next = OutsideEdges[nextIdx];
                if (edge.EndIndex != next.StartIndex)
                {
                    return Rebuild();
                }
            }

            return OutsideEdges;
        }

        private static void AddOutsideEdge(int i1, int i2)
        {
            foreach (var edge in OutsideEdges)
            {
                // If each edge was already added, then it's a shared edge with another triangle - exclude them both.
                if (edge.StartIndex == i1 & edge.EndIndex == i2 || edge.StartIndex == i2 & edge.EndIndex == i1)
                {
                    OutsideEdges.Remove(edge);
                    return;
                }
            }

            OutsideEdges.Add(new Edge { StartIndex = i1, EndIndex = i2 });
        }

        private static List<Edge> Rebuild()
        {
            var result = new List<Edge>();
            var map = OutsideEdges.ToDictionary(k => k.StartIndex, v => v.EndIndex);
            var cur = OutsideEdges.First().StartIndex;
            for (int i = 0; i < OutsideEdges.Count; i++)
            {
                var edge = new Edge
                {
                    StartIndex = cur,
                    EndIndex = map[cur]
                };
                result.Add(edge);
                cur = edge.EndIndex;
            }

            return result;
        }
    }
}
