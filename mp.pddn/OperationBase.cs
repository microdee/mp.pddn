using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Fasterflect;
using md.stdl.Interfaces;
using VVVV.PluginInterfaces.V2;

namespace mp.pddn
{
    /// <summary>
    /// This would be an operation payload with the abstract Invoke function
    /// </summary>
    public abstract class OperationBase
    {
        /// <summary>
        /// This would be called by the operation sink node or through the <see cref="OperationHost{TOp}"/>
        /// </summary>
        /// <remarks>
        /// This is not necessarily the same as the operation actual payload function.
        /// </remarks>
        public abstract void Invoke();
    }
    
    /// <summary>
    /// Wraps multiple operations of a node into a host object
    /// </summary>
    /// <typeparam name="TOp"></typeparam>
    public class OperationHost<TOp>
        where TOp : OperationBase
    {
        private ConstructorInvoker _ctorDlg;
        /// <summary>
        /// Sink nodes can operate on spread of spreads
        /// </summary>
        public Spread<Spread<TOp>> Operations { get; set; } = new Spread<Spread<TOp>>(0);

        /// <summary>
        /// Set bincount of Operations
        /// </summary>
        /// <param name="i"></param>
        public void SetBinCount(int i)
        {
            Operations.ResizeAndDismiss(i, () => new Spread<TOp>());
        }

        /// <summary>
        /// Other connected operation host preferably coming from upstream nodes
        /// </summary>
        public OperationHost<TOp> Child { get; set; }

        /// <summary>
        /// Invoke an operation at given indices
        /// </summary>
        /// <param name="i">Spread index</param>
        /// <param name="j">Operation index</param>
        /// <param name="setter">An optional setter method to set Operation fields</param>
        public void Invoke(int i, int j, Action<int, int, TOp> setter = null)
        {
            var op = Operations.TryGetSlice(i).TryGetSlice(j);
            setter?.Invoke(i, j, op);
            op?.Invoke();
        }

        /// <summary>
        /// Invoke all operations in a spread 
        /// </summary>
        /// <param name="i">Spread index</param>
        /// <param name="setter">An optional setter method to set Operation fields</param>
        public void Invoke(int i, Action<int, int, TOp> setter = null)
        {
            if(Operations.SliceCount <= 0) return;
            for (int j = 0; j < Operations[i].SliceCount; j++)
                Invoke(i, j, setter);
        }

        /// <summary>
        /// Invoke an operation recursively at given indices
        /// </summary>
        /// <param name="i">Spread index</param>
        /// <param name="j">Operation index</param>
        /// <param name="setter">An optional setter method to set Operation fields</param>
        public void InvokeRecursive(int i, int j, Action<int, int, TOp> setter = null)
        {
            Child?.InvokeRecursive(i, j, setter);
            Invoke(i, j, setter);
        }

        /// <summary>
        /// Invoke all operations in a spread recursively
        /// </summary>
        /// <param name="i">Spread index</param>
        /// <param name="setter">An optional setter method to set Operation fields</param>
        public void InvokeRecursive(int i, Action<int, int, TOp> setter = null)
        {
            Child?.InvokeRecursive(i, setter);
            Invoke(i, setter);
        }

        /// <summary>
        /// Create a new empty instance of the same type of OperationHost as this one
        /// </summary>
        /// <returns></returns>
        public OperationHost<TOp> CreateNew()
        {
            if (_ctorDlg == null)
                _ctorDlg = GetType().DelegateForCreateInstance();
            var res = _ctorDlg.Invoke();
            if (res is OperationHost<TOp> ophost) return ophost;
            return null;
        }
    }
    /// <summary></summary>
    public static class OperationExtensions
    {
        /// <summary>
        /// Recursively extract the first bin of an operation host into another operation host
        /// </summary>
        /// <typeparam name="TOp">Type of operation</typeparam>
        /// <param name="host"></param>
        /// <param name="target"></param>
        /// <param name="i"></param>
        public static void ExtractOperationHost<TOp>(this OperationHost<TOp> host, OperationHost<TOp> target, int i)
            where TOp : OperationBase
        {
            if (host == null || target == null) return;

            if (host.Child != null)
            {
                if(target.Child == null)
                    target.Child = host.Child.CreateNew();
                host.Child.ExtractOperationHost(target.Child, i);
            }
            
            if (host.Operations.SliceCount == 0)
            {
                target.Operations.SliceCount = 0;
            }
            else
            {
                target.SetBinCount(1);
                target.Operations[0].AssignFrom(host.Operations[i]);
            }
        }
    }
}
