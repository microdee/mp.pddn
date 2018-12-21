using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils.Reflection;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using Quaternion = System.Numerics.Quaternion;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace mp.pddn
{
    /// <summary>
    /// Abstract node allowing you to simply create object instances with default parameterless constructors
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ClassObjectJoinNode<T> : ObjectJoinNode<T> where T : new()
    {
        protected override T CreateObject()
        {
            return new T();
        }
    }

    public abstract class ObjectJoinNode : ObjectHandlerNodeBase
    {
        /// <summary>
        /// If an input property is IEnumerable, please specify how to clear it
        /// </summary>
        /// <param name="enumerable">extracted enumerable</param>
        /// <param name="member">Current property info</param>
        /// <param name="i">Current Slice</param>
        public virtual void ClearEnumerable(IEnumerable enumerable, PropertyInfo member, int i) { }

        /// <summary>
        /// If an input property is IEnumerable, please specify how to add objects to it
        /// </summary>
        /// <param name="enumerable">extracted enumerable</param>
        /// <param name="member">Current property info</param>
        /// <param name="obj">The object to be added to the IEnumerable</param>
        /// <param name="i">Current Slice</param>
        public virtual void AddToEnumerable(IEnumerable enumerable, object obj, PropertyInfo member, int i) { }

        /// <summary>
        /// Transform a property to a different value
        /// </summary>
        /// <param name="obj">Original value of the property</param>
        /// <param name="member">Property info</param>
        /// <param name="i">Current slice</param>
        /// <returns>The resulting transformed object</returns>
        public virtual object TransformInput(object obj, PropertyInfo member, int i)
        {
            return MiscExtensions.MapVVVVValueToSystemNumerics(obj);
        }

        /// <summary>
        /// Transform the default value of the property to a different value
        /// </summary>
        /// <param name="obj">Original value of the property</param>
        /// <param name="member">Property info</param>
        /// <param name="i">Current slice</param>
        /// <returns>The resulting transformed object</returns>
        public virtual object TransformDefaultValue(object obj, PropertyInfo member)
        {
            return MiscExtensions.MapSystemNumericsValueToVVVV(obj);
        }

        protected virtual double[] TransformDefaultToValues(object obj)
        {
            switch (obj)
            {
                case float v: return new[] { (double)v };
                case double v: return new[] { v };
                case short v: return new[] { (double)v };
                case int v: return new[] { (double)v };
                case long v: return new[] { (double)v };
                case ushort v: return new[] { (double)v };
                case uint v: return new[] { (double)v };
                case ulong v: return new[] { (double)v };
                case decimal v: return new[] { (double)v };
                case Vector2 v:
                    {
                        return new double[] { v.X, v.Y };
                    }
                case Vector3 v:
                    {
                        return new double[] { v.X, v.Y, v.Z };
                    }
                case Vector4 v:
                    {
                        return new double[] { v.X, v.Y, v.Z, v.W };
                    }
                case Quaternion v:
                    {
                        return new double[] { v.X, v.Y, v.Z, v.W };
                    }
                case Vector2D v:
                    {
                        return new double[] { v.x, v.y };
                    }
                case Vector3D v:
                    {
                        return new double[] { v.x, v.y, v.z };
                    }
                case Vector4D v:
                    {
                        return new double[] { v.x, v.y, v.z, v.w };
                    }
                default: return new[] { 0.0 };
            }
        }

        /// <summary>
        /// Transform the type a property to a different one
        /// </summary>
        /// <param name="original">Original type of the property</param>
        /// <param name="member">Property info</param>
        /// <returns>The resulting transformed type</returns>
        public virtual Type TransformType(Type original, PropertyInfo member)
        {
            return MiscExtensions.MapSystemNumericsTypeToVVVV(original);
        }

        protected readonly List<PropertyInfo> Properties = new List<PropertyInfo>();

        protected void AddPinAndSetDefaultValue(PropertyInfo member, InputAttribute iattr, object defaultValue)
        {
            var defVals = TransformDefaultToValues(defaultValue);
            iattr.Name = member.Name;
            iattr.DefaultValue = defVals[0];
            iattr.DefaultValues = defVals;
            iattr.DefaultString = defaultValue?.ToString() ?? "";
            iattr.DefaultNodeValue = defaultValue;

            if (defaultValue is bool b) iattr.DefaultBoolean = b;
            if (defaultValue is RGBAColor vcol) iattr.DefaultColor = new[] { vcol.R, vcol.G, vcol.B, vcol.A };
            if (defaultValue is Color4 s4Col) iattr.DefaultColor = new double[] { s4Col.Red, s4Col.Green, s4Col.Blue, s4Col.Alpha };
            if (defaultValue is Color3 s3Col) iattr.DefaultColor = new double[] { s3Col.Red, s3Col.Green, s3Col.Blue, 1 };

            Pd.AddInput(TransformType(member.PropertyType, member), iattr);
            var spread = Pd.InputPins[member.Name].Spread;
            spread.SliceCount = 1;
            spread[0] = TransformDefaultValue(defaultValue, member);
        }
    }

    /// <summary>
    /// Abstract node allowing you to simply create object instances. Although you have to delegate them to vvvv.
    /// </summary>
    /// <remarks>
    /// This abstract node doesn't present the object to vvvv, the implementer have to do that.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public abstract class ObjectJoinNode<T> : ObjectJoinNode, IPartImportsSatisfiedNotification
    {
        //[Output("Output")] public Pin<T> FOutput;

        [Import] protected IPluginHost2 FPluginHost;
        [Import] protected IIOFactory FIOFactory;
        
        protected T Default;

        public virtual void OnImportsSatisfiedBegin() { }
        public virtual void OnImportsSatisfiedEnd() { }

        protected abstract T CreateObject();

        private void AddMemberPin(PropertyInfo member)
        {
            if (!AllowMember(member)) return;
            if (!member.CanWrite) return;

            var memberType = member.PropertyType;
            Properties.Add(member);

            var iattr = MemberAttributeHandler<InputAttribute>(member);

            var enumerable = false;
            var dictionary = false;
            var defaultValue = typeof(T).GetProperty(member.Name)?.GetValue(Default);

            var allowEnumconv = AllowEnumBinsizing(member, memberType);

            GetEnumerableGenerics(member, memberType, out var potentialGenDictT, out var potentialGenEnumT);

            if (allowEnumconv && potentialGenDictT != null)
            {
                var stype = potentialGenDictT.GenericTypeArguments;
                iattr.Name = member.Name + " Values";
                var kattr = (InputAttribute)iattr.Clone();
                kattr.Name = member.Name + " Keys";
                Pd.AddInput(TransformType(stype[0], member), kattr, binSized: true, obj: member);
                Pd.AddInput(TransformType(stype[1], member), iattr, binSized: true, obj: member);
                dictionary = true;
            }
            else if (allowEnumconv && potentialGenEnumT != null)
            {
                var stype = potentialGenEnumT.GenericTypeArguments[0];
                Pd.AddInput(TransformType(stype, member), iattr, binSized: true, obj: member);
                enumerable = true;
            }
            else
            {
                AddPinAndSetDefaultValue(member, iattr, defaultValue);
                enumerable = false;
            }
            IsMemberEnumerable.Add(member, enumerable);
            IsMemberDictionary.Add(member, dictionary);
        }

        protected void FillObject(T obj, int i)
        {
            foreach (var prop in Properties)
            {
                FillObject(prop, obj, i);
            }
        }

        protected void FillObject(PropertyInfo member, T obj, int i)
        {
            if (IsMemberDictionary[member])
            {
                if (!(member.GetValue(obj) is IDictionary dict))
                {
                    return;
                }
                var keyspread = (ISpread)Pd.InputPins[member.Name + " Keys"].Spread[i];
                var valuespread = (ISpread)Pd.InputPins[member.Name + " Values"].Spread[i];
                dict.Clear();
                for (int j = 0; j < keyspread.SliceCount; j++)
                {
                    dict.Add(keyspread[j], valuespread[j]);
                }
            }
            else if (IsMemberEnumerable[member])
            {
                if (!(member.GetValue(obj) is IEnumerable enumerable))
                {
                    return;
                }
                var spread = (ISpread)Pd.InputPins[member.Name].Spread[i];
                ClearEnumerable(enumerable, member, i);
                for (int j = 0; j < spread.SliceCount; j++)
                {
                    AddToEnumerable(enumerable, spread[j], member, i);
                }
            }
            else
            {
                member.SetValue(obj, TransformInput(Pd.InputPins[member.Name].Spread[i], member, i));
            }
        }

        public void OnImportsSatisfied()
        {
            Default = CreateObject();
            Pd = new PinDictionary(FIOFactory);
            CType = typeof(T);

            OnImportsSatisfiedBegin();

            Properties.Clear();
            foreach (var prop in CType.GetProperties().Where(p => p.CanWrite))
                AddMemberPin(prop);

            OnImportsSatisfiedEnd();
        }
    }
}
