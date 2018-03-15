using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V1;
using VVVV.Utils.VColor;
using VVVV.Utils.Win32;

namespace mp.pddn
{
    /// <summary>
    /// An easy to use input which accepts anything
    /// </summary>
    public class GenericInput
    {
        public class ConnectionEventArgs : EventArgs
        {
            public bool Connected;

            public ConnectionEventArgs(bool connected)
            {
                Connected = connected;
            }
        }
        private INodeIn _pin;
        private bool _prevConnected;

        public INodeIn Pin
        {
            get => _pin;
            private set => _pin = value;
        }

        public bool Connected
        {
            get
            {
                object usi = GetUpstreamInterface();
                return usi != null;
            }
        }

        public object this[int i]
        {
            get
            {
                if (!Connected) return null;
                var usi = GetUpstreamInterface();
                _pin.GetUpsreamSlice(i, out var ui);
                switch (usi)
                {
                    case IValueData _:
                    {
                        var temp = usi as IValueData;
                        temp.GetValue(ui, out var t);
                        return t;
                    }
                    case IColorData _:
                    {
                        var temp = usi as IColorData;
                        temp.GetColor(ui, out var t);
                        return t;
                    }
                    case IStringData _:
                    {
                        var temp = usi as IStringData;
                        temp.GetString(ui, out var t);
                        return t;
                    }
                    case IRawData _:
                    {
                        var temp = usi as IRawData;
                        temp.GetData(ui, out var t);
                        return t;
                    }
                    case IEnumerable<object> _:
                    {
                        var temp = usi as IEnumerable<object>;
                        return temp.ToArray()[ui];
                    }
                }
                return null;
            }
        }

        public object GetUpstreamInterface()
        {
            try
            {
                _pin.GetUpstreamInterface(out object usi);
                return usi;
            }
            catch
            {
                return null;
            }
        }

        public GenericInput(IPluginHost plgh, IOAttribute attr, IMainLoop mainloop = null)
        {
            plgh.CreateNodeInput(attr.Name, (TSliceMode)attr.SliceMode, (TPinVisibility)attr.Visibility, out _pin);
            _pin.SetSubType2(null, new Guid[] { }, "Variant");
            _pin.Order = attr.Order;

            if(mainloop == null) return;
            mainloop.OnUpdateView += (sender, args) =>
            {
                var currconn = Connected;
                if (currconn != _prevConnected)
                {
                    OnConnectionChange?.Invoke(this, new ConnectionEventArgs(currconn));
                }
                _prevConnected = currconn;
            };
        }

        public event EventHandler<ConnectionEventArgs> OnConnectionChange;
    }

    /// <summary>
    /// An easy to use binsized input which accepts anything
    /// </summary>
    public class GenericBinSizedInput
    {
        private INodeIn _pin;
        private IValueIn _binSizePin;
        private bool _prevConnected;

        public INodeIn Pin
        {
            get => _pin;
            private set => _pin = value;
        }

        public IValueIn BinSizePin
        {
            get => _binSizePin;
            private set => _binSizePin = value;
        }

        public bool Connected
        {
            get
            {
                var usi = GetUpstreamInterface();
                return usi != null;
            }
        }

        private List<int> ConstructBinOffsets()
        {
            var res = new List<int>();
            
            int currslice = 0;
            int cb = 0;
            while (currslice < _pin.SliceCount)
            {
                if ((cb > _binSizePin.SliceCount) && (currslice <= 0))
                    break;
                int mcb = cb % _binSizePin.SliceCount;
                double btemp = 0;
                _binSizePin.GetValue(mcb, out btemp);
                int cbin = (int)btemp;
                if (cbin > 0)
                {
                    res.Add(currslice);
                    currslice += cbin;
                }
                if (cbin < 0)
                {
                    res.Add(0);
                }
                cb++;
            }
            
            return res;
        }

        public int SliceCount
        {
            get
            {
                int slicecount = 0;
                int currslice = 0;
                int cb = 0;
                while (currslice < _pin.SliceCount)
                {
                    if ((cb >= _binSizePin.SliceCount) && (currslice <= 0))
                        break;
                    int mcb = cb % _binSizePin.SliceCount;
                    _binSizePin.GetValue(mcb, out var btemp);
                    int cbin = (int)btemp;
                    if (cbin > 0)
                    {
                        currslice += cbin;
                        slicecount++;
                    }
                    if (cbin < 0)
                        slicecount++;
                    cb++;
                }
                return slicecount;
            }
        }

        public List<object> this[int i]
        {
            get
            {
                if (!Connected) return null;
                var usi = GetUpstreamInterface();
                _binSizePin.GetValue(i, out var btemp);
                int currbin = (int) btemp;
                var offsets = ConstructBinOffsets();
                int curroffs = offsets[i % offsets.Count];
                if (currbin < 0)
                {
                    currbin = _pin.SliceCount;
                    curroffs = 0;
                }
                var res = new List<object>();

                for (int j = 0; j < currbin; j++)
                {
                    _pin.GetUpsreamSlice(curroffs + j, out var ui);
                    switch (usi)
                    {
                        case IValueData _:
                        {
                            var temp = usi as IValueData;
                            temp.GetValue(ui, out var t);
                            res.Add(t);
                            continue;
                        }
                        case IColorData _:
                        {
                            var temp = usi as IColorData;
                            temp.GetColor(ui, out var t);
                            res.Add(t);
                            continue;
                        }
                        case IStringData _:
                        {
                            var temp = usi as IStringData;
                            temp.GetString(ui, out var t);
                            res.Add(t);
                            continue;
                        }
                        case IRawData _:
                        {
                            var temp = usi as IRawData;
                            temp.GetData(ui, out var t);
                            res.Add(t);
                            continue;
                        }
                        case IEnumerable<object> _:
                        {
                            var temp = usi as IEnumerable<object>;
                            res.Add(temp.ToArray()[ui]);
                            continue;
                        }
                    }
                    res.Add(null);
                }
                return res;
            }
        }

        public object GetUpstreamInterface()
        {
            try
            {
                _pin.GetUpstreamInterface(out object usi);
                return usi;
            }
            catch
            {
                return null;
            }
        }

        public GenericBinSizedInput(IPluginHost plgh, InputAttribute attr, IMainLoop mainloop = null)
        {
            plgh.CreateNodeInput(attr.Name, (TSliceMode)attr.SliceMode, (TPinVisibility)attr.Visibility, out _pin);
            plgh.CreateValueInput(attr.Name + " Bin Size", 1, null, TSliceMode.Dynamic, (TPinVisibility) attr.Visibility, out _binSizePin);
            _pin.SetSubType2(null, new Guid[] { }, "Variant");
            _binSizePin.SetSubType(-1, double.MaxValue, 1, attr.BinSize, false, false, true);
            _pin.Order = attr.Order;
            _binSizePin.Order = attr.BinOrder;

            if (mainloop == null) return;
            mainloop.OnUpdateView += (sender, args) =>
            {
                var currconn = Connected;
                if (currconn != _prevConnected)
                {
                    OnConnectionChange?.Invoke(this, new GenericInput.ConnectionEventArgs(currconn));
                }
                _prevConnected = currconn;
            };
        }

        public event EventHandler<GenericInput.ConnectionEventArgs> OnConnectionChange;
    }
}
