using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;
using NGISpread = VVVV.PluginInterfaces.V2.NonGeneric.ISpread;
using NGIDiffSpread = VVVV.PluginInterfaces.V2.NonGeneric.IDiffSpread;

namespace VVVV.Nodes.PDDN
{
    public static class NodeExtensions
    {
        /// <summary>
        /// Convenience function to set the slicecount of all output pins at once defined in the plugin class
        /// </summary>
        /// <param name="node">Current node</param>
        /// <param name="sc">Slicecount</param>
        /// <param name="ignore">Ignore pins via their Member names (NOT pin names!)</param>
        /// <param name="pinSet">Optional hashset to save the list of spreads to</param>
        public static void SetSliceCountForAllOutput(this IPluginEvaluate node, int sc, string[] ignore = null, HashSet<NGISpread> pinSet = null)
        {
            foreach (var field in node.GetType().GetFields())
            {
                if(ignore != null)
                    if (ignore.Contains(field.Name)) continue;
                if (field.GetCustomAttributes(typeof(OutputAttribute), false).Length == 0) continue;
                var spread = (NGISpread)field.GetValue(node);
                spread.SliceCount = sc;
                if (pinSet == null) continue;
                if (!pinSet.Contains(spread)) pinSet.Add(spread);
            }
        }

        /// <summary>
        /// Convenience function to get all the output pins defined in the plugin class
        /// </summary>
        /// <param name="node">Current node</param>
        /// <param name="pinSet">Hashset to save the list of spreads to</param>
        /// <param name="ignore">Ignore pins via their Member names (NOT pin names!)</param>
        public static void GetAllOutputSpreads(this IPluginEvaluate node, HashSet<NGISpread> pinSet, string[] ignore = null)
        {
            foreach (var field in node.GetType().GetFields())
            {
                if (ignore != null)
                    if (ignore.Contains(field.Name)) continue;
                if (field.GetCustomAttributes(typeof(OutputAttribute), false).Length == 0) continue;
                var spread = (NGISpread)field.GetValue(node);
                if (!pinSet.Contains(spread)) pinSet.Add(spread);
            }
        }

        /// <summary>
        /// Converts IIOcontainer to an actual spread
        /// </summary>
        /// <param name="pin"></param>
        /// <returns>the spread</returns>
        public static NGISpread ToISpread(this IIOContainer pin)
        {
            return (NGISpread)(pin.RawIOObject);
        }

        /// <summary>
        /// Converts IIOcontainer to an actual Diffspread
        /// </summary>
        /// <param name="pin"></param>
        /// <returns>the spread</returns>
        public static NGIDiffSpread ToIDiffSpread(this IIOContainer pin)
        {
            return (NGIDiffSpread)(pin.RawIOObject);
        }

        /// <summary>
        /// Converts IIOcontainer to an actual generic spread
        /// </summary>
        /// <param name="pin"></param>
        /// <returns>the spread</returns>
        public static ISpread<T> ToGenericISpread<T>(this IIOContainer pin)
        {
            return (ISpread<T>)(pin.RawIOObject);
        }
    }
}
