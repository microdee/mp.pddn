using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;

namespace mp.pddn
{
    public class MemberValueCache
    {
        public ISpread Collection;
        public ISpread Keys;
        public object Value;
    }
    public class ObjectMemberCache
    {
        public Dictionary<MemberInfo, MemberValueCache> MemberValues { get; } = new Dictionary<MemberInfo, MemberValueCache>();
        public bool Used { get; set; }
        public bool Wrote { get; set; }
    }
    public static class ObjectSplitCache
    {
        public static IHDEHost HdeHost { get; set; }
        public static Dictionary<object, ObjectMemberCache> Cache { get; } = new Dictionary<object, ObjectMemberCache>();

        public static void Initialize(IHDEHost hde)
        {
            if(HdeHost != null) return;
            HdeHost = hde;
            HdeHost.MainLoop.OnPrepareGraph += (sender, args) =>
            {
                foreach (var memcache in Cache.Values)
                {
                    memcache.Used = false;
                    memcache.Wrote = false;
                }
            };
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
    /// <summary>
    /// Generic version of expand node. Hence much quicker and output data can be transformed
    /// </summary>
    /// <typeparam name="T">Type of desired object</typeparam>
    public abstract class ObjectSplitNode<T> : IPartImportsSatisfiedNotification, IPluginEvaluate
    {
        [Input("Input")] public Pin<T> FInput;
        [Import] protected IPluginHost2 FPluginHost;
        [Import] protected IIOFactory FIOFactory;
        [Import] protected IHDEHost HdeHost;

        protected bool ExposePrivate = false;

        public virtual void OnImportsSatisfiedBegin() { }
        public virtual void OnImportsSatisfiedEnd() { }

        public virtual void OnEvaluateBegin() { }
        public virtual void OnEvaluateEnd() { }

        public virtual void OnChangedBegin() { }
        public virtual void OnChangedEnd() { }

        /// <summary>
        /// Transform a field or a property to a different value
        /// </summary>
        /// <param name="obj">Original value of the field / property</param>
        /// <param name="member">Field / Property info</param>
        /// <param name="i">Current slice</param>
        /// <returns>The resulting transformed object</returns>
        public virtual object TransformOutput(object obj, MemberInfo member, int i)
        {
            return obj;
        }

        /// <summary>
        /// Transform the type of a field or a property to a different one
        /// </summary>
        /// <param name="original">Original type of the field / property</param>
        /// <param name="member">Field / Property info</param>
        /// <returns>The resulting transformed type</returns>
        public virtual Type TransformType(Type original, MemberInfo member)
        {
            return original;
        }

        protected Dictionary<MemberInfo, bool> IsMemberEnumerable = new Dictionary<MemberInfo, bool>();
        protected Dictionary<MemberInfo, bool> IsMemberDictionary = new Dictionary<MemberInfo, bool>();

        protected Type CType;
        protected PinDictionary Pd;

        private void AddMemberPin(MemberInfo member)
        {
            if (!(member is FieldInfo) && !(member is PropertyInfo)) return;
            Type memberType = typeof(object);
            switch (member)
            {
                case FieldInfo field:
                    if (field.IsStatic) return;
                    if (field.FieldType.IsPointer) return;
                    if (!field.FieldType.IsPublic && !ExposePrivate) return;

                    memberType = field.FieldType;
                    break;
                case PropertyInfo prop:
                    if (!prop.CanRead) return;
                    if (prop.GetIndexParameters().Length > 0) return;

                    memberType = prop.PropertyType;
                    break;
            }
            var enumerable = false;
            var dictionary = false;

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
            else if ((memberType.GetInterface("IEnumerable") != null) && (memberType != typeof(string)))
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
            IsMemberEnumerable.Add(member, enumerable);
            IsMemberDictionary.Add(member, dictionary);
        }

        private ObjectMemberCache InitializeObjectCache(object input)
        {
            var res = new ObjectMemberCache();
            ObjectSplitCache.Cache.Add(input, res);
            return res;
        }
        private MemberValueCache InitializeCachedValue(MemberInfo member, ObjectMemberCache memberCache)
        {
            var res = new MemberValueCache();
            memberCache.MemberValues.Add(member, res);
            return res;
        }

        private void AssignMemberValue(MemberInfo member, object input, int i, MemberValueCache valueCache)
        {
            object memberValue = null;
            switch (member)
            {
                case FieldInfo field:
                    memberValue = field.GetValue(input);
                    break;
                case PropertyInfo prop:
                    memberValue = prop.GetValue(input);
                    break;
                default: return;
            }

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

                valueCache.Collection = valuespread;
                valueCache.Keys = keyspread;
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

                valueCache.Collection = spread;
            }
            else
            {
                valueCache.Value = TransformOutput(memberValue, member, i);
                Pd.OutputPins[member.Name][i] = valueCache.Value;
            }
        }

        private void ReadCachedMemberValue(MemberInfo member, int i, MemberValueCache valueCache)
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

        public void OnImportsSatisfied()
        {
            ObjectSplitCache.Initialize(HdeHost);
            Pd = new PinDictionary(FIOFactory);
            CType = typeof(T);

            OnImportsSatisfiedBegin();

            foreach (var field in CType.GetFields())
                AddMemberPin(field);
            foreach (var prop in CType.GetProperties())
                AddMemberPin(prop);

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

            if (FInput[0] == null)
            {
                OnEvaluateEnd();
                return;
            }
            var sprmax = FInput.SliceCount;
            foreach (var input in FInput)
            {
                if (ObjectSplitCache.Cache.ContainsKey(input))
                    ObjectSplitCache.Cache[input].Used = true;
            }
            if (FInput.IsChanged)
            {
                OnChangedBegin();
                foreach (var pin in Pd.OutputPins.Values)
                {
                    pin.Spread.SliceCount = sprmax;
                }
                for (int i = 0; i < sprmax; i++)
                {
                    var obj = FInput[i];
                    if (obj == null) continue;
                    if (ObjectSplitCache.Cache.ContainsKey(obj))
                    {
                        var objCache = ObjectSplitCache.Cache[obj];
                        foreach (var member in IsMemberEnumerable.Keys)
                        {
                            if(objCache.Wrote)
                                ReadCachedMemberValue(member, i, objCache.MemberValues[member]);
                            else
                                AssignMemberValue(member, obj, i, objCache.MemberValues[member]);
                        }
                        objCache.Used = true;
                        objCache.Wrote = true;
                    }
                    else
                    {
                        var objCache = InitializeObjectCache(obj);
                        foreach (var member in IsMemberEnumerable.Keys)
                        {
                            var memberCache = InitializeCachedValue(member, objCache);
                            AssignMemberValue(member, obj, i, memberCache);
                        }
                        objCache.Used = true;
                        objCache.Wrote = true;
                    }
                }
                OnChangedEnd();
            }
            OnEvaluateEnd();
        }
    }
}
