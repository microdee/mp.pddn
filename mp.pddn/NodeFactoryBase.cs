using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using md.stdl.Coding;
using VVVV.Core.Logging;
using VVVV.Core.Model;
using VVVV.Hosting.Factories;
using VVVV.Hosting.Interfaces;
using VVVV.Hosting.IO;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.Collections;
using VVVV.Utils.Reflection;

namespace mp.pddn
{
    //[Export(typeof(IAddonFactory))]
    //[Export(typeof(MyNodeFactory))]
    //[ComVisible(false)]
    
    public abstract class NodeFactoryBase : AbstractFileFactory<IInternalPluginHost>
    {
        protected readonly Dictionary<IPluginBase, PluginContainer> PluginContainers = new Dictionary<IPluginBase, PluginContainer>();
        protected readonly CompositionContainer ParentContainer;

        [Import]
        protected DotNetPluginFactory DotNetFactory;

        [Import]
        protected IORegistry IORegistry;

        public override string JobStdSubPath => "plugins";

        public event PluginCreatedDelegate PluginCreated;
        public event PluginDeletedDelegate PluginDeleted;


        public NodeFactoryBase(CompositionContainer parentContainer) : this(parentContainer, ".dll;.exe") { }

        protected NodeFactoryBase(CompositionContainer parentContainer, string fileExtension) : base(fileExtension)
        {
            ParentContainer = parentContainer;
        }

        protected virtual bool IsFilenameValid(string filename)
        {
            return filename.Equals(GetType().Assembly.Location, StringComparison.InvariantCultureIgnoreCase);
        }

        protected abstract IEnumerable<INodeInfo> GenerateNodeInfos(string filename);

        protected override IEnumerable<INodeInfo> LoadNodeInfos(string filename)
        {
            if (IsFilenameValid(filename))
            {
                return GenerateNodeInfos(filename);
            }
            return Enumerable.Empty<INodeInfo>();
        }

        protected override void AddSubDir(string dir, bool recursive)
        {
            // Ignore obj directories used by C# IDEs and ignore dynamic bin directories
            if (dir.EndsWith(@"\obj\x86") || dir.EndsWith(@"\obj\x64") || dir.EndsWith(@"\bin\Dynamic")) return;

            base.AddSubDir(dir, recursive);
        }

        protected abstract Type DelegateNodeType(INodeInfo nodeInfo);
        protected virtual void ProcessNode(PluginContainer container, INodeInfo nodeInfo) { }

        protected override bool CreateNode(INodeInfo nodeInfo, IInternalPluginHost pluginHost)
        {
            var plugin = pluginHost.Plugin;

            //make the host mark all its pins for possible deletion
            pluginHost.Plugin = null;

            //dispose previous plugin
            if (plugin != null) DisposePlugin(plugin);

            //create the new plugin
            var type = DelegateNodeType(nodeInfo);
            if (type == null) return false;
            
            var pluginContainer = new PluginContainer(
                pluginHost,
                IORegistry,
                ParentContainer,
                FNodeInfoFactory,
                null,
                type,
                nodeInfo);

            plugin = pluginContainer;
            PluginContainers.UpdateGeneric(pluginContainer.PluginBase, pluginContainer);
            AssignOptionalPluginInterfaces(pluginHost, pluginContainer.PluginBase);

            ProcessNode(pluginContainer, nodeInfo);

            PluginCreated?.Invoke(pluginContainer.PluginBase, pluginHost);

            pluginHost.Plugin = plugin;
            return true;
        }

        protected override bool DeleteNode(INodeInfo nodeInfo, IInternalPluginHost pluginHost)
        {
            var plugin = pluginHost.Plugin;
            if (plugin != null)
            {
                DisposePlugin(plugin);
                return true;
            }
            return false;
        }

        public void DisposePlugin(IPluginBase plugin)
        {
            //Send event before delete
            PluginDeleted?.Invoke(plugin);

            var disposablePlugin = plugin as IDisposable;
            if (plugin is PluginContainer pluginContainer)
            {
                PluginContainers.Remove(pluginContainer.PluginBase);
                pluginContainer.Dispose();
            }
            else if (PluginContainers.ContainsKey(plugin))
            {
                PluginContainers[plugin].Dispose();
                PluginContainers.Remove(plugin);
            }
            else
            {
                disposablePlugin?.Dispose();
            }
        }

        private static void AssignOptionalPluginInterfaces(IInternalPluginHost pluginHost, IPluginBase pluginBase)
        {
            switch (pluginBase)
            {
                case IWin32Window win32Window:
                    pluginHost.Win32Window = win32Window;
                    break;
                case IPluginConnections pluginConnections:
                    pluginHost.Connections = pluginConnections;
                    break;
                case IPluginDXLayer pluginDXLayer:
                    pluginHost.DXLayer = pluginDXLayer;
                    break;
                case IPluginDXMesh pluginDXMesh:
                    pluginHost.DXMesh = pluginDXMesh;
                    break;
                case IPluginDXTexture pluginTexture:
                    pluginHost.DXTexture = pluginTexture;
                    break;
                case IPluginDXTexture2 pluginTexture2:
                    pluginHost.DXTexture2 = pluginTexture2;
                    break;
            }
            if (pluginBase is IPluginDXResource pluginDXResource)
            {
                pluginHost.DXResource = pluginDXResource;
            }
        }
    }
}
