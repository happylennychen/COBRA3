using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace O2Micro.Cobra.Common
{
    public static class CobraGlobal
    {
        public static class Constant
        {
            public const string OldBoardConfigName = "BoardConfig";
            public const string NewBoardConfigName = "Board Config";
            public const string OldEFUSEConfigName = "EfuseConfig";		//Issue1556 Leon
            public const string NewEFUSEConfigName = "EFUSE Config";
        }

        public static string CurrentOCEName = String.Empty;
    }
}
