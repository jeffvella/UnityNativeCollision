using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;
using Vella.UnityNativeHull;
using Debug = UnityEngine.Debug;

public class HullValidation 
{
    [Conditional("UNITY_EDITOR")]
    public unsafe static void ValidateHull(NativeHull hull)
    {
        Debug.Assert(hull.IsCreated);
        Debug.Assert(!hull.IsDisposed);

        Debug.Assert((IntPtr)hull.Faces != IntPtr.Zero);
        Debug.Assert((IntPtr)hull.Vertices != IntPtr.Zero);
        Debug.Assert((IntPtr)hull.Planes != IntPtr.Zero);
        Debug.Assert((IntPtr)hull.Faces != IntPtr.Zero);
        Debug.Assert((IntPtr)hull.Edges != IntPtr.Zero);
 
        for (int i = 0; i < hull.FaceCount; i++)
        {
            ValidateFace(hull, i);
        }

        CheckAllVerticesAreUsed(hull);
        CheckForInvalidUnusedEdges(hull);
    }

    [Conditional("UNITY_EDITOR")]
    public static unsafe void CheckAllVerticesAreUsed(NativeHull hull)
    {
        for (int i = 0; i < hull.VertexCount; i++)
        {
            bool isUsed = false;
            for (int j = 0; j < hull.EdgeCount; j++)
            {
                NativeHalfEdge edge = hull.GetEdge(j);
                if (edge.Origin == i)
                {
                    isUsed = true;
                    break;
                }
            }

            Debug.Assert(isUsed, 
                "All vertices should be used by an edge");
        }
    }

    [Conditional("UNITY_EDITOR")]
    public static void CheckForInvalidUnusedEdges(NativeHull hull)
    {
        for (int j = 0; j < hull.EdgeCount; j++)
        {  
            ValidateEdge(hull, j);
        }
    }

    [Conditional("UNITY_EDITOR")]
    public static void ValidateFace(NativeHull hull, int faceIndex)
    {
        if (faceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(faceIndex));

        NativeFace face = hull.GetFace(faceIndex);

        Debug.Assert(face.Edge >= 0,
            "All faces should point to a starting edge index");

        Debug.Assert(face.Edge < hull.EdgeCount,
            "A face references an out of range edge index");

        NativeHalfEdge startEdge = hull.GetEdge(face.Edge);
        NativeHalfEdge current = startEdge;

        int currentIndex = face.Edge;
        var edgeCount = 0;

        do
        {            
            var next = hull.GetEdge(current.Next);

            Debug.Assert(faceIndex == current.Face,
                "All edges in a face loop should point to the same face");

            Debug.Assert(currentIndex == next.Prev,
                "Next and previous edges in a face loop should point to each other");

            ValidateEdge(hull, currentIndex);

            if (++edgeCount >= hull.EdgeCount)
            {
                Debug.Assert(true, "Infinite loop in face edges");
                break;
            }

            currentIndex = current.Next;
            current = next;
        }
        while (current.Origin != startEdge.Origin);

        Debug.Assert(edgeCount > 1,
            "Faces should have more than one edge");
    }

    [Conditional("UNITY_EDITOR")]
    public static void ValidateEdge(NativeHull hull, int edgeIndex)
    {
        if (edgeIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(edgeIndex));

        var edge = hull.GetEdge(edgeIndex);

        if (edge.Twin == -1 || edge.Prev == -1 || edge.Next == -1)
        {            
            Debug.LogError($"Edge {edgeIndex} has an out of range index for twin, prev or next");

            // Avoid exceptions so our debug visualizations can aid in debugging the issue.
            return;
        }

        var twin = edge.GetTwin(hull);

        Debug.Assert(edgeIndex == twin.Twin,
            "The twin of the edge twin must be the edge itself");

        Debug.Assert(math.abs(edge.Twin - edgeIndex) == 1,
            "An edge/twin combination should be indexed one directly after the other.");

        Debug.Assert(edgeIndex == hull.GetEdge(edge.Prev).Next,
            "The previous edge should point to the next edge");

        Debug.Assert(edgeIndex == hull.GetEdge(edge.Next).Prev,
            "The next edge should point to the previous edge");

        Debug.Assert(edge.Origin != twin.Origin,
            "Edges and their twin should not point to the same vertex");

        Debug.Assert(edge.Face >= 0,
            "All edges should have a face index");

        Debug.Assert(edge.Face < hull.FaceCount,
            "An edge references an out of range face index");

    }
}
