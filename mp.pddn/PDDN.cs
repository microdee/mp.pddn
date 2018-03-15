using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils.IO;

namespace mp.pddn
{
    /// <summary>
    /// An abstract pin wrapper containing all types a pin is operating with
    /// </summary>
    public abstract class SimplePin
    {
        public object CustomData;
        public bool BinSized = false;
        public Type Type;
        public IOAttribute Attributes;
        public IIOContainer IOContainer;
    }

    /// <summary>
    /// Simple pin with spread
    /// </summary>
    public class SpreadPin : SimplePin
    {
        public ISpread Spread;
        public SpreadPin(ISpread spread, IOAttribute attr, IIOContainer ioc)
        {
            Attributes = attr;
            IOContainer = ioc;
            Spread = spread;
        }

        public T GetSlice<T>(int i, T fallback)
        {
            if (Spread[i] is T res)
                return res;
            else return fallback;
        }

        public object this[int i]
        {
            get => Spread[i];
            set => Spread[i] = value;
        }
    }

    /// <summary>
    /// Simple pin with DiffSpread
    /// </summary>
    public class DiffSpreadPin : SimplePin
    {
        public IDiffSpread Spread;
        public DiffSpreadPin(IDiffSpread spread, IOAttribute attr, IIOContainer ioc)
        {
            Attributes = attr;
            IOContainer = ioc;
            Spread = spread;
        }

        public T GetSlice<T>(int i, T fallback)
        {
            if (Spread[i] is T res)
                return res;
            else return fallback;
        }

        public object this[int i]
        {
            get => Spread[i];
            set => Spread[i] = value;
        }
    }

    /// <summary>
    /// Simple class component for managing group of massively dynamic pins
    /// </summary>
    public class PinDictionary
    {
        protected IIOFactory FIOFactory;

        public PinDictionary(IIOFactory iofactory)
        {
            FIOFactory = iofactory;
        }

        public Dictionary<string, DiffSpreadPin> InputPins = new Dictionary<string, DiffSpreadPin>();
        public Dictionary<string, DiffSpreadPin> ConfigPins = new Dictionary<string, DiffSpreadPin>();
        public Dictionary<string, SpreadPin> OutputPins = new Dictionary<string, SpreadPin>();
        public List<string> ConfigTaggedForRemove = new List<string>();
        public List<string> InputTaggedForRemove = new List<string>();
        public List<string> OutputTaggedForRemove = new List<string>();
        public bool ExchangingConfig = false;
        public bool ExchangingInput = false;
        public bool ExchangingOutput = false;

        public void BeginConfigExchange()
        {
            if (ExchangingConfig) return;
            ExchangingConfig = true;
            ConfigTaggedForRemove = ConfigPins.Keys.ToList();
        }

        public void EndConfigExchange()
        {
            if (!ExchangingConfig) return;
            ExchangingConfig = false;
            RemoveTaggedConfig();
        }

        public void BeginInputExchange()
        {
            if(ExchangingInput) return;
            ExchangingInput = true;
            InputTaggedForRemove = InputPins.Keys.ToList();
        }

        public void EndInputExchange()
        {
            if(!ExchangingInput) return;
            ExchangingInput = false;
            RemoveTaggedInput();
        }

        public void BeginOutputExchange()
        {
            if(ExchangingOutput) return;
            ExchangingOutput = true;
            OutputTaggedForRemove = OutputPins.Keys.ToList();
        }

        public void EndOutputExchange()
        {
            if (!ExchangingOutput) return;
            ExchangingOutput = false;
            RemoveTaggedOutput();
        }

        protected SimplePin GetPin(string name, IDictionary pindict)
        {
            SimplePin pin;
            switch (pindict)
            {
                case Dictionary<string, DiffSpreadPin> dpd:
                    pin = dpd[name];
                    break;
                case Dictionary<string, SpreadPin> spd:
                    pin = spd[name];
                    break;
                default:
                    return null;
            }
            return pin;
        }

        protected Type GetPinType(Type T, bool binSizable, IDictionary pindict)
        {
            var spreadType = pindict is Dictionary<string, DiffSpreadPin> ? typeof(IDiffSpread<>) : typeof(ISpread<>);

            Type pinType;
            if (binSizable)
                pinType = spreadType.MakeGenericType(typeof(ISpread<>).MakeGenericType(T));
            else
                pinType = spreadType.MakeGenericType(T);
            return pinType;
        }

        protected void ChangeType(string name, Type T, IDictionary pindict)
        {
            var pin = GetPin(name, pindict);
            if (pin == null) return;
            ChangeType(name, T, pin.BinSized, pindict);
        }

        protected void ChangeType(string name, Type T, bool binSizable, IDictionary pindict)
        {
            if (!pindict.Contains(name)) return;

            var pin = GetPin(name, pindict);
            if(pin == null) return;

            pin.IOContainer.Dispose();
            var attr = pin.Attributes;

            var pinType = GetPinType(T, binSizable, pindict);

            var ioc = FIOFactory.CreateIOContainer(pinType, attr);
            pin.IOContainer = ioc;
            switch (pindict)
            {
                case Dictionary<string, DiffSpreadPin> dpd:
                    var diffpin = (DiffSpreadPin) pin;
                    diffpin.Spread = ioc.ToIDiffSpread();
                    break;
                case Dictionary<string, SpreadPin> spd:
                    var spin = (SpreadPin) pin;
                    spin.Spread = ioc.ToISpread();
                    break;
            }
            pin.Type = T;
            pin.BinSized = binSizable;
        }

        protected void AddPin(Type T, IOAttribute attr, bool binSizable, object customData, IDictionary pindict)
        {
            SimplePin CreatePin(bool isdiff)
            {
                var pinType = GetPinType(T, binSizable, pindict);

                var ioc = FIOFactory.CreateIOContainer(pinType, attr);
                if (isdiff) return new DiffSpreadPin(ioc.ToIDiffSpread(), attr, ioc)
                {
                    Type = T,
                    BinSized = binSizable,
                    CustomData = customData
                };
                else return new SpreadPin(ioc.ToISpread(), attr, ioc)
                {
                    Type = T,
                    BinSized = binSizable,
                    CustomData = customData
                };
            }
            switch (pindict)
            {
                case Dictionary<string, DiffSpreadPin> diffpindict:
                    if (diffpindict.ContainsKey(attr.Name) && diffpindict[attr.Name].Type != T)
                        ChangeType(attr.Name, T, pindict);

                    if (!diffpindict.ContainsKey(attr.Name))
                    {
                        var pin = (DiffSpreadPin)CreatePin(true);
                        diffpindict.Add(attr.Name, pin);
                    }
                    break;
                case Dictionary<string, SpreadPin> spindict:
                    if (spindict.ContainsKey(attr.Name) && spindict[attr.Name].Type != T)
                        ChangeType(attr.Name, T, pindict);

                    if (!spindict.ContainsKey(attr.Name))
                    {
                        var pin = (SpreadPin)CreatePin(false);
                        spindict.Add(attr.Name, pin);
                    }
                    break;
            }
        }

        public DiffSpreadPin AddConfig(Type T, ConfigAttribute attr, object obj = null)
        {
            if (ExchangingConfig && ConfigTaggedForRemove.Contains(attr.Name))
                ConfigTaggedForRemove.Remove(attr.Name);

            AddPin(T, attr, false, obj, ConfigPins);
            return ConfigPins[attr.Name];
        }
        public void ChangeConfigType(string name, Type T)
        {
            ChangeType(name, T, false, ConfigPins);
        }

        public DiffSpreadPin AddInput(Type T, InputAttribute attr, bool binSized = false, object obj = null)
        {
            if (ExchangingInput && InputTaggedForRemove.Contains(attr.Name))
                InputTaggedForRemove.Remove(attr.Name);

            AddPin(T, attr, binSized, obj, InputPins);
            return InputPins[attr.Name];
        }

        public void ChangeInputType(string name, Type T)
        {
            ChangeType(name, T, InputPins);
        }
        public void ChangeInputType(string name, Type T, bool binSizable)
        {
            ChangeType(name, T, binSizable, InputPins);
        }
        public SpreadPin AddOutput(Type T, OutputAttribute attr, bool binSized = false, object obj = null)
        {
            if (ExchangingOutput && OutputTaggedForRemove.Contains(attr.Name))
                OutputTaggedForRemove.Remove(attr.Name);

            AddPin(T, attr, binSized, obj, OutputPins);
            return OutputPins[attr.Name];
        }
        public void ChangeOutputType(string name, Type T)
        {
            ChangeType(name, T, OutputPins);
        }
        public void ChangeOutputType(string name, Type T, bool binSizable)
        {
            ChangeType(name, T, binSizable, OutputPins);
        }

        public void RemoveConfig(string name)
        {
            if (!ConfigPins.ContainsKey(name)) return;
            ConfigPins[name].IOContainer.Dispose();
            ConfigPins.Remove(name);
        }
        public void RemoveTaggedConfig()
        {
            foreach (var k in ConfigTaggedForRemove)
            {
                RemoveConfig(k);
            }
        }
        public void RemoveAllConfig()
        {
            foreach (var p in ConfigPins.Keys)
            {
                ConfigPins[p].IOContainer.Dispose();
            }
            ConfigPins.Clear();
        }

        public void RemoveInput(string name)
        {
            if (!InputPins.ContainsKey(name)) return;
            InputPins[name].IOContainer.Dispose();
            InputPins.Remove(name);
        }
        public void RemoveTaggedInput()
        {
            foreach (var k in InputTaggedForRemove)
            {
                RemoveInput(k);
            }
        }
        public void RemoveAllInput()
        {
            foreach (var p in InputPins.Keys)
            {
                InputPins[p].IOContainer.Dispose();
            }
            InputPins.Clear();
        }
        public void RemoveOutput(string name)
        {
            if (!OutputPins.ContainsKey(name)) return;
            OutputPins[name].IOContainer.Dispose();
            OutputPins.Remove(name);
        }
        public void RemoveTaggedOutput()
        {
            foreach (var k in OutputTaggedForRemove)
            {
                RemoveOutput(k);
            }
        }
        public void RemoveAllOutput()
        {
            foreach (var p in OutputPins.Keys)
            {
                OutputPins[p].IOContainer.Dispose();
            }
            OutputPins.Clear();
        }

        public int InputSpreadMax => InputPins.Count > 0 ? InputPins.Values.Max(pin => pin.Spread.SliceCount) : 0;
        public int OutputSpreadMax => OutputPins.Count > 0 ? OutputPins.Values.Max(pin => pin.Spread.SliceCount) : 0;
        public int InputSpreadMin => InputPins.Count > 0 ? InputPins.Values.Min(pin => pin.Spread.SliceCount) : 0;
        public int OutputSpreadMin => OutputPins.Count > 0 ? OutputPins.Values.Min(pin => pin.Spread.SliceCount) : 0;

        public bool InputChanged => InputPins.Values.Any(pin => pin.Spread.IsChanged);
    }
}
