using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Fasterflect;
using VVVV.PluginInterfaces.V2;

namespace mp.pddn
{
    /// <summary>
    /// Ignore member by ObjectSplitNodes
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class VvvvIgnoreAttribute : Attribute { }

    /// <summary>
    /// Do not convert enumerables into spread of spreads
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class VvvvNoBinSizingAttribute : Attribute { }

    /// <summary>
    /// Base class for object member filtering classes
    /// </summary>
    public abstract class ObjectMemberFilterBase
    {
        /// <summary>
        /// If not null (which is by default) and not empty, only expose members present in this collection
        /// </summary>
        /// <remarks>
        /// If white list is also valid, black list is ignored
        /// </remarks>
        public virtual StringCollection MemberWhiteList { get; set; }

        /// <summary>
        /// If not null (which is by default) and not empty, don't expose members present in this collection
        /// </summary>
        /// <remarks>
        /// If white list is also valid, black list is ignored
        /// </remarks>
        public virtual StringCollection MemberBlackList { get; set; }

        /// <summary>
        /// Expose private members. Not recommended but can be used if needed
        /// </summary>
        public virtual bool ExposePrivate { get; set; } = false;

        public bool AllowMember(MemberInfo member)
        {
            if (!(member is FieldInfo) && !(member is PropertyInfo)) return false;
            var attributes = member.GetCustomAttributes(typeof(VvvvIgnoreAttribute), true);
            if (attributes.Any()) return false;

            switch (member)
            {
                case FieldInfo field:
                    if (field.IsStatic) return false;
                    if (field.FieldType.IsPointer) return false;
                    if (!field.FieldType.IsPublic && !ExposePrivate) return false;
                    break;
                case PropertyInfo prop:
                    if (!prop.CanRead) return false;
                    if (prop.PropertyType.IsPointer) return false;
                    if (prop.GetIndexParameters().Length > 0) return false;
                    break;
            }

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
    }

    /// <summary></summary>
    public class ObjectMemberFilter : ObjectMemberFilterBase { }

    /// <summary>
    /// Base class for abstract nodes which handle members of objects automatically
    /// </summary>
    public abstract class ObjectHandlerNodeBase : ObjectMemberFilterBase
    {
        /// <summary>
        /// Opt out automatic enumerable conversion for these types
        /// </summary>
        public virtual HashSet<Type> OptOutEnumerable { get; set; }

        protected readonly Dictionary<MemberInfo, bool> IsMemberEnumerable = new Dictionary<MemberInfo, bool>();
        protected readonly Dictionary<MemberInfo, bool> IsMemberDictionary = new Dictionary<MemberInfo, bool>();

        protected Type CType;
        protected PinDictionary Pd;

        protected T MemberAttributeHandler<T>(MemberInfo member) where T : IOAttribute
        {
            var attributes = member.GetCustomAttributes(typeof(T), true);
            T res = null;
            foreach (var attr in attributes)
            {
                if (!(attr is T oattr)) continue;
                res = oattr;
                break;
            }

            res = res ?? (typeof(T).CreateInstance(member.Name) as T);
            res.Name = member.Name;
            return res;
        }

        protected bool DoOptOutBinSizing(MemberInfo member)
        {
            var attributes = member.GetCustomAttributes(typeof(VvvvNoBinSizingAttribute), true);
            return attributes.Any();
        }

        protected bool AllowEnumBinsizing(MemberInfo member, Type memberType)
        {
            if (memberType == typeof(string)) return false;
            var allowEnumconv = !(OptOutEnumerable?.Contains(memberType) ?? false);
            if (allowEnumconv && memberType.IsConstructedGenericType)
            {
                if (OptOutEnumerable?.Contains(memberType.GetGenericTypeDefinition()) ?? false) allowEnumconv = false;
            }
            allowEnumconv = allowEnumconv && !DoOptOutBinSizing(member);
            return allowEnumconv;
        }

        protected void GetEnumerableGenerics(MemberInfo member, Type memberType, out Type potentialGenDictT, out Type potentialGenEnumT)
        {
            var interfaces = memberType.GetInterfaces().ToList();
            interfaces.Add(memberType);

            potentialGenDictT = null;
            potentialGenEnumT = null;

            foreach (var intf in interfaces)
            {
                if (!intf.IsConstructedGenericType) continue;
                if (intf.GetGenericTypeDefinition() == typeof(IDictionary<,>)) potentialGenDictT = intf;
                if (intf.GetGenericTypeDefinition() == typeof(IEnumerable<>)) potentialGenEnumT = intf;
                if (potentialGenDictT != null && potentialGenEnumT != null) break;
            }
        }
    }
}
