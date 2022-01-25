using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cobra.Common
{
    public static class COBRA_GLOBAL
    {
        public static class Constant
        {
            public const string OldBoardConfigName = "BoardConfig";
            public const string NewBoardConfigName = "Board Config";
            public const string OldEFUSEConfigName = "EfuseConfig";		//Issue1556 Leon
            public const string NewEFUSEConfigName = "EFUSE Config";
            public const string NewRegisterConfigName = "Register Config";
            public const string PRODUCT_FAMILY_NODE = "PRODUCT_FAMILY";
            public const string OCE_TOKEN_NODE = "OCE_TOKEN";
            public const string CHIP_NAME_NODE = "chip";
            public const string SETTINGS_FILE_NAME = "Setting.xml";
            public const string CONFIG_FILE_PATH_NODE = "ConfigFilePath";
        }

        public static string CurrentOCEName = String.Empty;	//Issue1606 Leon
        public static string CurrentOCEToken = String.Empty;    //Issue1741 Leon
        public static string CurrentDLLToken = String.Empty;    //Issue2843 Leon
        public static string CurrentParamToken = String.Empty;    //Issue2843 Leon
        public static string CurrentBoardToken = String.Empty;    //Issue2843 Leon
    }
}
