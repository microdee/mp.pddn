using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V1;
using VVVV.Utils.VColor;
using VVVV.Utils.Win32;

namespace VVVV.Nodes.PDDN
{
    public class GenericInput
    {
        public INodeIn Pin;
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
                Pin.GetUpsreamSlice(i, out var ui);
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
            Pin.GetUpstreamInterface(out object usi);
            return usi;
        }

        public GenericInput(IPluginHost plgh, IOAttribute attr)
        {
            plgh.CreateNodeInput(attr.Name, (TSliceMode)attr.SliceMode, (TPinVisibility)attr.Visibility, out Pin);
            Pin.SetSubType2(null, new Guid[] { }, "Variant");
            Pin.Order = attr.Order;
        }
    }

    public class GenericBinSizedInput
    {
        public INodeIn Pin;
        public IValueIn BinSizePin;
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
            while (currslice < Pin.SliceCount)
            {
                if ((cb > BinSizePin.SliceCount) && (currslice <= 0))
                    break;
                int mcb = cb % BinSizePin.SliceCount;
                double btemp = 0;
                BinSizePin.GetValue(mcb, out btemp);
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
                while (currslice < Pin.SliceCount)
                {
                    if ((cb >= BinSizePin.SliceCount) && (currslice <= 0))
                        break;
                    int mcb = cb % BinSizePin.SliceCount;
                    BinSizePin.GetValue(mcb, out var btemp);
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
                BinSizePin.GetValue(i, out var btemp);
                int currbin = (int) btemp;
                var offsets = ConstructBinOffsets();
                int curroffs = offsets[i % offsets.Count];
                if (currbin < 0)
                {
                    currbin = Pin.SliceCount;
                    curroffs = 0;
                }
                var res = new List<object>();

                for (int j = 0; j < currbin; j++)
                {
                    Pin.GetUpsreamSlice(curroffs + j, out var ui);
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
            Pin.GetUpstreamInterface(out object usi);
            return usi;
        }

        public GenericBinSizedInput(IPluginHost plgh, InputAttribute attr)
        {
            plgh.CreateNodeInput(attr.Name, (TSliceMode)attr.SliceMode, (TPinVisibility)attr.Visibility, out Pin);
            plgh.CreateValueInput(attr.Name + " Bin Size", 1, null, TSliceMode.Dynamic, (TPinVisibility) attr.Visibility, out BinSizePin);
            Pin.SetSubType2(null, new Guid[] { }, "Variant");
            BinSizePin.SetSubType(-1, double.MaxValue, 1, attr.BinSize, false, false, true);
            Pin.Order = attr.Order;
            BinSizePin.Order = attr.BinOrder;
        }
    }
}
