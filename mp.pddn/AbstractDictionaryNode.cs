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
    /// <summary>
    /// Static class for following object changes
    /// </summary>
    public static class ObjectChange
    {
        /// <summary>
        /// Object change counters based on hashcode of object
        /// </summary>
        private static Dictionary<int, int> ChangeCounters { get; } = new Dictionary<int, int>();

        public static int NotifyChange(this object obj)
        {
            var hc = obj.GetHashCode();
            if (ChangeCounters.ContainsKey(hc))
                ChangeCounters[hc]++;
            else ChangeCounters.Add(hc, 1);
            return ChangeCounters[hc];
        }

        public static bool CheckChanged(this object obj, ref int refchangecount)
        {
            var hc = obj.GetHashCode();
            if (ChangeCounters.ContainsKey(hc))
            {
                var res = ChangeCounters[hc] != refchangecount;
                refchangecount = ChangeCounters[hc];
                return res;
            }
            else
            {
                refchangecount = -1;
                return false;
            }
        }
    }

    public class AbstractDictionaryNode<TKey, TVal> : IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        #region fields & pins

        [Input("Dictionary In")]
        public Pin<IDictionary<TKey, TVal>> FDictIn;

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
        public Pin<TKey> FGetKey;

        [Output("Dictionary Out")]
        public ISpread<IDictionary<TKey, TVal>> FDictOut;
        [Output("Keys")]
        public ISpread<TKey> FKeysOut;
        [Output("Values")]
        public ISpread<ISpread<TVal>> FOut;

        [Output(
            "Changed",
            IsBang = true,
            Visibility = PinVisibility.Hidden
        )]
        public ISpread<bool> FChanged;

        private IDictionary<TKey, TVal> _dict = new Dictionary<TKey, TVal>();
        private int _dictChangeCounter = -1;

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        public void OnImportsSatisfied()
        {
            FDictIn.Disconnected += (sender, args) => _dict = new Dictionary<TKey, TVal>();
        }

        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            if (FDictIn.IsConnected)
            {
                _dict = FDictIn[0];
            }
            if(FClear[0])
            {
                _dict.Clear();
                _dict.NotifyChange();
            }
            if (FResetDefault[0])
            {
                _dict.Clear();
                if (FDef.SliceCount != 0)
                {
                    for (int i = 0; i < FKeys.SliceCount; i++)
                    {
                        if (!_dict.ContainsKey(FKeys[i]))
                            _dict.Add(FKeys[i], FDef[i]);
                    }
                }
                _dict.NotifyChange();
            }
            if (FSet.IsChanged)
            {
                if (FVal.SliceCount != 0 && FMod.SliceCount != 0)
                {
                    for (int i = 0; i < FMod.SliceCount; i++)
                    {
                        if(!FSet[i]) continue;

                        if (_dict.ContainsKey(FMod[i]))
                            _dict[FMod[i]] = FVal[i];
                        else _dict.Add(FMod[i], FVal[i]);
                        _dict.NotifyChange();
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

                        if (_dict.ContainsKey(FMod[i]))
                            _dict.Remove(FMod[i]);
                        else _dict.Add(FMod[i], FVal[i]);
                        _dict.NotifyChange();
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
                        if (_dict.ContainsKey(FRemoveKey[i])) _dict.Remove(FRemoveKey[i]);
                        _dict.NotifyChange();
                    }
                }
            }

            if (FDictIn.IsChanged && FDictIn.SliceCount > 0)
                _dict.NotifyChange();

            var changed = _dict.CheckChanged(ref _dictChangeCounter);

            if (FGetKey.IsConnected)
            {
                if(changed || FGetKey.IsChanged)
                {
                    FOut.SliceCount = FKeysOut.SliceCount = FGetKey.SliceCount;
                    for (int i = 0; i < FGetKey.SliceCount; i++)
                    {
                        if (FGetKey[i] == null)
                        {
                            FOut[i].SliceCount = 0;
                            continue;
                        }

                        if (!_dict.TryGetValue(FGetKey[i], out var res))
                        {
                            FOut[i].SliceCount = 0;
                            continue;
                        }

                        FKeysOut[i] = FGetKey[i];
                        FOut[i].SliceCount = 1;
                        FOut[i][0] = res;
                    }
                }
            }
            else
            {
                if(changed)
                {
                    FOut.SliceCount = _dict.Count;
                    FKeysOut.SliceCount = _dict.Count;
                    int ii = 0;
                    foreach (var kvp in _dict)
                    {
                        FOut[ii].SliceCount = 1;
                        FOut[ii][0] = kvp.Value;
                        FKeysOut[ii] = kvp.Key;
                        ii++;
                    }
                }
            }

            FChanged[0] = changed;
            FDictOut.Stream.IsChanged = false;
            if (FDictOut[0]?.GetHashCode() != _dict?.GetHashCode())
            {
                FDictOut.Stream.IsChanged = true;
                FDictOut[0] = _dict;
            }
        }

    }
}
