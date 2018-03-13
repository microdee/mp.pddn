using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Coding;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

namespace mp.pddn
{
    public class ConfigurableTypePinGroup : IDisposable
    {
        public PinDictionary Pd { get; }
        public IDiffSpread<string> TypeConfigPin { get; }

        public GenericInput RefTypePin { get; }
        public ISpread<int> TypeInheritenceLevelPin { get; }
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
            {"color", typeof(RGBAColor) },
            {"vcolor", typeof(VColor) }
        };

        public void AddInput(params InputAttribute[] attrs)
        {
            foreach (var attr in attrs)
            {
                attr.Order = StartOrder + Pd.InputPins.Count * 2 + 3;
                Pd.AddInput(GroupType, attr);
            }
        }
        public void AddInput(params (InputAttribute attr, object auxobj)[] attrs)
        {
            foreach (var attr in attrs)
            {
                attr.attr.Order = StartOrder + Pd.InputPins.Count * 2 + 3;
                Pd.AddInput(GroupType, attr.attr, attr.auxobj);
            }
        }

        public void AddInputBinSized(params InputAttribute[] attrs)
        {
            foreach (var attr in attrs)
            {
                attr.Order = StartOrder + Pd.InputPins.Count * 2 + 3;
                attr.BinOrder = StartOrder + Pd.InputPins.Count * 2 + 4;
                Pd.AddInputBinSized(GroupType, attr);
            }
        }
        public void AddInputBinSized(params (InputAttribute attr, object auxobj)[] attrs)
        {
            foreach (var attr in attrs)
            {
                attr.attr.Order = StartOrder + Pd.InputPins.Count * 2 + 3;
                attr.attr.BinOrder = StartOrder + Pd.InputPins.Count * 2 + 4;
                Pd.AddInputBinSized(GroupType, attr.attr, attr.auxobj);
            }
        }

        public void AddOutput(params OutputAttribute[] attrs)
        {
            foreach (var attr in attrs)
            {
                attr.Order = StartOrder + Pd.OutputPins.Count * 2 + 3;
                Pd.AddOutput(GroupType, attr);
            }
        }
        public void AddOutput(params (OutputAttribute attr, object auxobj)[] attrs)
        {
            foreach (var attr in attrs)
            {
                attr.attr.Order = StartOrder + Pd.OutputPins.Count * 2 + 3;
                Pd.AddOutput(GroupType, attr.attr, attr.auxobj);
            }
        }

        public void AddOutputBinSized(params OutputAttribute[] attrs)
        {
            foreach (var attr in attrs)
            {
                attr.Order = StartOrder + Pd.OutputPins.Count * 2 + 3;
                attr.BinOrder = StartOrder + Pd.OutputPins.Count * 2 + 4;
                Pd.AddOutputBinSized(GroupType, attr);
            }
        }
        public void AddOutputBinSized(params (OutputAttribute attr, object auxobj)[] attrs)
        {
            foreach (var attr in attrs)
            {
                attr.attr.Order = StartOrder + Pd.OutputPins.Count * 2 + 3;
                attr.attr.BinOrder = StartOrder + Pd.OutputPins.Count * 2 + 4;
                Pd.AddOutputBinSized(GroupType, attr.attr, attr.auxobj);
            }
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

        public ConfigurableTypePinGroup(IPluginHost2 plgh, IIOFactory iofact, string groupname, int startOrder = 0, bool hideTypeLearn = false)
        {
            Pd = new PinDictionary(iofact);
            StartOrder = startOrder;
            TypeConfigPin = iofact.CreateDiffSpread<string>(new ConfigAttribute(groupname + " Type")
            {
                Order = startOrder,
                Visibility = hideTypeLearn ? PinVisibility.OnlyInspector : PinVisibility.True
            });
            RefTypePin = new GenericInput(plgh, new InputAttribute($"Learn {groupname} Type Reference")
            {
                Order = startOrder,
                Visibility = hideTypeLearn ? PinVisibility.OnlyInspector : PinVisibility.True
            });
            TypeInheritenceLevelPin = iofact.CreateSpread<int>(new InputAttribute($"Learn {groupname} Type Inheritence Level")
            {
                Order = startOrder + 1,
                DefaultValue = 0,
                Visibility = hideTypeLearn ? PinVisibility.OnlyInspector : PinVisibility.Hidden
            });
            LearnTypeBangPin = iofact.CreateDiffSpread<bool>(new InputAttribute($"Learn {groupname} Type")
            {
                Order = startOrder + 2,
                IsBang = true,
                Visibility = hideTypeLearn ? PinVisibility.OnlyInspector : PinVisibility.True
            });

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
            LearnTypeBangPin.Changed += spread =>
            {
                if (!LearnTypeBangPin[0]) return;
                try
                {
                    var types = RefTypePin[0].GetType().GetTypes().ToArray();
                    TypeConfigPin[0] = types[Math.Min(TypeInheritenceLevelPin[0], types.Length - 1)].AssemblyQualifiedName;
                    TypeConfigPin.Stream.IsChanged = true;
                }
                catch (Exception e)
                { }

                OnTypeLearnt?.Invoke(this, EventArgs.Empty);
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
