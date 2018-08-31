using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils.Reflection;

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

    /// <summary>
    /// Abstract node allowing you to simply create object instances
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ObjectJoinNode<T> : IPartImportsSatisfiedNotification
    {
        //[Output("Output")] public Pin<T> FOutput;

        [Import] protected IPluginHost2 FPluginHost;
        [Import] protected IIOFactory FIOFactory;

        protected bool ExposePrivate = false;
        protected T Default;

        public virtual void OnImportsSatisfiedBegin() { }
        public virtual void OnImportsSatisfiedEnd() { }

        protected abstract T CreateObject();
        
        /// <summary>
        /// If not null (which is by default) and not empty, only expose members present in this collection
        /// </summary>
        /// <remarks>
        /// If white list is also valid, black list is ignored
        /// </remarks>
        protected StringCollection MemberWhiteList;

        /// <summary>
        /// If not null (which is by default) and not empty, don't expose members present in this collection
        /// </summary>
        /// <remarks>
        /// If white list is also valid, black list is ignored
        /// </remarks>
        protected StringCollection MemberBlackList;

        private bool AllowMember(MemberInfo member)
        {
            if (MemberWhiteList != null && MemberWhiteList.Count > 0)
            {
                return MemberWhiteList.Contains(member.Name);
            }
            if (MemberBlackList != null && MemberBlackList.Count > 0)
            {
                return !MemberBlackList.Contains(member.Name);
            }
            return true;
        }

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

        protected Dictionary<PropertyInfo, bool> IsMemberEnumerable = new Dictionary<PropertyInfo, bool>();
        protected Dictionary<PropertyInfo, bool> IsMemberDictionary = new Dictionary<PropertyInfo, bool>();

        protected Type CType;
        protected PinDictionary Pd;

        private void SetDefaultValue(PropertyInfo member, object defaultValue)
        {
            var defVals = TransformDefaultToValues(defaultValue);
            var attr = new InputAttribute(member.Name)
            {
                DefaultValue = defVals[0],
                DefaultValues = defVals,
                DefaultString = defaultValue?.ToString() ?? "",
                DefaultNodeValue = defaultValue
            };
            if (defaultValue is bool b) attr.DefaultBoolean = b;

            Pd.AddInput(TransformType(member.PropertyType, member), attr);
            var spread = Pd.InputPins[member.Name].Spread;
            spread.SliceCount = 1;
            spread[0] = TransformDefaultValue(defaultValue, member);
        }

        private void AddMemberPin(PropertyInfo member)
        {
            Type memberType = typeof(object);
            if (!AllowMember(member)) return;

            if (!member.CanRead) return;
            if (member.GetIndexParameters().Length > 0) return;

            memberType = member.PropertyType;

            var enumerable = false;
            var dictionary = false;
            var defaultValue = typeof(T).GetProperty(member.Name)?.GetValue(Default);
            if (memberType.GetInterface("IDictionary") != null)
            {
                try
                {
                    var interfaces = memberType.GetInterfaces().ToList();
                    interfaces.Add(memberType);
                    var stype = interfaces
                        .Where(type =>
                        {
                            try
                            {
                                var res = type.GetGenericTypeDefinition();
                                if (res == null) return false;
                                return res == typeof(IDictionary<,>);
                            }
                            catch (Exception)
                            {
                                return false;
                            }
                        })
                        .First().GenericTypeArguments;
                    Pd.AddInput(TransformType(stype[0], member), new InputAttribute(member.Name + " Keys"), binSized: true);
                    Pd.AddInput(TransformType(stype[1], member), new InputAttribute(member.Name + " Values"), binSized: true);
                    dictionary = true;
                }
                catch (Exception)
                {
                    Pd.AddInput(TransformType(memberType, member), new InputAttribute(member.Name));
                    dictionary = false;
                }
            }
            else if (memberType.GetInterface("IEnumerable") != null && memberType != typeof(string))
            {
                try
                {
                    var interfaces = memberType.GetInterfaces().ToList();
                    interfaces.Add(memberType);
                    var stype = interfaces
                        .Where(type =>
                        {
                            try
                            {
                                var res = type.GetGenericTypeDefinition();
                                if (res == null) return false;
                                return res == typeof(IEnumerable<>);
                            }
                            catch (Exception)
                            {
                                return false;
                            }
                        })
                        .First().GenericTypeArguments[0];
                    Pd.AddInput(TransformType(stype, member), new InputAttribute(member.Name), binSized: true);
                    enumerable = true;
                }
                catch (Exception)
                {
                    Pd.AddInput(TransformType(memberType, member), new InputAttribute(member.Name));
                    SetDefaultValue(member, defaultValue);
                    enumerable = false;
                }
            }
            else
            {
                SetDefaultValue(member, defaultValue);
                enumerable = false;
            }
            IsMemberEnumerable.Add(member, enumerable);
            IsMemberDictionary.Add(member, dictionary);
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

            foreach (var prop in CType.GetProperties().Where(p => p.CanWrite))
                AddMemberPin(prop);

            OnImportsSatisfiedEnd();
        }
    }
}
