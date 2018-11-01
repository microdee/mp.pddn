using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Coding;
using VVVV.Hosting.IO;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.Reflection;

namespace mp.pddn
{
    public struct TypeSplitter
    {
        public readonly PluginInfo Info;
        public readonly Type BaseType;
        public readonly Type SplitterType;
        public readonly string BaseName;

        public TypeSplitter(string name, Type b, Type splitter, PluginInfo info = null)
        {
            Info = info;
            BaseType = b;
            SplitterType = splitter;
            BaseName = name;
        }
    }

    public struct NodeInfoExtension
    {
        public readonly TypeSplitter Splitter;
        public readonly string MemberName;

        public NodeInfoExtension(TypeSplitter splitter, string memberName)
        {
            Splitter = splitter;
            MemberName = memberName;
        }
    }

    public abstract class ObjectMemberNodeFactory : NodeFactoryBase
    {
        protected ObjectMemberNodeFactory(CompositionContainer parentContainer) : base(parentContainer) { }

        protected ObjectMemberNodeFactory(CompositionContainer parentContainer, string fileExtension) : base(parentContainer, fileExtension) { }

        public abstract List<TypeSplitter> ObjectSplitters { get; }

        protected Dictionary<INodeInfo, NodeInfoExtension> AuxNodeInfo { get; } = new Dictionary<INodeInfo, NodeInfoExtension>();

        public virtual ObjectMemberFilter MemberFilter { get; } = new ObjectMemberFilter();

        protected override IEnumerable<INodeInfo> GenerateNodeInfos(string filename)
        {
            foreach (var os in ObjectSplitters)
            {
                if(!typeof(ObjectSplitNode).IsAssignableFrom(os.SplitterType)) continue;
                var category = os.BaseType.GetCSharpName(true);
                foreach (var prop in os.BaseType.GetProperties())
                {
                    if(!MemberFilter.AllowMember(prop)) continue;
                    yield return CreateMemberNodeInfo(prop, os, category, filename);
                }
            }
        }

        private INodeInfo CreateMemberNodeInfo(MemberInfo member, TypeSplitter os, string category, string filename)
        {

            var name = $"{os.BaseName}.{member.Name}";
            var ninfo = FNodeInfoFactory.CreateNodeInfo(name, category, "", filename, true);

            ninfo.BeginUpdate();

            ninfo.Credits = "microdee, MESO";
            ninfo.Help = "Exposes a single member of a CLR Type";
            ninfo.Tags = "property, field, member";
            ninfo.Type = NodeType.Plugin;
            ninfo.Factory = this;

            if (os.Info != null) ninfo.UpdateFromPluginInfo(os.Info);

            AuxNodeInfo.UpdateGeneric(ninfo, new NodeInfoExtension(os, member.Name));
            ninfo.Arguments = os.SplitterType.ToString();

            ninfo.CommitUpdate();

            return ninfo;
        }

        protected override Type DelegateNodeType(INodeInfo nodeInfo)
        {
            if (AuxNodeInfo.TryGetValue(nodeInfo, out var nie))
            {
                return nie.Splitter.SplitterType;
            }
            return null;
        }

        protected override void ProcessNode(PluginContainer container, INodeInfo nodeInfo)
        {
            if (container.PluginBase is ObjectSplitNode osn && AuxNodeInfo.TryGetValue(nodeInfo, out var nie))
            {
                osn.MemberWhiteList = new StringCollection { nie.MemberName };
                osn.Initialize();
            }
        }

        public override string ToString()
        {
            return GetType().ToString();
        }
    }
}
