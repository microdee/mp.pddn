using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils.Reflection;

namespace mp.pddn
{
    /// <summary>
    /// Non-Generic base of ObjectSplitNode
    /// </summary>
    public abstract class ObjectSplitNode : ObjectHandlerNodeBase
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

        public virtual void OnImportsSatisfiedBegin() { }
        public virtual void OnImportsSatisfiedEnd() { }

        public virtual void OnEvaluateBegin() { }
        public virtual void OnEvaluateEnd() { }

        public virtual void OnChangedBegin() { }
        public virtual void OnChangedEnd() { }

        public abstract void Initialize();
        protected string NodePath;

        public virtual bool StopWatchToSeconds { get; set; } = true;

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
        /// <returns>The resulting transformed type</returns>
        public virtual Type TransformType(Type original, MemberInfo member)
        {
            return MiscExtensions.MapSystemNumericsTypeToVVVV(original, StopWatchToSeconds);
        }

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
            var oattr = MemberAttributeHandler<OutputAttribute>(member);

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
            var allowEnumconv = AllowEnumBinsizing(member, memberType);

            GetEnumerableGenerics(member, memberType, out var potentialGenDictT, out var potentialGenEnumT);

            if (allowEnumconv && potentialGenDictT != null)
            {
                var stype = potentialGenDictT.GenericTypeArguments;
                oattr.Name = member.Name + " Values";
                oattr.BinVisibility = oattr.Visibility == PinVisibility.OnlyInspector
                    ? PinVisibility.OnlyInspector
                    : PinVisibility.Hidden;

                var kattr = (OutputAttribute) oattr.Clone();
                kattr.Name = member.Name + " Keys";
                kattr.BinVisibility = PinVisibility.OnlyInspector;
                Pd.AddOutput(TransformType(stype[0], member), kattr, binSized: true);
                Pd.AddOutput(TransformType(stype[1], member), oattr, binSized: true);
                dictionary = true;
            }
            else if (allowEnumconv && potentialGenEnumT != null)
            {
                var stype = potentialGenEnumT.GenericTypeArguments[0];
                oattr.BinVisibility = oattr.Visibility == PinVisibility.OnlyInspector
                    ? PinVisibility.OnlyInspector
                    : PinVisibility.Hidden;
                Pd.AddOutput(TransformType(stype, member), oattr, binSized: true);
                enumerable = true;
            }
            else
            {
                Pd.AddOutput(TransformType(memberType, member), oattr);
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
            var memberValue = valueCache?.Getter(input) ?? GetMemberValue(member, input);

            if (IsMemberDictionary[member])
            {
                var keyspread = (ISpread)Pd.OutputPins[member.Name + " Keys"][i];
                var valuespread = (ISpread)Pd.OutputPins[member.Name + " Values"][i];
                keyspread.SliceCount = valuespread.SliceCount = 0;

                if (memberValue == null) return;
                var dict = (IDictionary)memberValue;

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
                    valueCache.Collection = valuespread.ToObjectArray(valueCache.Collection);
                    valueCache.Keys = keyspread.ToObjectArray(valueCache.Keys);
                }
            }
            else if (IsMemberEnumerable[member])
            {
                var spread = (ISpread)Pd.OutputPins[member.Name][i];
                spread.SliceCount = 0;

                if(memberValue == null) return;
                var enumerable = (IEnumerable)memberValue;

                foreach (var o in enumerable)
                {
                    spread.SliceCount++;
                    spread[-1] = TransformOutput(o, member, i);
                }

                if (valueCache != null)
                    valueCache.Collection = spread.ToObjectArray(valueCache.Collection);
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
                var keyspread = (ISpread)Pd.OutputPins[member.Name + " Keys"][i];
                keyspread.SliceCount = valueCache.Keys.Length;
                for (int j = 0; j < valueCache.Keys.Length; j++)
                {
                    keyspread[j] = valueCache.Keys[j];
                }
                var valspread = (ISpread)Pd.OutputPins[member.Name + " Values"][i];
                valspread.SliceCount = valueCache.Collection.Length;
                for (int j = 0; j < valueCache.Collection.Length; j++)
                {
                    valspread[j] = valueCache.Collection[j];
                }
            }
            else if (IsMemberEnumerable[member])
            {
                var valspread = (ISpread)Pd.OutputPins[member.Name][i];
                valspread.SliceCount = valueCache.Collection.Length;
                for (int j = 0; j < valueCache.Collection.Length; j++)
                {
                    valspread[j] = valueCache.Collection[j];
                }
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
                IsChanged();
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
