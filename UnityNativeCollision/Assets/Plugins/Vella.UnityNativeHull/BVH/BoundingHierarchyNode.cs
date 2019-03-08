// Copyright(C) David W. Jeske, 2014, and released to the public domain. 
//
// Dynamic BVH (Bounding Volume Hierarchy) using incremental refit and tree-rotations
//
// initial BVH build based on: Bounding Volume Hierarchies (BVH) – A brief tutorial on what they are and how to implement them
//              http://www.3dmuve.com/3dmblog/?p=182
//
// Dynamic Updates based on: "Fast, Effective BVH Updates for Animated Scenes" (Kopta, Ize, Spjut, Brunvand, David, Kensler)
//              http://www.cs.utah.edu/~thiago/papers/rotations.pdf
//
// see also:  Space Partitioning: Octree vs. BVH
//            http://thomasdiewald.com/blog/?p=1488
//


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Vella.Common;

namespace SimpleScene.Util.ssBVH
{



}