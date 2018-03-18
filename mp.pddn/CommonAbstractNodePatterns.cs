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
using VVVV.Utils.Reflection;

namespace mp.pddn
{
    public static class ObjectHelper
    {
        public static void DisposeDisposable(object obj)
        {
            if (!(obj is IDisposable)) return;
            var t = (IDisposable) obj;
            t.Dispose();
        }

        public static Type ForceGetType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(typeName);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
    /// <summary>
    /// Constructor node where output is persistent until another construct
    /// </summary>
    /// <typeparam name="T">Type of desired object</typeparam>
    public abstract class ConstructorNode<T> : IPluginEvaluate
    {
        [Input("Construct", IsBang = true, Order = 0)]
        public ISpread<bool> FConstruct;
        [Input("Auto Clear", DefaultBoolean = true, Order = 1)]
        public ISpread<bool> FAutoClear;
        [Output("Output Object", Order = 0)]
        public ISpread<T> FOutput;
        
        public int CurrObj;
        public int SliceCount = 0;
        public int fc = 0;
        public virtual void SetSliceCount(int SpreadMax)
        {
            this.SliceCount = SpreadMax;
        }
        public virtual void InitializeFrame()
        { }

        public abstract T ConstructObject();

        public void Evaluate(int SpreadMax)
        {
            this.SetSliceCount(SpreadMax);
            this.InitializeFrame();

            if (FAutoClear[0])
            {
                bool clear = false;
                for (int i = 0; i < this.SliceCount; i++)
                {
                    if (FConstruct[i]) clear = true;
                }
                if (clear) fc = 0;
            }
            if (fc == 0) FOutput.SliceCount = 0;
            fc++;

            for (int i = 0; i < this.SliceCount; i++)
            {
                this.CurrObj = i;
                if (FConstruct[i])
                {
                    var ro = ConstructObject();
                    if (ro != null) FOutput.Add(ro);
                }
            }
        }
    }
    /// <summary>
    /// Constructor node where output is persistent until another construct, it can also update object with incoming data
    /// </summary>
    /// <typeparam name="T">Type of desired object</typeparam>
    public abstract class ConstructAndSetNode<T> : IPluginEvaluate
    {
        [Input("Construct", IsBang = true, Order = 0)]
        public ISpread<bool> FConstruct;
        [Input("Auto Clear", DefaultBoolean = true, Order = 1)]
        public ISpread<bool> FAutoClear;
        [Input("Set", IsBang = true, Order = 2)]
        public ISpread<bool> FSet;
        [Input("Dispose Disposable", Order = 3, Visibility = PinVisibility.OnlyInspector)]
        public ISpread<bool> FDisposeDisposable;
        [Output("Output Object", Order = 0)]
        public ISpread<T> FOutput;
        
        public int CurrObj;
        public int SliceCount = 0;
        public int fc = 0;
        public virtual void SetSliceCount(int SpreadMax)
        {
            SliceCount = SpreadMax;
        }
        public virtual void InitializeFrame() { }

        public abstract T ConstructObject();

        public virtual void SetObject() { }

        public void Evaluate(int SpreadMax)
        {
            this.SetSliceCount(SpreadMax);
            this.InitializeFrame();

            if (FAutoClear[0])
            {
                bool clear = false;
                for (int i = 0; i < this.SliceCount; i++)
                {
                    if (FConstruct[i]) clear = true;
                }
                if (clear) fc = 0;
            }
            if (fc == 0)
            {
                for (int i = 0; i < FOutput.SliceCount; i++)
                {
                    if ((FOutput[i] != null) && FDisposeDisposable[0])
                        ObjectHelper.DisposeDisposable(FOutput[i]);
                }
                FOutput.SliceCount = 0;
            }
            fc++;

            bool empty = FOutput.SliceCount == 0;
            for (int i = 0; i < this.SliceCount; i++)
            {
                this.CurrObj = i;
                if (FConstruct[i] || (FSet[i] && empty))
                {
                    var ro = ConstructObject();
                    if (ro != null) FOutput.Add(ro);
                }
            }
            for (int i = 0; i < FOutput.SliceCount; i++)
            {
                if (!FSet[i]) continue;
                CurrObj = i;
                SetObject();
            }
        }
    }
}
