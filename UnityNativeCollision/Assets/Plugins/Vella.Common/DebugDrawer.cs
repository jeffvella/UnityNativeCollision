using System;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Vella.Common
{

#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// A structure capable of being drawn within <see cref="DebugDrawer"/>
    /// </summary>
    public interface IDebugDrawing
    {
        /// <summary>
        /// Draws debug information; is called by <see cref="DebugDrawer"/> from within SceneGUI context.
        /// </summary>
        void Draw();
    }

    /// <summary>
    /// Utility to allow debug drawing of 'Handles' and GUI content (such as labels)
    /// without being restricted to Monobehavior/Editor OnGUI methods.
    /// </summary>
    public static class DebugDrawer
    {
        public static Color DefaultColor = Color.white;

        private static List<IDebugDrawing> Drawings = new List<IDebugDrawing>();

#if UNITY_EDITOR

        static DebugDrawer()
        {
            SceneView.duringSceneGui += SceneViewOnDuringSceneGui;
        }

        private static void SceneViewOnDuringSceneGui(SceneView obj)
        {
            using (var scope = new Handles.DrawingScope())
            {
                foreach (var drawing in Drawings)
                {
                    drawing.Draw();
                }
            }
            CheckForFrameChange();
        }

        private static int _lastFrame;
#endif

        private static void CheckForFrameChange()
        {
#if UNITY_EDITOR
            // SceneGui and Monobehavior update ticks are out of sync
            // So redraw elements between monobehavior ticks.

            var t = Time.frameCount;
            if (_lastFrame != t)
            {
                Drawings.Clear();
                _lastFrame = t;
            }
#endif
        }



        /// <summary>
        /// Draw something custom in the scene view.
        /// </summary>
        /// <param name="drawing">instance of your IDebugDrawing implementation</param>
        [Conditional("UNITY_EDITOR")]
        public static void Draw(IDebugDrawing drawing)
        {
            CheckForFrameChange();
            Drawings.Add(drawing);
        }

        /// <summary>
        /// Draw a text label in 3D space.
        /// </summary>
        /// <param name="position">Where to draw the label in world coordinates</param>
        /// <param name="text">What the label should say</param>
        /// <param name="style">Style controlling how the label should look</param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawLabel(Vector3 position, string text, GUIStyle style = null)
        {
            Draw(new LabelDrawing
            {
                Position = position,
                Text = text,
                Style = style,
            });
        }

        /// <summary>
        /// Draw a text label in 3D space.
        /// </summary>
        /// <param name="position">Where to draw the label in world coordinates</param>
        /// <param name="text">What the label should say</param>
        /// <param name="style">Style controlling how the label should look</param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawLine(Vector3 start, Vector3 end, Color? color = null)
        {
            Debug.DrawLine(start, end, color ?? DefaultColor);
        }

        /// <summary>
        /// Draw a debug dotted line.
        /// </summary>
        /// <param name="start">start position in world space</param>
        /// <param name="end">end position in world space</param>
        /// <param name="color">color of the line</param>
        /// <param name="GapSize">The space between dots in pixels</param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawDottedLine(Vector3 start, Vector3 end, Color? color = null, float GapSize = default)
        {
            if(GapSize == default)
            {
                GapSize = Vector3.Distance(Camera.main.transform.position, start);
            }

            Draw(new DottedLineDrawing
            {
                Color = color ?? DefaultColor,
                Start = start,
                End = end,
                GapSize = GapSize,
            });
        }

        /// <summary>
        /// Draw a solid outlined rectangle in 3D space.
        /// </summary>
        /// <param name="verts">The screen coodinates rectangle.</param>
        /// <param name="faceColor">The color of the rectangle's face.</param>
        /// <param name="outlineColor">The outline color of the rectangle.</param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawSolidRectangleWithOutline(Rect rectangle, Color? faceColor = null, Color? outlineColor = null)
        {
            Vector3[] verts = new Vector3[]
            {
            new Vector3(rectangle.xMin, rectangle.yMin, 0f),
            new Vector3(rectangle.xMax, rectangle.yMin, 0f),
            new Vector3(rectangle.xMax, rectangle.yMax, 0f),
            new Vector3(rectangle.xMin, rectangle.yMax, 0f)
            };

            Draw(new RectangleWithOutlineDrawing
            {
                FaceColor = faceColor ?? DefaultColor,
                OutlineColor = outlineColor ?? DefaultColor,
                Verts = verts,
            });
        }

        /// <summary>
        /// Draw a solid outlined rectangle in 3D space.
        /// </summary>
        /// <param name="verts">The 4 vertices of the rectangle in world coordinates.</param>
        /// <param name="faceColor">The color of the rectangle's face.</param>
        /// <param name="outlineColor">The outline color of the rectangle.</param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawSolidRectangleWithOutline(Vector3[] verts, Color? faceColor = null, Color? outlineColor = null)
        {
            Draw(new RectangleWithOutlineDrawing
            {
                FaceColor = faceColor ?? DefaultColor,
                OutlineColor = outlineColor ?? DefaultColor,
                Verts = verts,
            });
        }

        /// <summary>
        /// Draw anti-aliased convex polygon specified with point array.
        /// </summary>
        /// <param name="verts">List of points describing the convex polygon</param>
        /// <param name="faceColor"></param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawAAConvexPolygon(Vector3[] verts, Color? color = null)
        {
            Draw(new PolygonDrawing
            {
                Color = color ?? DefaultColor,
                Verts = verts,
            });
        }

        /// <summary>
        /// Draw anti-aliased convex polygon specified with point array.
        /// </summary>
        /// <param name="verts">List of points describing the convex polygon</param>
        /// <param name="faceColor"></param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawSphere(Vector3 center, float radius, Color? color = null)
        {
            Draw(new SphereDrawing
            {
                Color = color ?? DefaultColor,
                Center = center,
                Radius = radius,
            });
        }

        /// <summary>
        /// Draws an arrow
        /// </summary>
        /// <param name="position">The start position of the arrow.</param>
        /// <param name="direction">The direction the arrow will point in.</param>
        /// <param name="color">The color of the arrow.</param>
        /// <param name="duration">How long to draw the arrow.</param>
        /// <param name="depthTest">Whether or not the arrow should be faded when behind other objects. </param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawArrow(Vector3 position, Vector3 direction, Color? color = null, float duration = 0, bool depthTest = true)
        {
            /// Debug Extension
            /// By Arkham Interactive
            /// Source: https://assetstore.unity.com/packages/tools/debug-drawing-extension-11396
            /// 	- Static class that extends Unity's debugging functionallity.
            /// 	- Attempts to mimic Unity's existing debugging behaviour for ease-of-use.
            /// 	- Includes gizmo drawing methods for less memory-intensive debug visualization.   

            Debug.DrawRay(position, direction, color ?? DefaultColor, duration, depthTest);
            DrawCone(position + direction, -direction * 0.333f, color ?? DefaultColor, 15, duration, depthTest);
        }

        /// <summary
        /// Draw a point as a cross/star shape made of lines.
        /// </summary>
        /// <param name="position">The point to debug.</param>
        /// <param name="color">The color of the point.</param>
        /// <param name="scale">The size of the point.</param>
        /// <param name="duration">How long to draw the point.</param>
        /// <param name="depthTest">depthTest</param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawPoint(Vector3 position, Color color = default, float scale = 1.0f, float duration = 0, bool depthTest = true)
        {
            /// Debug Extension
            /// By Arkham Interactive
            /// Source: https://assetstore.unity.com/packages/tools/debug-drawing-extension-11396
            /// 	- Static class that extends Unity's debugging functionallity.
            /// 	- Attempts to mimic Unity's existing debugging behaviour for ease-of-use.
            /// 	- Includes gizmo drawing methods for less memory-intensive debug visualization.
            color = (color != default) ? color : DefaultColor;
            Debug.DrawRay(position + (Vector3.up * (scale * 0.25f)), -Vector3.up * scale * 0.5f, color, duration, depthTest);
            Debug.DrawRay(position + (Vector3.right * (scale * 0.25f)), -Vector3.right * scale * 0.5f, color, duration, depthTest);
            Debug.DrawRay(position + (Vector3.forward * (scale * 0.25f)), -Vector3.forward * scale * 0.5f, color, duration, depthTest);
        }

        /// <summary>
        /// Draws a circle
        /// </summary>
        /// <param name="position">Where the center of the circle will be positioned.</param>
        /// <param name="up">The direction perpendicular to the surface of the circle.</param>
        /// <param name="color">The color of the circle.</param>
        /// <param name="radius">The radius of the circle.</param>
        /// <param name="duration">How long to draw the circle.</param>
        /// <param name="depthTest">Whether or not the circle should be faded when behind other objects.</param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawCircle(Vector3 position, Vector3 up, float radius = 1.0f, Color? color = null, float duration = 0, bool depthTest = true)
        {
            /// Debug Extension
            /// By Arkham Interactive
            /// Source: https://assetstore.unity.com/packages/tools/debug-drawing-extension-11396
            /// 	- Static class that extends Unity's debugging functionallity.
            /// 	- Attempts to mimic Unity's existing debugging behaviour for ease-of-use.
            /// 	- Includes gizmo drawing methods for less memory-intensive debug visualization.

            Vector3 _up = up.normalized * radius;
            Vector3 _forward = Vector3.Slerp(_up, -_up, 0.5f);
            Vector3 _right = Vector3.Cross(_up, _forward).normalized * radius;

            Matrix4x4 matrix = new Matrix4x4();

            matrix[0] = _right.x;
            matrix[1] = _right.y;
            matrix[2] = _right.z;

            matrix[4] = _up.x;
            matrix[5] = _up.y;
            matrix[6] = _up.z;

            matrix[8] = _forward.x;
            matrix[9] = _forward.y;
            matrix[10] = _forward.z;

            Vector3 _lastPoint = position + matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)));
            Vector3 _nextPoint = Vector3.zero;

            for (var i = 0; i < 91; i++)
            {
                _nextPoint.x = Mathf.Cos((i * 4) * Mathf.Deg2Rad);
                _nextPoint.z = Mathf.Sin((i * 4) * Mathf.Deg2Rad);
                _nextPoint.y = 0;

                _nextPoint = position + matrix.MultiplyPoint3x4(_nextPoint);

                Debug.DrawLine(_lastPoint, _nextPoint, color ?? DefaultColor, duration, depthTest);
                _lastPoint = _nextPoint;
            }
        }

        /// <summary>
        /// Debugs a cone.
        /// </summary>
        /// <param name="position">The position for the tip of the cone.</param>
        /// <param name="direction">The direction for the cone gets wider in.</param>
        /// <param name="color">The angle of the cone.</param>
        /// <param name="angle">The color of the cone.</param>
        /// <param name="duration">How long to draw the cone.</param>
        /// <param name="depthTest">Whether or not the cone should be faded when behind other objects.</param>
        [Conditional("UNITY_EDITOR")]
        public static void DrawCone(Vector3 position, Vector3 direction, Color color = default, float angle = 45, float duration = 0, bool depthTest = true)
        {
            /// Debug Extension
            /// By Arkham Interactive
            /// Source: https://assetstore.unity.com/packages/tools/debug-drawing-extension-11396
            /// 	- Static class that extends Unity's debugging functionallity.
            /// 	- Attempts to mimic Unity's existing debugging behaviour for ease-of-use.
            /// 	- Includes gizmo drawing methods for less memory-intensive debug visualization.

            float length = direction.magnitude;

            Vector3 _forward = direction;
            Vector3 _up = Vector3.Slerp(_forward, -_forward, 0.5f);
            Vector3 _right = Vector3.Cross(_forward, _up).normalized * length;

            direction = direction.normalized;

            Vector3 slerpedVector = Vector3.Slerp(_forward, _up, angle / 90.0f);

            float dist;
            var farPlane = new Plane(-direction, position + _forward);
            var distRay = new Ray(position, slerpedVector);

            farPlane.Raycast(distRay, out dist);

            color = color != default ? color : Color.white;
            Debug.DrawRay(position, slerpedVector.normalized * dist, color);
            Debug.DrawRay(position, Vector3.Slerp(_forward, -_up, angle / 90.0f).normalized * dist, color, duration, depthTest);
            Debug.DrawRay(position, Vector3.Slerp(_forward, _right, angle / 90.0f).normalized * dist, color, duration, depthTest);
            Debug.DrawRay(position, Vector3.Slerp(_forward, -_right, angle / 90.0f).normalized * dist, color, duration, depthTest);

            DrawCircle(position + _forward, direction, (_forward - (slerpedVector.normalized * dist)).magnitude, color, duration, depthTest);
            DrawCircle(position + (_forward * 0.5f), direction, ((_forward * 0.5f) - (slerpedVector.normalized * (dist * 0.5f))).magnitude, color, duration, depthTest);
        }

    }


    public struct SphereDrawing : IDebugDrawing
    {
        public Color Color;
        public float Radius;
        public Vector3 Center;

        public void Draw()
        {
#if UNITY_EDITOR
            Handles.color = Color;
            Handles.SphereHandleCap(0, Center, Quaternion.identity, Radius, EventType.Repaint);
#endif
        }
    }

    public struct RectangleWithOutlineDrawing : IDebugDrawing
    {
        public Color FaceColor;
        public Color OutlineColor;
        public Vector3[] Verts;

        public void Draw()
        {
#if UNITY_EDITOR
            Handles.DrawSolidRectangleWithOutline(Verts, FaceColor, OutlineColor);
#endif
        }
    }

    public struct PolygonDrawing : IDebugDrawing
    {
        public Color Color;
        public Vector3[] Verts;

        public void Draw()
        {
#if UNITY_EDITOR
            Handles.color = Color;
            Handles.DrawAAConvexPolygon(Verts);
#endif
        }
    }

    public struct DottedLineDrawing : IDebugDrawing
    {
        public Color Color;
        public Vector3 Start;
        public Vector3 End;

        /// <summary>
        /// The spacing between the dots in pixels.
        /// </summary>
        public float GapSize;

        public void Draw()
        {
#if UNITY_EDITOR
            Handles.color = Color;
            Handles.DrawDottedLine(Start,End, GapSize);
#endif
        }
    }

    public struct LabelDrawing : IDebugDrawing
    {
        public Vector3 Position;
        public string Text;
        public GUIStyle Style;

        public void Draw()
        {
            CenteredLabel(Position, Text, Style ?? SceneBoldLabelWithBackground.Value);
        }

        private static void CenteredLabel(Vector3 position, string text, GUIStyle style)
        {
#if UNITY_EDITOR
            try
            {
                GUIContent gUIContent = TempGuiContent(text, null, null);
                if (HandleUtility.WorldToGUIPointWithDepth(position).z < 0.0)
                    return;

                var size = style.CalcSize(gUIContent) / 2;
                Handles.BeginGUI();
                var screenPos = HandleUtility.WorldPointToSizedRect(position, gUIContent, style);
                screenPos.x -= size.x;
                screenPos.y -= size.y;
                GUI.Label(screenPos, gUIContent, style);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            finally
            {
                Handles.EndGUI();
            }
#endif
        }


        public static Lazy<GUIStyle> SceneBoldLabelWithBackground { get; } = new Lazy<GUIStyle>(() =>
        {
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle(EditorStyles.helpBox);
#else
            GUIStyle style = new GUIStyle();
#endif
        style.contentOffset = new Vector2(2, 2);
            style.padding = new RectOffset(2, 2, 2, 2);
            style.normal.textColor = Color.black;
            return style;
        });

        public static Lazy<GUIStyle> SceneBoldLabel { get; } = new Lazy<GUIStyle>(() =>
        {
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
#else
            GUIStyle style = new GUIStyle();
#endif
        style.contentOffset = new Vector2(-1, 2);
            style.padding = new RectOffset(2, 2, 2, 2);
            style.normal.textColor = Color.black;
            return style;
        });

        private static GUIContent _guiContent = null;
        private static GUIContent TempGuiContent(string label, string tooltip = null, Texture2D icon = null)
        {
            if (_guiContent == null)
            {
                _guiContent = new GUIContent();
            }
            _guiContent.text = label;
            _guiContent.tooltip = tooltip;
            _guiContent.image = icon;
            return _guiContent;
        }
    }
}