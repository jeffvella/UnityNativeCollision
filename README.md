# UnityNativeCollision #

This project is an experimental C# implementation of SAT collision and polygon intersection for the Unity game engine. It's specifically designed to be compatable with Unity's Burst Compiler for performance. 

##### Features:

* Generation of native half-edge mesh from Unity Meshes.
* Fast convex polygon face/edge boolean collision detection.
* Polygon/Polygon intersection manifold generation (via Sutherland-Hodgman clipping)
* Burst compiled jobs for single and batch collision operations.

<img src="https://i.imgur.com/2r6IAtB.gif" target="_blank" />

Note: Project was created with Unity 2019.2, older versions may not work.


##### Acknowledgments:
This work is largely derived from BounceLite by Irlan Robson (zLib License): 
https://github.com/irlanrobson/bounce_lite 

The SAT implementation is based on the 2013 GDC presentation by Dirk Gregorius and his forum posts about Valve's Rubikon physics engine:
 * https://www.gdcvault.com/play/1017646/Physics-for-Game-Programmers-The
 * https://www.gamedev.net/forums/topic/692141-collision-detection-why-gjk/?do=findComment&comment=5356490 
 * http://www.gamedev.net/topic/667499-3d-sat-problem/ 
