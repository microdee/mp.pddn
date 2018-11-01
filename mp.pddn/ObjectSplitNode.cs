using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Fasterflect;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils.Reflection;

namespace mp.pddn
{
    public class FrameCacheState
    {
        public long FrameCounter { get; private set; }
        public bool Used => FrameCounter >= ObjectSplitCache.FrameCounter - 2;

        public bool Wrote
        {
            get => FrameCounter == ObjectSplitCache.FrameCounter;
            set
            {
                if (value)
                    FrameCounter = ObjectSplitCache.FrameCounter;
            }
        }
    }
    public class MemberValueCache : FrameCacheState
    {
        private ISpread _collection;
        private ISpread _keys;
        private object _value;

        public ObjectMemberCache CacheParent { get; set; }

        public ISpread Collection
        {
            get => _collection;
            set
            {
                Wrote = true;
                _collection = value;
            }
        }
        public ISpread Keys
        {
            get => _keys;
            set
            {
                Wrote = true;
                _keys = value;
            }
        }
        public object Value
        {
            get => _value;
            set
            {
                Wrote = true;
                _value = value;
            }
        }

        public MemberGetter Getter { get; set; }
        public MemberInfo Info { get; set; }
        
    }
    public class ObjectMemberCache : FrameCacheState
    {
        public Dictionary<string, MemberValueCache> MemberValues { get; } = new Dictionary<string, MemberValueCache>();

        private object _associatedObject;
        public object AssociatedObject
        {
            get => _associatedObject;
            set
            {
                Wrote = true;
                _associatedObject = value;
            }
        }

        private Type _associatedType;
        public Type AssociatedType
        {
            get => _associatedType;
            set
            {
                Wrote = true;
                _associatedType = value;
            }
        }

        public MemberValueCache AddMember(MemberInfo member)
        {
            Wrote = true;
            if (MemberValues.ContainsKey(member.Name)) return MemberValues[member.Name];

            MemberGetter getter = null;
            switch (member)
            {
                case PropertyInfo prop:
                    getter = AssociatedType.DelegateForGetPropertyValue(prop.Name);
                    break;
                case FieldInfo field:
                    getter = AssociatedType.DelegateForGetFieldValue(field.Name);
                    break;
                default:
                    return null;
            }
            var res = new MemberValueCache
            {
                CacheParent = this,
                Getter = getter,
                Info = member,
                Wrote = true
            };
            MemberValues.Add(member.Name, res);

            return res;
        }

        public ObjectMemberCache(object target)
        {
            AssociatedObject = target;
            AssociatedType = target.GetType();
        }
    }
    public static class ObjectSplitCache
    {
        public static IHDEHost HdeHost { get; set; }
        public static Dictionary<object, ObjectMemberCache> Cache { get; } = new Dictionary<object, ObjectMemberCache>();

        public static long FrameCounter = 0;

        public static void Initialize(IHDEHost hde)
        {
            if(HdeHost != null) return;
            HdeHost = hde;
            HdeHost.MainLoop.OnPrepareGraph += (sender, args) => { FrameCounter++; };
            HdeHost.MainLoop.OnResetCache += (sender, args) =>
            {
                foreach (var k in Cache.Keys.ToArray())
                {
                    if(Cache[k].Used) continue;
                    Cache.Remove(k);
                }
            };
        }
    }

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

    public class ObjectMemberFilter : ObjectMemberFilterBase { }

    /// <summary>
    /// Non-Generic base of ObjectSplitNode
    /// </summary>
    public abstract class ObjectSplitNode : ObjectMemberFilterBase
    {
        [Output("Top Level Type", Visibility = PinVisibility.OnlyInspector)]
        public ISpread<string> FTypeName;
        [Output("Valid", Visibility = PinVisibility.OnlyInspector, Order = 9000)]
        public ISpread<bool> FValid;

        [Input("Nil on Null", Order = 9999, Visibility = PinVisibility.OnlyInspector)]
        public IDiffSpread<bool> FNullMode;

        [Import] protected IPluginHost2 FPluginHost;
        [Import] protected IIOFactory FIOFactory;
        [Import] protected IHDEHost HdeHost;

        /// <summary>
        /// By default object values are cached so properties and enumerables are only executed once per frame. There are situations though when caching makes things worse.
        /// </summary>
        public virtual bool UseObjectCache { get; set; } = true;

        public virtual bool ManualInit { get; set; } = false;

        /// <summary>
        /// Expose attributes of listed types for specific members. Null turns off this feature, default is null
        /// </summary>
        public virtual Dictionary<string, HashSet<Type>> ExposeMemberAttributes { get; set; }

        /// <summary>
        /// Expose attributes of listed types for all members. Null turns off this feature, default is null
        /// </summary>
        public virtual HashSet<Type> ExposeAttributes { get; set; }

        /// <summary>
        /// Opt out automatic enumerable conversion for these types
        /// </summary>
        public virtual HashSet<Type> OptOutEnumerable { get; set; }

        public virtual bool StopWatchToSeconds { get; set; } = true;

        public virtual void OnImportsSatisfiedBegin() { }
        public virtual void OnImportsSatisfiedEnd() { }

        public virtual void OnEvaluateBegin() { }
        public virtual void OnEvaluateEnd() { }

        public virtual void OnChangedBegin() { }
        public virtual void OnChangedEnd() { }

        public abstract void Initialize();


        /// <summary>
        /// Transform a field or a property to a different value
        /// </summary>
        /// <param name="obj">Original value of the field / property</param>
        /// <param name="member">Field / Property info</param>
        /// <param name="i">Current slice</param>
        /// <param name="stopwatchtoseconds"></param>
        /// <returns>The resulting transformed object</returns>
        public virtual object TransformOutput(object obj, MemberInfo member, int i)
        {
            return MiscExtensions.MapSystemNumericsValueToVVVV(obj, StopWatchToSeconds);
        }

        /// <summary>
        /// Transform the type of a field or a property to a different one
        /// </summary>
        /// <param name="original">Original type of the field / property</param>
        /// <param name="member">Field / Property info</param>
        /// <param name="stopwatchtoseconds"></param>
        /// <returns>The resulting transformed type</returns>
        public virtual Type TransformType(Type original, MemberInfo member)
        {
            return MiscExtensions.MapSystemNumericsTypeToVVVV(original, StopWatchToSeconds);
        }

        protected Dictionary<MemberInfo, bool> IsMemberEnumerable = new Dictionary<MemberInfo, bool>();
        protected Dictionary<MemberInfo, bool> IsMemberDictionary = new Dictionary<MemberInfo, bool>();

        protected Type CType;
        protected PinDictionary Pd;
        protected string NodePath;

        protected void AddMemberAttributePin(MemberInfo member)
        {
            if (ExposeAttributes != null)
                AddMemberAttributePin(member, ExposeAttributes);
            if (ExposeMemberAttributes != null && ExposeMemberAttributes.ContainsKey(member.Name))
                AddMemberAttributePin(member, ExposeMemberAttributes[member.Name]);
        }

        protected void AddMemberAttributePin(MemberInfo member, IEnumerable<Type> desiredattrtypes)
        {
            foreach (var attrtype in desiredattrtypes)
            {
                Attribute[] validattrs;
                try
                {
                    validattrs = member.GetCustomAttributes(attrtype, true).Cast<Attribute>().ToArray();
                }
                catch (Exception e)
                {
                    continue;
                }
                if (validattrs.Length == 0) continue;
                var pin = Pd.AddOutput(TransformType(attrtype, member), new OutputAttribute(member.Name + " " + attrtype.GetCSharpName())
                {
                    Visibility = PinVisibility.OnlyInspector
                });
                pin.Spread.SliceCount = validattrs.Length;
                for (int i = 0; i < validattrs.Length; i++)
                {
                    pin[i] = TransformOutput(validattrs[i], member, i);
                }
            }
        }

        protected void AddMemberPin(MemberInfo member)
        {
            if (!AllowMember(member)) return;

            Type memberType = typeof(object);
            switch (member)
            {
                case FieldInfo field:
                    memberType = field.FieldType;
                    break;
                case PropertyInfo prop:
                    memberType = prop.PropertyType;
                    break;
            }
            var enumerable = false;
            var dictionary = false;

            var allowEnumconv = !(OptOutEnumerable?.Contains(memberType) ?? false);
            if (allowEnumconv && memberType.IsConstructedGenericType)
            {
                if (OptOutEnumerable?.Contains(memberType.GetGenericTypeDefinition()) ?? false) allowEnumconv = false;
            }

            if (allowEnumconv && memberType.GetInterface("IDictionary") != null)
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
                    Pd.AddOutput(TransformType(stype[0], member), new OutputAttribute(member.Name + " Keys"), binSized: true);
                    Pd.AddOutput(TransformType(stype[1], member), new OutputAttribute(member.Name + " Values"), binSized: true);
                    dictionary = true;
                }
                catch (Exception)
                {
                    Pd.AddOutput(TransformType(memberType, member), new OutputAttribute(member.Name));
                    dictionary = false;
                }
            }
            else if (allowEnumconv && memberType.GetInterface("IEnumerable") != null && memberType != typeof(string))
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
                    Pd.AddOutput(TransformType(stype, member), new OutputAttribute(member.Name), binSized: true);
                    enumerable = true;
                }
                catch (Exception)
                {
                    Pd.AddOutput(TransformType(memberType, member), new OutputAttribute(member.Name));
                    enumerable = false;
                }
            }
            else
            {
                Pd.AddOutput(TransformType(memberType, member), new OutputAttribute(member.Name));
                enumerable = false;
            }
            AddMemberAttributePin(member);
            IsMemberEnumerable.Add(member, enumerable);
            IsMemberDictionary.Add(member, dictionary);
        }

        protected ObjectMemberCache InitializeObjectCache(object input)
        {
            var res = new ObjectMemberCache(input);
            ObjectSplitCache.Cache.Add(input, res);
            return res;
        }
        protected MemberValueCache InitializeCachedValue(MemberInfo member, ObjectMemberCache memberCache)
        {
            var res = memberCache.AddMember(member);
            return res;
        }

        protected object GetMemberValue(MemberInfo member, object target)
        {
            switch (member)
            {
                case PropertyInfo prop:
                    return prop.GetValue(target);
                case FieldInfo field:
                    return field.GetValue(target);
                default:
                    return null;
            }
        }

        protected void AssignMemberValue(MemberInfo member, object input, int i, MemberValueCache valueCache)
        {
            object memberValue = valueCache != null ? valueCache.Getter(input) : GetMemberValue(member, input);

            if (IsMemberDictionary[member])
            {
                var dict = (IDictionary)memberValue;
                var keyspread = (ISpread)Pd.OutputPins[member.Name + " Keys"][i];
                var valuespread = (ISpread)Pd.OutputPins[member.Name + " Values"][i];

                keyspread.SliceCount = valuespread.SliceCount = 0;
                foreach (var k in dict.Keys)
                {
                    keyspread.SliceCount++;
                    keyspread[-1] = TransformOutput(k, member, i);
                }
                foreach (var v in dict.Values)
                {
                    valuespread.SliceCount++;
                    valuespread[-1] = TransformOutput(v, member, i);
                }
                if (valueCache != null)
                {
                    valueCache.Collection = valuespread;
                    valueCache.Keys = keyspread;
                }
            }
            else if (IsMemberEnumerable[member])
            {
                var enumerable = (IEnumerable)memberValue;
                var spread = (ISpread)Pd.OutputPins[member.Name][i];
                spread.SliceCount = 0;
                foreach (var o in enumerable)
                {
                    spread.SliceCount++;
                    spread[-1] = TransformOutput(o, member, i);
                }

                if (valueCache != null)
                    valueCache.Collection = spread;
            }
            else
            {
                var val = TransformOutput(memberValue, member, i);
                Pd.OutputPins[member.Name][i] = val;

                if (valueCache != null)
                    valueCache.Value = val;
            }
        }

        protected void ReadCachedMemberValue(MemberInfo member, int i, MemberValueCache valueCache)
        {
            if (IsMemberDictionary[member])
            {
                Pd.OutputPins[member.Name + " Keys"][i] = valueCache.Keys;
                Pd.OutputPins[member.Name + " Values"][i] = valueCache.Collection;
            }
            else if (IsMemberEnumerable[member])
            {
                Pd.OutputPins[member.Name][i] = valueCache.Collection;
            }
            else
            {
                Pd.OutputPins[member.Name][i] = valueCache.Value;
            }
        }
    }

    /// <summary>
    /// Generic version of expand node. Hence much quicker and output data can be transformed
    /// </summary>
    /// <typeparam name="TObj">Type of desired object</typeparam>
    public abstract class ObjectSplitNode<TObj> : ObjectSplitNode, IPartImportsSatisfiedNotification, IPluginEvaluate
    {
        [Input("Input")] public Pin<TObj> FInput;

        public virtual bool IsChanged()
        {
            return FInput.IsChanged;
        }

        public void OnImportsSatisfied()
        {
            if(!ManualInit) Initialize();
        }

        public override void Initialize()
        {
            NodePath = FPluginHost.GetNodePath(false);
            if (UseObjectCache)
                ObjectSplitCache.Initialize(HdeHost);
            Pd = new PinDictionary(FIOFactory);
            CType = typeof(TObj);

            OnImportsSatisfiedBegin();

            if (CType.IsInterface)
            {
                IEnumerable<PropertyInfo> props = CType.GetProperties();
                foreach (var iif in CType.GetInterfaces())
                {
                    props = props.Concat(iif.GetProperties());
                }
                foreach (var prop in props)
                    AddMemberPin(prop);
            }
            else
            {
                foreach (var field in CType.GetFields())
                    AddMemberPin(field);
                foreach (var prop in CType.GetProperties())
                    AddMemberPin(prop);
            }

            OnImportsSatisfiedEnd();
        }

        public void Evaluate(int SpreadMax)
        {
            OnEvaluateBegin();
            if (FInput.SliceCount == 0)
            {
                foreach (var outpin in Pd.OutputPins.Values)
                {
                    outpin.Spread.SliceCount = 0;
                }
                OnEvaluateEnd();
                return;
            }

            var sprmax = FInput.SliceCount;
            if (sprmax > 0 && FNullMode[0] && FInput[0] == null)
            {
                sprmax = 0;
            }
            if (IsChanged() || FNullMode.IsChanged)
            {
                FTypeName.SliceCount = FValid.SliceCount = sprmax;
                OnChangedBegin();
                foreach (var pin in Pd.OutputPins.Values)
                {
                    pin.Spread.SliceCount = sprmax;
                }
                for (int i = 0; i < sprmax; i++)
                {
                    var obj = FInput[i];
                    FTypeName[i] = obj?.GetType().AssemblyQualifiedName ?? "";
                    if (obj == null)
                    {
                        FValid[i] = false;
                        continue;
                    }
                    FValid[i] = true;
                    if (UseObjectCache && ObjectSplitCache.Cache.ContainsKey(obj))
                    {
                        var objCache = ObjectSplitCache.Cache[obj];
                        foreach (var member in IsMemberEnumerable.Keys)
                        {
                            if (objCache.MemberValues.ContainsKey(member.Name))
                            {
                                var memberCache = objCache.MemberValues[member.Name];
                                if (memberCache.Wrote)
                                {
                                    ReadCachedMemberValue(member, i, memberCache);
                                }
                                else
                                {
                                    AssignMemberValue(member, obj, i, memberCache);
                                    memberCache.Wrote = true;
                                    objCache.Wrote = true;
                                }
                            }
                            else
                            {
                                var memberCache = objCache.AddMember(member);
                                AssignMemberValue(member, obj, i, memberCache);
                            }
                        }
                    }
                    else if(UseObjectCache)
                    {
                        var objCache = InitializeObjectCache(obj);
                        foreach (var member in IsMemberEnumerable.Keys)
                        {
                            var memberCache = InitializeCachedValue(member, objCache);
                            AssignMemberValue(member, obj, i, memberCache);
                            memberCache.Wrote = true;
                        }
                        objCache.Wrote = true;
                    }
                    else
                    {
                        foreach (var member in IsMemberEnumerable.Keys)
                            AssignMemberValue(member, obj, i, null);
                    }
                }
                OnChangedEnd();
            }
            OnEvaluateEnd();
        }
    }
}
