using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;

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
        private object[] _collection;
        private object[] _keys;
        private object _value;

        public ObjectMemberCache CacheParent { get; set; }

        public object[] Collection
        {
            get => _collection;
            set
            {
                Wrote = true;
                _collection = value;
            }
        }
        public object[] Keys
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

        public Func<object, object> Getter { get; set; }
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

            Func<object, object> getter = null;
            switch (member)
            {
                case PropertyInfo prop:
                    getter = prop.GetValue;
                    break;
                case FieldInfo field:
                    getter = field.GetValue;
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
            if (HdeHost != null) return;
            HdeHost = hde;
            HdeHost.MainLoop.OnPrepareGraph += (sender, args) => { FrameCounter++; };
            HdeHost.MainLoop.OnResetCache += (sender, args) =>
            {
                foreach (var k in Cache.Keys.ToArray())
                {
                    if (Cache[k].Used) continue;
                    Cache.Remove(k);
                }
            };
        }
    }
}
