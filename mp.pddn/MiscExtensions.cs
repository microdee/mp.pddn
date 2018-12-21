using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Mathematics;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils.VMath;
using VMatrix = VVVV.Utils.VMath.Matrix4x4;
using SMatrix = System.Numerics.Matrix4x4;

namespace mp.pddn
{
    public static class MiscExtensions
    {
        public static Type MapSystemNumericsTypeToVVVV(Type original, bool stopwatchToSeconds = true)
        {
            if (original == typeof(Vector2))
            {
                return typeof(Vector2D);
            }
            if (original == typeof(Vector3))
            {
                return typeof(Vector3D);
            }
            if (original == typeof(Vector4))
            {
                return typeof(Vector4D);
            }
            if (original == typeof(Quaternion))
            {
                return typeof(Vector4D);
            }
            if (original == typeof(SMatrix))
            {
                return typeof(VMatrix);
            }
            if (original == typeof(Stopwatch) && stopwatchToSeconds)
            {
                return typeof(double);
            }
            if (original == typeof(TimeSpan))
            {
                return typeof(double);
            }
            return original;
        }

        public static object MapSystemNumericsValueToVVVV(object obj, bool stopwatchToSeconds = true)
        {
            switch (obj)
            {
                case Vector2 v:
                {
                    return v.AsVVector();
                }
                case Vector3 v:
                {
                    return v.AsVVector();
                }
                case Vector4 v:
                {
                    return v.AsVVector();
                }
                case Quaternion v:
                {
                    return v.AsVVector();
                }
                case SMatrix v:
                {
                    return v.AsVMatrix4X4();
                }
                case TimeSpan ts:
                {
                    return ts.TotalSeconds;
                }
                case Stopwatch s:
                {
                    if (stopwatchToSeconds) return s.Elapsed.TotalSeconds;
                    else return s;
                }
                default:
                {
                    return obj;
                }
            }
        }

        public static Type MapVVVVTypeToSystemNumerics(Type original)
        {
            if (original == typeof(Vector2D))
            {
                return typeof(Vector2);
            }
            if (original == typeof(Vector3D))
            {
                return typeof(Vector3);
            }
            if (original == typeof(Vector4D))
            {
                return typeof(Vector4);
            }
            if (original == typeof(VMatrix))
            {
                return typeof(SMatrix);
            }
            return original;
        }

        public static object MapVVVVValueToSystemNumerics(object obj)
        {
            switch (obj)
            {
                case Vector2D v:
                {
                    return v.AsSystemVector();
                }
                case Vector3D v:
                {
                    return v.AsSystemVector();
                }
                case Vector4D v:
                {
                    return v.AsSystemVector();
                }
                case VMatrix v:
                {
                    return v.AsSystemMatrix4X4();
                }
                default:
                {
                    return obj;
                }
            }
        }

        public static object[] ToObjectArray(this ISpread spread, object[] buffer = null)
        {
            if(buffer == null)
            {
                var res = new object[spread.SliceCount];
                for (int i = 0; i < spread.SliceCount; i++)
                {
                    res[i] = spread[i];
                }
                return res;
            }
            else
            {
                if (buffer.Length != spread.SliceCount)
                {
                    var res = new object[spread.SliceCount];
                    for (int i = 0; i < spread.SliceCount; i++)
                    {
                        res[i] = spread[i];
                    }
                    return res;
                }
                else
                {
                    for (int i = 0; i < spread.SliceCount; i++)
                    {
                        buffer[i] = spread[i];
                    }
                    return buffer;
                }
            }
        }
    }
}
