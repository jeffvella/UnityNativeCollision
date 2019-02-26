using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Vella.Common;
using Vella.UnityNativeHull;

public class BoxCollision
{
    public const int BoxSize = 1;

    public static NativeHull CreateBox() => HullFactory.CreateBox(BoxSize);

    [Test]
    public void Box_CornerOnCorner()
    {
        using (var box = CreateBox())
        {
            var insideUnitRangePosition = new float3(0.99f, 0.99f, 0.99f);
            var outsideUnitRangePosition = new float3(1.01f, 1.01f, 1.01f);

            var insideUnitRange = new RigidTransform(float3x3.identity, insideUnitRangePosition);
            var outsideUnitRange = new RigidTransform(float3x3.identity, outsideUnitRangePosition);

            Assert.IsTrue(NativeCollision.IsCollision(insideUnitRange, box, RigidTransform.identity, box));
            Assert.IsFalse(NativeCollision.IsCollision(outsideUnitRange, box, RigidTransform.identity, box));
        }
    }

    [Test]
    public void Box_FaceOnFace()
    {
        using (var box = CreateBox())
        {
            var testTransform = new RigidTransform();

            testTransform.pos = Vector3.down * 0.99f;
            Assert.IsTrue(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.down * 1.01f;
            Assert.IsFalse(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.up * 0.99f;
            Assert.IsTrue(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.up * 1.01f;
            Assert.IsFalse(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.back * 0.99f;
            Assert.IsTrue(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.back * 1.01f;
            Assert.IsFalse(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.forward * 0.99f;
            Assert.IsTrue(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.forward * 1.01f;
            Assert.IsFalse(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.right * 0.99f;
            Assert.IsTrue(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.right * 1.01f;
            Assert.IsFalse(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.left * 0.99f;
            Assert.IsTrue(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));

            testTransform.pos = Vector3.left * 1.01f;
            Assert.IsFalse(NativeCollision.IsCollision(testTransform, box, RigidTransform.identity, box));
        }
    }

    [Test]
    public void Box_PointOnFace()
    {
        using (var box = CreateBox())
        {
            HullDrawingUtility.DrawBasicHull(box, RigidTransform.identity, Color.black, 100);

            var rotateEveryAxis45 = Quaternion.Euler((float3)45);

            // up and rotated position
            var insideUnitBox = new RigidTransform(rotateEveryAxis45, new float3(0.2f, 1.34f, 0.1f));
            var outsideUnitBox = new RigidTransform(rotateEveryAxis45, new float3(0.2f, 1.38f, 0.1f));

            Assert.IsTrue(NativeCollision.IsCollision(insideUnitBox, box, RigidTransform.identity, box));
            Assert.IsFalse(NativeCollision.IsCollision(outsideUnitBox, box, RigidTransform.identity, box));

            HullDrawingUtility.DrawBasicHull(box, insideUnitBox, Color.blue, 100);
            HullDrawingUtility.DrawBasicHull(box, outsideUnitBox, Color.black, 100);

            // down and rotated position
            insideUnitBox = new RigidTransform(rotateEveryAxis45, new float3(0.2f, -1.34f, 0.1f));
            outsideUnitBox = new RigidTransform(rotateEveryAxis45, new float3(0.2f, -1.38f, 0.1f));

            Assert.IsTrue(NativeCollision.IsCollision(insideUnitBox, box, RigidTransform.identity, box));
            Assert.IsFalse(NativeCollision.IsCollision(outsideUnitBox, box, RigidTransform.identity, box));

            HullDrawingUtility.DrawBasicHull(box, insideUnitBox, Color.blue, 100);
            HullDrawingUtility.DrawBasicHull(box, outsideUnitBox, Color.black, 100);
        }
    }

    [Test]
    public void Box_EdgeOnEdge()
    {
        using (var box = CreateBox())
        {
            HullDrawingUtility.DrawBasicHull(box, RigidTransform.identity, Color.black, 100);

            var rotateEveryAxis45 = Quaternion.Euler((float3)45);
            var insideUnitBox = new RigidTransform(rotateEveryAxis45, new float3(0.2f, 1.1f, -0.80f));
            var outsideUnitBox = new RigidTransform(rotateEveryAxis45, new float3(0.184f, 1.123f, -0.816f));

            Assert.IsTrue(NativeCollision.IsCollision(insideUnitBox, box, RigidTransform.identity, box));            
            Assert.IsFalse(NativeCollision.IsCollision(outsideUnitBox, box, RigidTransform.identity, box));

            HullDrawingUtility.DrawBasicHull(box, insideUnitBox, Color.blue, 100);
            HullDrawingUtility.DrawBasicHull(box, outsideUnitBox, Color.black, 100);

        }
    }

    [Test]
    public void Box_EdgeSeparation()
    {      
        using (var box = CreateBox())
        {
            // These two boxes are positioned so that both face SAT checks would show no separation,
            // a working SAT edge test is required to catch the false positive.

            var boxTransformA = new RigidTransform(Quaternion.Euler(-24.357f, -4.779f, -32.115f), new float3(-0.089f, -0.821f, -2.233f));
            var boxTransformB = new RigidTransform(Quaternion.Euler(55.943f, 21.207f, 47.057f), new float3(-0.207f, -0.06f, -1.256f));

            Assert.IsFalse(NativeCollision.IsCollision(boxTransformA, box, boxTransformB, box));

            HullDrawingUtility.DrawBasicHull(box, boxTransformA, Color.blue, 100);
            HullDrawingUtility.DrawBasicHull(box, boxTransformB, Color.black, 100);
        }
    }


}



