using System;
using System.Collections.Generic;
using System.Globalization;

namespace BrewLib.Util.LZMA.Common
{
    public enum SwitchType
    {
        Simple,
        PostMinus,
        LimitedPostString,
        UnLimitedPostString,
        PostChar
    }

    public class SwitchForm
    {
        public string IDString, PostCharSet;
        public SwitchType Type;
        public bool Multi;
        public int MinLen, MaxLen;

        public SwitchForm(string idString, SwitchType type, bool multi, int minLen, int maxLen, string postCharSet)
        {
            IDString = idString;
            Type = type;
            Multi = multi;
            MinLen = minLen;
            MaxLen = maxLen;
            PostCharSet = postCharSet;
        }

        public SwitchForm(string idString, SwitchType type, bool multi, int minLen) : this(idString, type, multi, minLen, 0, "") { }
        public SwitchForm(string idString, SwitchType type, bool multi) : this(idString, type, multi, 0) { }
    }

    public class SwitchResult
    {
        public bool ThereIs, WithMinus;
        public IList<string> PostStrings = new List<string>();
        public int PostCharIndex;

        public SwitchResult() => ThereIs = false;
    }

    public class Parser
    {
        public IList<string> NonSwitchStrings = new List<string>();
        readonly SwitchResult[] _switches;

        public Parser(int numSwitches)
        {
            _switches = new SwitchResult[numSwitches];
            for (var i = 0; i < numSwitches; ++i) _switches[i] = new SwitchResult();
        }

        bool ParseString(string srcString, SwitchForm[] switchForms)
        {
            var len = srcString.Length;
            if (len == 0) return false;

            var pos = 0;
            if (!IsItSwitchChar(srcString[pos])) return false;
            while (pos < len)
            {
                if (IsItSwitchChar(srcString[pos])) ++pos;
                const int kNoLen = -1;

                var matchedSwitchIndex = 0;
                var maxLen = kNoLen;
                for (var switchIndex = 0; switchIndex < _switches.Length; ++switchIndex)
                {
                    var switchLen = switchForms[switchIndex].IDString.Length;
                    if (switchLen <= maxLen || pos + switchLen > len) continue;
                    if (string.Compare(switchForms[switchIndex].IDString, 0, srcString, pos, switchLen, true, CultureInfo.InvariantCulture) == 0)
                    {
                        matchedSwitchIndex = switchIndex;
                        maxLen = switchLen;
                    }
                }
                if (maxLen == kNoLen) throw new ArgumentException("maxLen == kNoLen");

                var matchedSwitch = _switches[matchedSwitchIndex];
                var switchForm = switchForms[matchedSwitchIndex];
                if (!switchForm.Multi && matchedSwitch.ThereIs) throw new ArgumentException("switch must be single");

                matchedSwitch.ThereIs = true;
                pos += maxLen;
                int tailSize = len - pos;
                var type = switchForm.Type;

                switch (type)
                {
                    case SwitchType.PostMinus:
                        {
                            if (tailSize == 0) matchedSwitch.WithMinus = false;
                            else
                            {
                                matchedSwitch.WithMinus = srcString[pos] == kSwitchMinus;
                                if (matchedSwitch.WithMinus) ++pos;
                            }
                            break;
                        }
                    case SwitchType.PostChar:
                        {
                            if (tailSize < switchForm.MinLen) throw new ArgumentException("switch is not full");
                            var charSet = switchForm.PostCharSet;

                            const int kEmptyCharValue = -1;
                            if (tailSize == 0) matchedSwitch.PostCharIndex = kEmptyCharValue;
                            else
                            {
                                var index = charSet.IndexOf(srcString[pos]);
                                if (index < 0) matchedSwitch.PostCharIndex = kEmptyCharValue;
                                else
                                {
                                    matchedSwitch.PostCharIndex = index;
                                    ++pos;
                                }
                            }
                            break;
                        }
                    case SwitchType.LimitedPostString:
                    case SwitchType.UnLimitedPostString:
                        {
                            var minLen = switchForm.MinLen;
                            if (tailSize < minLen) throw new ArgumentException("switch is not full");
                            if (type is SwitchType.UnLimitedPostString)
                            {
                                matchedSwitch.PostStrings.Add(srcString.Substring(pos));
                                return true;
                            }
                            var stringSwitch = srcString.Substring(pos, minLen);

                            pos += minLen;
                            for (var i = minLen; i < switchForm.MaxLen && pos < len; ++i, ++pos)
                            {
                                var c = srcString[pos];
                                if (IsItSwitchChar(c)) break;
                                stringSwitch += c;
                            }
                            matchedSwitch.PostStrings.Add(stringSwitch);
                            break;
                        }
                }
            }
            return true;
        }

        public void ParseStrings(SwitchForm[] switchForms, string[] commandStrings)
        {
            var numCommandStrings = commandStrings.Length;
            var stopSwitch = false;

            for (var i = 0; i < numCommandStrings; ++i)
            {
                var s = commandStrings[i];
                if (stopSwitch) NonSwitchStrings.Add(s);
                else if (s == kStopSwitchParsing) stopSwitch = true;
                else if (!ParseString(s, switchForms)) NonSwitchStrings.Add(s);
            }
        }

        public SwitchResult this[int index] => _switches[index];

        public static int ParseCommand(CommandForm[] commandForms, string commandString, out string postString)
        {
            for (var i = 0; i < commandForms.Length; ++i)
            {
                var id = commandForms[i].IDString;
                if (commandForms[i].PostStringMode)
                {
                    if (commandString.IndexOf(id, StringComparison.Ordinal) == 0)
                    {
                        postString = commandString.Substring(id.Length);
                        return i;
                    }
                }
                else if (commandString == id)
                {
                    postString = "";
                    return i;
                }
            }
            postString = "";
            return -1;
        }

        const char kSwitchID1 = '-', kSwitchID2 = '/', kSwitchMinus = '-';
        const string kStopSwitchParsing = "--";

        static bool IsItSwitchChar(char c) => c == kSwitchID1 || c == kSwitchID2;
    }

    public class CommandForm
    {
        public string IDString = "";
        public bool PostStringMode;

        public CommandForm(string idString, bool postStringMode)
        {
            IDString = idString;
            PostStringMode = postStringMode;
        }
    }
}