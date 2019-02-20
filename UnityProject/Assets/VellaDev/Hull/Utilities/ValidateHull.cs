using Unity.Mathematics;
using UnityEngine;

namespace VellaDev.Hull
{
    public static class ValidateHull
    {
        public static unsafe void Validate(this NativeHull hull)
        {
            Debug.Assert(hull.faceCount > 0);
            Debug.Assert(hull.edgeCount > 0);

            for (int i = 0; i < hull.faceCount; ++i)
            {
                ValidateFace(hull, hull.faces + i);
            }
        }

        public static unsafe void ValidateFace(this NativeHull hull, NativeFace* face)
        {
            Debug.Assert(hull.faceCount > 0);
            Debug.Assert(hull.edgeCount > 0);
            Debug.Assert(face->edge != -1);

            ValidateEdge(hull, hull.edges + face->edge);
        }

        public static unsafe void ValidateEdge(this NativeHull hull, NativeHalfEdge* edge)
        {
            Debug.Assert(hull.faceCount > 0);
            Debug.Assert(hull.edgeCount > 0);
            Debug.Assert(edge->twin != -1);

            NativeHalfEdge* curTwin = hull.edges + edge->twin;

            int edgeIndex = (int)(edge - hull.edges);

            Debug.Assert(curTwin->twin == edgeIndex, "The twin of the edge twin must be the edge itself");
            Debug.Assert(math.abs(edge->twin - edgeIndex) == 1, "The two edges must be close by one index.");
            Debug.Assert(hull.edges[edge->prev].next == edgeIndex, "The twin of the edge twin must be the edge");
            Debug.Assert(edge->origin != curTwin->origin, "Edges and their twin must point to each others' origin vertex");

            int count = 0;
            NativeHalfEdge* start = edge;
            do
            {
                NativeHalfEdge* next = hull.edges + edge->next;
                NativeHalfEdge* twin = hull.edges + next->twin;
                edge = twin;

                Debug.Assert(edge->face != -1, "All edges must have a face index");

                bool infiniteLoop = count > hull.edgeCount;
                if (count > hull.edgeCount)
                {
                    Debug.Assert(true, "Possible infinite Edge Loop");
                    break;
                }
                ++count;
            }
            while (edge != start);

        }
    }
}

