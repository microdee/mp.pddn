using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Coding;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.Reflection;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

namespace mp.pddn
{
    public class TypeIsNullException : Exception
    {
        public override string Message => "Pingroup type is not set yet properly";
    }

    public class ConfigurableTypePinGroup : IDisposable
    {
        private Type[] _typeInheritance;
        private string _inheritanceEnumName;

        public PinDictionary Pd { get; }
        public IDiffSpread<string> TypeConfigPin { get; }

        public GenericInput RefTypePin { get; }
        public ISpread<EnumEntry> TypeInheritenceLevelPin { get; }
        public IDiffSpread<bool> LearnTypeBangPin { get; }

        public Type GroupType { get; private set; }

        public int StartOrder { get; }

        public bool OnlyAllowMappedTypes { get; set; } = false;

        public Dictionary<string, Type> SimplifiedTypeMapping { get; set; } = new Dictionary<string, Type>
        {
            {"value", typeof(double) },
            {"double", typeof(double) },
            {"float", typeof(float) },
            {"int", typeof(int) },
            {"string", typeof(string) },
            {"vector2d", typeof(Vector2D) },
            {"vector3d", typeof(Vector3D) },    
            {"vector4d", typeof(Vector4D) },
            {"vector2", typeof(Vector2D) },
            {"vector3", typeof(Vector3D) },
            {"vector4", typeof(Vector4D) },
            {"matrix4x4", typeof(Matrix4x4) },
            {"transform", typeof(Matrix4x4) },
            {"color", typeof(RGBAColor) }
        };

        public ConfigurableTypePinGroup AddTypeMap(Type t)
        {
            return AddTypeMap(t.GetCSharpName(), t);
        }

        public ConfigurableTypePinGroup AddTypeMap(string alias, Type t)
        {
            SimplifiedTypeMapping.UpdateGeneric(alias.ToLowerInvariant(), t);
            return this;
        }

        public DiffSpreadPin AddInput(params InputAttribute[] attrs)
        {
            if(GroupType == null) throw new TypeIsNullException();

            return attrs.Select(attr => Pd.AddInput(GroupType, attr)).ToArray()[0];
        }
        public DiffSpreadPin AddInput(params (InputAttribute attr, object auxobj)[] attrs)
        {
            if (GroupType == null) throw new TypeIsNullException();

            return attrs.Select(attr => Pd.AddInput(GroupType, attr.attr, obj: attr.auxobj)).ToArray()[0];
        }

        public DiffSpreadPin AddInputBinSized(params InputAttribute[] attrs)
        {
            if (GroupType == null) throw new TypeIsNullException();

            return attrs.Select(attr => Pd.AddInput(GroupType, attr, binSized: true)).ToArray()[0];
        }
        public DiffSpreadPin AddInputBinSized(params (InputAttribute attr, object auxobj)[] attrs)
        {
            if (GroupType == null) throw new TypeIsNullException();

            return attrs.Select(attr => Pd.AddInput(GroupType, attr.attr, binSized: true, obj: attr.auxobj)).ToArray()[0];
        }

        public SpreadPin AddOutput(params OutputAttribute[] attrs)
        {
            if (GroupType == null) throw new TypeIsNullException();

            return attrs.Select(attr => Pd.AddOutput(GroupType, attr)).ToArray()[0];
        }
        public SpreadPin AddOutput(params (OutputAttribute attr, object auxobj)[] attrs)
        {
            if (GroupType == null) throw new TypeIsNullException();

            return attrs.Select(attr => Pd.AddOutput(GroupType, attr.attr, obj: attr.auxobj)).ToArray()[0];
        }

        public SpreadPin AddOutputBinSized(params OutputAttribute[] attrs)
        {
            if (GroupType == null) throw new TypeIsNullException();

            return attrs.Select(attr => Pd.AddOutput(GroupType, attr, binSized: true)).ToArray()[0];
        }
        public SpreadPin AddOutputBinSized(params (OutputAttribute attr, object auxobj)[] attrs)
        {
            if (GroupType == null) throw new TypeIsNullException();

            return attrs.Select(attr => Pd.AddOutput(GroupType, attr.attr, binSized: true, obj: attr.auxobj)).ToArray()[0];
        }

        public void RemoveInput(string name) { Pd.RemoveInput(name); }
        public void RemoveOutput(string name) { Pd.RemoveOutput(name); }

        public object GetInputSlice(string name, int i)
        {
            return Pd.InputPins.ContainsKey(name) ? Pd.InputPins[name][i] : null;
        }
        public T GetInputSlice<T>(string name, int i, T fallback)
        {
            return Pd.InputPins.ContainsKey(name) ? Pd.InputPins[name].GetSlice(i, fallback) : fallback;
        }
        public object GetOutputSlice(string name, int i)
        {
            return Pd.OutputPins.ContainsKey(name) ? Pd.OutputPins[name][i] : null;
        }
        public T GetOutputSlice<T>(string name, int i, T fallback)
        {
            return Pd.OutputPins.ContainsKey(name) ? Pd.OutputPins[name].GetSlice(i, fallback) : fallback;
        }

        public ConfigurableTypePinGroup(IPluginHost2 plgh, IIOFactory iofact, IMainLoop ml, string groupname, int startOrder = 0, bool hideTypeLearn = false)
        {
            Pd = new PinDictionary(iofact);
            StartOrder = startOrder;
            _inheritanceEnumName = $"{plgh.GetNodePath(false)}/{groupname}.TypeInheritance";
            
            EnumManager.UpdateEnum(_inheritanceEnumName, "TopLevel", new[] { "TopLevel" });

            TypeConfigPin = iofact.CreateDiffSpread<string>(new ConfigAttribute(groupname + " Type")
            {
                Order = startOrder,
                Visibility = hideTypeLearn ? PinVisibility.OnlyInspector : PinVisibility.True
            });
            RefTypePin = new GenericInput(plgh, new InputAttribute($"Learn {groupname} Type Reference")
            {
                Order = startOrder,
                Visibility = hideTypeLearn ? PinVisibility.OnlyInspector : PinVisibility.True
            }, ml);
            TypeInheritenceLevelPin = iofact.CreateSpread<EnumEntry>(new InputAttribute($"Learn {groupname} Type Inheritence Level")
            {
                Order = startOrder + 1,
                EnumName = _inheritanceEnumName,
                DefaultEnumEntry = "TopLevel",
                Visibility = hideTypeLearn ? PinVisibility.OnlyInspector : PinVisibility.Hidden
            });
            LearnTypeBangPin = iofact.CreateDiffSpread<bool>(new InputAttribute($"Learn {groupname} Type")
            {
                Order = startOrder + 2,
                IsBang = true,
                Visibility = hideTypeLearn ? PinVisibility.OnlyInspector : PinVisibility.True
            });

            RefTypePin.OnConnectionChange += (sender, args) =>
            {
                if (args.Connected)
                {
                    _typeInheritance = RefTypePin[0].GetType().GetTypes().ToArray();
                    var names = new [] { "TopLevel" }.Concat(_typeInheritance.Select(t => t.GetCSharpName(true))).ToArray();
                    EnumManager.UpdateEnum(_inheritanceEnumName, names[0], names);
                }
                else
                {
                    var names = new[] { "TopLevel" };
                    EnumManager.UpdateEnum(_inheritanceEnumName, names[0], names);
                }
            };

            LearnTypeBangPin.Changed += spread =>
            {
                if (!LearnTypeBangPin[0]) return;
                try
                {
                    var typesel = Math.Max(TypeInheritenceLevelPin[0].Index - 1, 0);
                    TypeConfigPin[0] = _typeInheritance[Math.Min(typesel, _typeInheritance.Length - 1)].AssemblyQualifiedName;
                    TypeConfigPin.Stream.IsChanged = true;
                }
                catch (Exception e)
                { }

                OnTypeLearnt?.Invoke(this, EventArgs.Empty);
            };

            TypeConfigPin.Changed += spread =>
            {
                TypeConfigPin.Stream.IsChanged = false;

                Type ctype = null;
                if (string.IsNullOrWhiteSpace(TypeConfigPin[0])) return;

                if (SimplifiedTypeMapping.ContainsKey(TypeConfigPin[0].ToLowerInvariant()))
                    ctype = SimplifiedTypeMapping[TypeConfigPin[0].ToLowerInvariant()];
                else if(!OnlyAllowMappedTypes) ctype = Type.GetType(TypeConfigPin[0]);

                if (ctype == null) return;

                OnTypeChangeBegin?.Invoke(this, EventArgs.Empty);

                GroupType = ctype;

                foreach (var inputpin in Pd.InputPins.Keys)
                {
                    Pd.ChangeInputType(inputpin, GroupType);
                }
                foreach (var outputpin in Pd.OutputPins.Keys)
                {
                    Pd.ChangeOutputType(outputpin, GroupType);
                }

                OnTypeChangeEnd?.Invoke(this, EventArgs.Empty);
            };
        }

        public void Dispose()
        {
            Pd.RemoveAllInput();
            Pd.RemoveAllOutput();
        }

        public event EventHandler OnTypeChangeBegin;
        public event EventHandler OnTypeChangeEnd;
        public event EventHandler OnTypeLearnt;
    }
}
