using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Vella.Common;
using Vella.UnityNativeHull;

public class BoxCreation
{
    public const int BoxSize = 1;

    public static NativeHull CreateBox() => HullFactory.CreateBox(BoxSize);

    [Test]
    public unsafe void Box_IsCreated()
    {
        using (var box = HullFactory.CreateBox(BoxSize))
        {
            Assert.IsTrue(box.IsCreated);
            Assert.IsFalse(box.IsDisposed);

            Assert.IsTrue((IntPtr)box.Faces != IntPtr.Zero);
            Assert.IsTrue((IntPtr)box.Vertices != IntPtr.Zero);
            Assert.IsTrue((IntPtr)box.Planes != IntPtr.Zero);
            Assert.IsTrue((IntPtr)box.Faces != IntPtr.Zero);
            Assert.IsTrue((IntPtr)box.Edges != IntPtr.Zero);
        }
    }

    [Test]
    public void Box_Vertices()
    {
        var validUnitBoxVertices = new HashSet<float3>
        {
            new float3(0.5f, 0.5f, -0.5f),
            new float3(-0.5f, 0.5f, -0.5f),
            new float3(-0.5f, -0.5f, -0.5f),
            new float3(0.5f, -0.5f, -0.5f),
            new float3(0.5f, 0.5f, 0.5f),
            new float3(-0.5f, 0.5f, 0.5f),
            new float3(-0.5f, -0.5f, 0.5f),
            new float3(0.5f, -0.5f, 0.5f)
        };

        using (var box = CreateBox())
        {
            Assert.IsTrue(box.VertexCount == 8);

            for (int i = 0; i < box.VertexCount; i++)
            {
                var vertex = box.GetVertex(i);

                Assert.IsTrue(validUnitBoxVertices.Contains(vertex),
                    "All vertices should be in one of eight specific positions");

                validUnitBoxVertices.Remove(vertex);
            }
        }
    }

    [Test]
    public void Box_Planes()
    {
        var validAABBNormals = new HashSet<Vector3>
        {
            Vector3.up, Vector3.down,
            Vector3.left, Vector3.right,
            Vector3.forward, Vector3.back,
        };
                
        var validPlaneOffset = BoxSize * 0.5f;

        using (var box = CreateBox())
        {
            Assert.IsTrue(box.FaceCount == 6);

            for (int i = 0; i < box.FaceCount; i++)
            {
                var plane = box.GetPlane(i);

                Assert.IsTrue(validAABBNormals.Contains(plane.Normal), 
                    "The plane normal of an Axis-Aligned box should be a base direction");

                Assert.IsTrue(plane.Offset == validPlaneOffset, 
                    "The plane offset from the centroid should be half the box size");
            }
        }
    }

    [Test]
    public void Box_Edges()
    {
        using (var box = CreateBox())
        {
            Assert.IsTrue(box.EdgeCount == 24);

            for (int i = 0; i < box.EdgeCount; i++)
            {
                HullValidation.ValidateEdge(box, i);
            }
        }
    }

    [Test]
    public void Box_Faces()
    {
        using (var box = CreateBox())
        {
            Assert.IsTrue(box.FaceCount == 6);

            for (int i = 0; i < box.FaceCount; i++)
            {
                HullValidation.ValidateFace(box, i);
            }
        }
    }



}


 