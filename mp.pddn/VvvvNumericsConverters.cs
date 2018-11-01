using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

namespace mp.pddn
{
    /// <inheritdoc />
    /// <summary>
    /// String converter for <see cref="Vector2D" />
    /// </summary>
    public class Vector2DConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string))
                return true;
            return base.CanConvertTo(context, destinationType);
        }

        /// <inheritdoc />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Vector2D v)
            {
                var res = "";
                for (int i = 0; i < 2; i++)
                {
                    res += v[i].ToString(CultureInfo.InvariantCulture) + ", ";
                }
                return res.TrimEnd().TrimEnd(',');
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string sval)
            {
                var comps = sval.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (comps.Length < 2) return Vector2D.Zero;
                var res = new Vector2D();
                for (int i = 0; i < 2; i++)
                {
                    res[i] = double.Parse(comps[i].Trim(), CultureInfo.InvariantCulture);
                }
                return res;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// String converter for <see cref="Vector3D" />
    /// </summary>
    public class Vector3DConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string))
                return true;
            return base.CanConvertTo(context, destinationType);
        }

        /// <inheritdoc />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Vector3D v)
            {
                var res = "";
                for (int i = 0; i < 3; i++)
                {
                    res += v[i].ToString(CultureInfo.InvariantCulture) + ", ";
                }
                return res.TrimEnd().TrimEnd(',');
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string sval)
            {
                var comps = sval.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (comps.Length < 3) return Vector3D.Zero;
                var res = new Vector3D();
                for (int i = 0; i < 3; i++)
                {
                    res[i] = double.Parse(comps[i].Trim(), CultureInfo.InvariantCulture);
                }
                return res;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// String converter for <see cref="Vector4D" />
    /// </summary>
    public class Vector4DConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string))
                return true;
            return base.CanConvertTo(context, destinationType);
        }

        /// <inheritdoc />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Vector4D v)
            {
                var res = "";
                for (int i = 0; i < 4; i++)
                {
                    res += v[i].ToString(CultureInfo.InvariantCulture) + ", ";
                }
                return res.TrimEnd().TrimEnd(',');
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string sval)
            {
                var comps = sval.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (comps.Length < 4) return new Vector4D(0, 0, 0, 0);
                var res = new Vector4D();
                for (int i = 0; i < 4; i++)
                {
                    res[i] = double.Parse(comps[i].Trim(), CultureInfo.InvariantCulture);
                }
                return res;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// String converter for <see cref="RGBAColor" />
    /// </summary>
    public class RGBAColorConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string))
                return true;
            return base.CanConvertTo(context, destinationType);
        }

        /// <inheritdoc />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is RGBAColor v)
            {
                return $"{v.R.ToString(CultureInfo.InvariantCulture)}, {v.G.ToString(CultureInfo.InvariantCulture)}, {v.B.ToString(CultureInfo.InvariantCulture)}, {v.A.ToString(CultureInfo.InvariantCulture)}";
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string sval)
            {
                var comps = sval.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (comps.Length < 4) return new RGBAColor(0, 0, 0, 1);
                return new RGBAColor(
                    double.Parse(comps[0].Trim(), CultureInfo.InvariantCulture),
                    double.Parse(comps[1].Trim(), CultureInfo.InvariantCulture),
                    double.Parse(comps[2].Trim(), CultureInfo.InvariantCulture),
                    double.Parse(comps[3].Trim(), CultureInfo.InvariantCulture)
                );
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// String converter for <see cref="Matrix4x4" />
    /// </summary>
    public class VMatrix4x4Converter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string))
                return true;
            return base.CanConvertTo(context, destinationType);
        }

        /// <inheritdoc />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Matrix4x4 m)
            {
                var res = "";
                for (int i = 0; i < 3; i++)
                {
                    res += m.Values[i].ToString(CultureInfo.InvariantCulture) + ", ";
                }
                return res.TrimEnd().TrimEnd(',');
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string sval)
            {
                var comps = sval.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (comps.Length < 16) return VMath.IdentityMatrix;

                var res = new Matrix4x4();
                for (int i = 0; i < 4; i++)
                {
                    res[i] = double.Parse(comps[i].Trim(), CultureInfo.InvariantCulture);
                }
                return res;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
