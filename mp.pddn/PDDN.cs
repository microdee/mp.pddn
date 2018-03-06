﻿using System;
using System.Collections.Generic;
using System.Linq;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils.IO;
using NGISpread = VVVV.PluginInterfaces.V2.NonGeneric.ISpread;
using NGIDiffSpread = VVVV.PluginInterfaces.V2.NonGeneric.IDiffSpread;

namespace mp.pddn
{
    /// <summary>
    /// An abstract pin wrapper containing all types a pin is operating with
    /// </summary>
    public abstract class SimplePin
    {
        public object CustomData;
        public Type Type;
        public IOAttribute Attributes;
        public IIOContainer IOContainer;
    }

    /// <summary>
    /// Simple pin with spread
    /// </summary>
    public class SpreadPin : SimplePin
    {
        public NGISpread Spread;
        public SpreadPin(NGISpread spread, IOAttribute attr, IIOContainer ioc)
        {
            Attributes = attr;
            IOContainer = ioc;
            Spread = spread;
        }
    }

    /// <summary>
    /// Simple pin with DiffSpread
    /// </summary>
    public class DiffSpreadPin : SimplePin
    {
        public NGIDiffSpread Spread;
        public DiffSpreadPin(NGIDiffSpread spread, IOAttribute attr, IIOContainer ioc)
        {
            Attributes = attr;
            IOContainer = ioc;
            Spread = spread;
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
        public Dictionary<string, SpreadPin> OutputPins = new Dictionary<string, SpreadPin>();
        public List<string> InputTaggedForRemove = new List<string>();
        public List<string> OutputTaggedForRemove = new List<string>();
        public bool ExchangingInput = false;
        public bool ExchangingOutput = false;

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

        public void AddInput(Type T, InputAttribute attr)
        {
            if (ExchangingInput && InputTaggedForRemove.Contains(attr.Name))
                InputTaggedForRemove.Remove(attr.Name);

            if (InputPins.ContainsKey(attr.Name) && InputPins[attr.Name].Type != T)
            {
                RemoveInput(attr.Name);
            }
            if (!InputPins.ContainsKey(attr.Name))
            {
                var pinType = typeof (IDiffSpread<>).MakeGenericType(T);
                var ioc = FIOFactory.CreateIOContainer(pinType, attr);
                var ispread = ioc.ToIDiffSpread();
                var pin = new DiffSpreadPin(ispread, attr, ioc);
                InputPins.Add(attr.Name, pin);
                pin.Type = T;
            }
        }
        public void AddInput(Type T, InputAttribute attr, object obj)
        {
            AddInput(T, attr);
            InputPins[attr.Name].CustomData = obj;
        }
        public void AddInputBinSized(Type T, InputAttribute attr)
        {
            if (ExchangingInput && InputTaggedForRemove.Contains(attr.Name))
                InputTaggedForRemove.Remove(attr.Name);

            if (InputPins.ContainsKey(attr.Name) && InputPins[attr.Name].Type != T)
            {
                RemoveInput(attr.Name);
            }
            if (!InputPins.ContainsKey(attr.Name))
            {
                var pinType = typeof (IDiffSpread<>).MakeGenericType(typeof (ISpread<>).MakeGenericType(T));
                var ioc = FIOFactory.CreateIOContainer(pinType, attr);
                var ispread = ioc.ToIDiffSpread();
                var pin = new DiffSpreadPin(ispread, attr, ioc);
                InputPins.Add(attr.Name, pin);
                pin.Type = T;
            }
        }
        public void AddInputBinSized(Type T, InputAttribute attr, object obj)
        {
            AddInputBinSized(T, attr);
            InputPins[attr.Name].CustomData = obj;
        }
        public void ChangeInputType(string name, Type T, bool BinSizable = false)
        {
            if (!InputPins.ContainsKey(name)) return;
            var pin = InputPins[name];
            pin.IOContainer.Dispose();
            var attr = pin.Attributes;

            Type pinType;
            if (BinSizable)
                pinType = typeof(IDiffSpread<>).MakeGenericType(typeof(ISpread<>).MakeGenericType(T));
            else
                pinType = typeof(IDiffSpread<>).MakeGenericType(T);

            var ioc = FIOFactory.CreateIOContainer(pinType, attr);
            var ispread = ioc.ToIDiffSpread();
            pin.IOContainer = ioc;
            pin.Spread = ispread;
            pin.Type = T;
        }
        public void AddOutput(Type T, OutputAttribute attr)
        {
            if (ExchangingOutput && OutputTaggedForRemove.Contains(attr.Name))
                OutputTaggedForRemove.Remove(attr.Name);

            if (OutputPins.ContainsKey(attr.Name) && OutputPins[attr.Name].Type != T)
            {
                RemoveOutput(attr.Name);
            }
            if (!OutputPins.ContainsKey(attr.Name))
            {
                var pinType = typeof (ISpread<>).MakeGenericType(T);
                var ioc = FIOFactory.CreateIOContainer(pinType, attr);
                var ispread = ioc.ToISpread();
                var pin = new SpreadPin(ispread, attr, ioc);
                OutputPins.Add(attr.Name, pin);
                pin.Type = T;
            }
        }
        public void AddOutput(Type T, OutputAttribute attr, object obj)
        {
            AddOutput(T, attr);
            OutputPins[attr.Name].CustomData = obj;
        }
        public void AddOutputBinSized(Type T, OutputAttribute attr)
        {
            if (ExchangingOutput && OutputTaggedForRemove.Contains(attr.Name))
                OutputTaggedForRemove.Remove(attr.Name);

            if (OutputPins.ContainsKey(attr.Name) && OutputPins[attr.Name].Type != T)
            {
                RemoveOutput(attr.Name);
            }
            if (!OutputPins.ContainsKey(attr.Name))
            {
                var pinType = typeof (ISpread<>).MakeGenericType(typeof (ISpread<>).MakeGenericType(T));
                var ioc = FIOFactory.CreateIOContainer(pinType, attr);
                var ispread = ioc.ToISpread();
                var pin = new SpreadPin(ispread, attr, ioc);
                OutputPins.Add(attr.Name, pin);
                pin.Type = T;
            }
        }
        public void AddOutputBinSized(Type T, OutputAttribute attr, object obj)
        {
            AddOutputBinSized(T, attr);
            OutputPins[attr.Name].CustomData = obj;
        }
        public void ChangeOutputType(string name, Type T, bool BinSizable = false)
        {
            if (!OutputPins.ContainsKey(name)) return;
            var pin = OutputPins[name];
            pin.IOContainer.Dispose();
            var attr = pin.Attributes;

            Type pinType;
            if (BinSizable)
                pinType = typeof(ISpread<>).MakeGenericType(typeof(ISpread<>).MakeGenericType(T));
            else
                pinType = typeof(ISpread<>).MakeGenericType(T);

            var ioc = FIOFactory.CreateIOContainer(pinType, attr);
            var ispread = ioc.ToIDiffSpread();
            pin.IOContainer = ioc;
            pin.Spread = ispread;
            pin.Type = T;
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
