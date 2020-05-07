using System;
using System.Collections.Generic;
using System.Text;

namespace storage_operator.fileshare
{
    public class Constants
    {
        public const string LabelSelectorKey = "fileshare";
        public const string LabelSelectorValue = "true";
        public static string LabelSelector => $"{LabelSelectorKey }={LabelSelectorValue}";
    }
}
