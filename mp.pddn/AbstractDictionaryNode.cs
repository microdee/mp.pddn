using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V2;

namespace mp.pddn
{
    public class AbstractDictionaryNode<TKey, TVal> : IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        #region fields & pins
        [Input("Dictionary In")]
        public Pin<Dictionary<TKey, TVal>> FDictIn;

        [Input("Default Keys")]
        public IDiffSpread<TKey> FKeys;
        [Input("Default Values")]
        public ISpread<TVal> FDef;
        [Input("Reset to Default", IsBang = true)]
        public ISpread<bool> FResetDefault;
        [Input("Update Key")]
        public ISpread<TKey> FMod;
        [Input("Update Value")]
        public ISpread<TVal> FVal;
        [Input("Update or Add", IsBang = true)]
        public ISpread<bool> FSet;
        [Input("Remove or Add", IsBang = true)]
        public ISpread<bool> FRemoveAdd;
        [Input("Remove Key")]
        public ISpread<TKey> FRemoveKey;
        [Input("Remove", IsBang = true)]
        public ISpread<bool> FRemove;
        [Input("Clear", IsBang = true)]
        public ISpread<bool> FClear;
        [Input("Get Value Of")]
        public ISpread<TKey> FGetKey;

        [Output("Dictionary Out")]
        public ISpread<Dictionary<TKey, TVal>> FDictOut;
        [Output("Keys Out")]
        public ISpread<TKey> FKeysOut;
        [Output("Values")]
        public ISpread<TVal> FOut;
        [Output("Queried Values")]
        public ISpread<ISpread<TVal>> FQueryOut;

        private Dictionary<TKey, TVal> dict = new Dictionary<TKey, TVal>();

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        public void OnImportsSatisfied()
        {
            FDictIn.Disconnected += (sender, args) => dict = new Dictionary<TKey, TVal>();
        }

        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            if (FDictIn.IsConnected)
            {
                dict = FDictIn[0];
            }
            if(FClear[0]) dict.Clear();
            if (FResetDefault[0])
            {
                dict.Clear();
                if (FDef.SliceCount != 0)
                {
                    for (int i = 0; i < FKeys.SliceCount; i++)
                    {
                        if (!dict.ContainsKey(FKeys[i]))
                            dict.Add(FKeys[i], FDef[i]);
                    }
                }
            }
            if (FSet.IsChanged)
            {
                if (FVal.SliceCount != 0 && FMod.SliceCount != 0)
                {
                    for (int i = 0; i < FMod.SliceCount; i++)
                    {
                        if(!FSet[i]) continue;

                        if (dict.ContainsKey(FMod[i]))
                            dict[FMod[i]] = FVal[i];
                        else dict.Add(FMod[i], FVal[i]);
                    }
                }
            }
            if (FRemoveAdd.IsChanged)
            {
                if (FVal.SliceCount != 0 && FMod.SliceCount != 0)
                {
                    for (int i = 0; i < FMod.SliceCount; i++)
                    {
                        if (!FRemoveAdd[i]) continue;

                        if (dict.ContainsKey(FMod[i]))
                            dict.Remove(FMod[i]);
                        else dict.Add(FMod[i], FVal[i]);
                    }
                }
            }

            if (FRemove.IsChanged)
            {
                if (FRemoveKey.SliceCount != 0)
                {
                    for (int i = 0; i < FRemoveKey.SliceCount; i++)
                    {
                        if (!FRemove[i]) continue;
                        if (dict.ContainsKey(FRemoveKey[i])) dict.Remove(FRemoveKey[i]);
                    }
                }
            }
            FOut.SliceCount = dict.Count;
            FKeysOut.SliceCount = dict.Count;
            int ii = 0;
            foreach (var kvp in dict)
            {
                FOut[ii] = kvp.Value;
                FKeysOut[ii] = kvp.Key;
                ii++;
            }

            FQueryOut.SliceCount = FGetKey.SliceCount;
            for (int i = 0; i < FGetKey.SliceCount; i++)
            {
                if (FGetKey[i] == null)
                {
                    FQueryOut[i].SliceCount = 0;
                    continue;
                }
                if (!dict.ContainsKey(FGetKey[i]))
                {
                    FQueryOut[i].SliceCount = 0;
                    continue;
                }

                FQueryOut[i].SliceCount = 1;
                FQueryOut[i][0] = dict[FGetKey[i]];
            }
            FDictOut[0] = dict;
        }

    }
}
