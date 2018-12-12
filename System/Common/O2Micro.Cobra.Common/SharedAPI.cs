using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace O2Micro.Cobra.Common
{
    public class SharedAPI
    {
        #region 常量定义
        internal static UInt16 order = 0;
        #endregion

        public static void ReBuildBusOptions(ref BusOptions busOptions, ref ParamListContainer ParamlistContainer)
        {
            order = 0;
            ParamContainer pmrcntTemp = null;

            foreach (Parameter param in ParamlistContainer.GetParameterListByGuid(BusOptions.BusOptionsElement).parameterlist)
            {
                if (param == null) continue;
                InitSFLParameter(ref busOptions,param);
            }

            //todo: update busOptions with database record
            if (DBManager.supportdb == true)
            {
                LoadBusOptionsFromDB(ref busOptions);
            }

            pmrcntTemp = ParamlistContainer.GetParameterListByGuid(AutomationElement.GUIDAutomationTestSeciton);
            if (pmrcntTemp != null)
            {
                foreach (Parameter parameach in pmrcntTemp.parameterlist)
                {
                    if (parameach == null) continue;
                    InitAutomationElement(ref busOptions, parameach);
                }
            }
        }

        private static void LoadBusOptionsFromDB(ref BusOptions busOptions)
        {
            Dictionary<string, string> options = new Dictionary<string, string>();
            Int32 ret = DBManager.LoadBusOptions(busOptions.BusType, busOptions.DeviceIndex, ref options);
            if (ret == 0)
            {
                switch (busOptions.BusType)
                {
                    case BUS_TYPE.BUS_TYPE_I2C:
                        //busOptions.GetOptionsByGuid(BusOptions.I2CFrequency_GUID).sphydata = "135";
                        foreach (var option in options)
                        {
                            switch (option.Key)
                            {
                                case "frequency":
                                    busOptions.GetOptionsByGuid(BusOptions.I2CFrequency_GUID).sphydata = option.Value;
                                    break;
                                case "address":
                                    busOptions.GetOptionsByGuid(BusOptions.I2CAddress_GUID).sphydata = option.Value;
                                    break;
                                case "pec_enable":
                                    busOptions.GetOptionsByGuid(BusOptions.I2CPECMODE_GUID).sphydata = option.Value;
                                    break;
                            }
                        }
                        break;
                }
            }
            else
            {
                //System.Windows.MessageBox.Show("Load Bus Options Failed!");
            }
        }

        public static void SaveBusOptionsToDB(BusOptions busOptions)
        {
            Dictionary<string, string> options = new Dictionary<string, string>();
            switch (busOptions.BusType)
            {
                case BUS_TYPE.BUS_TYPE_I2C:
                    //busOptions.GetOptionsByGuid(BusOptions.I2CFrequency_GUID).sphydata = "135";
                    string frequency = "", address = "", pec_enable = "";
                    foreach(var option in busOptions.optionsList)
                    {
                        switch(option.guid)
                        {
                            case BusOptions.I2CFrequency_GUID:
                                frequency = option.sphydata;
                                break;
                            case BusOptions.I2CAddress_GUID:
                                address = option.sphydata;
                                break;
                            case BusOptions.I2CPECMODE_GUID:
                                pec_enable = option.sphydata;
                                break;
                        }
                    }
                    options.Add("frequency", frequency);
                    options.Add("address", address);
                    options.Add("pec_enable", pec_enable);
                    break;
            }
            int ret = DBManager.SaveBusOptions(busOptions.BusType, busOptions.DeviceIndex, options);
            if (ret != 0)
                System.Windows.MessageBox.Show("Save Bus Options Failed!");
        }

        private static void InitSFLParameter(ref BusOptions busOptions, Parameter param)
        {
            UInt16 index = 0;
            UInt16 udata = 0;
            Double ddata = 0.0;
            bool bdata = false;
            Options model = new Options();

            model.guid = param.guid;
            model.bedit = true;
            model.berror = false;
            model.brange = true;
            model.sdevicename = busOptions.DeviceName;

            foreach (DictionaryEntry de in param.sfllist["BusOptions"].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "NickName":
                        model.nickname = de.Value.ToString();
                        break;
                    case "Order":
                        {
                            model.order = order;
                            order++;
                            break;
                        }
                    case "EditType":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.editortype = 0;
                            else
                                model.editortype = udata;
                            break;
                        }
                    case "Format":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.format = 0;
                            else
                                model.format = udata;
                            break;
                        }
                    case "MinValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.minvalue = 0.0;
                            else
                                model.minvalue = Convert.ToDouble(de.Value.ToString());
                            break;
                        }
                    case "MaxValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.maxvalue = 0.0;
                            else
                                model.maxvalue = Convert.ToDouble(de.Value.ToString());
                            break;
                        }
                    case "Catalog":
                        model.catalog = de.Value.ToString();
                        break;
                    case "DefValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.data = 0.0;
                            else
                                model.data = Convert.ToDouble(de.Value.ToString());
                            break;
                        }
                    case "BRange":
                        {
                            if (!Boolean.TryParse(de.Value.ToString(), out bdata))
                                model.brange = true;
                            else
                                model.brange = bdata;
                            break;
                        }
                    default:
                        break;
                }
            }

            switch ((UI_TYPE)model.editortype)
            {
                case UI_TYPE.TextBox_Type:
                    model.sphydata = string.Format("{0:F0}", param.phydata);
                    break;
                case UI_TYPE.CheckBox_Type:
                    model.sphydata = (param.phydata > 0)?"1":"0";
                    break;
                case UI_TYPE.ComboBox_Type:
                    {
                        index = 0;
                        if (model.guid == BusOptions.ConnectPort_GUID) break;
                        foreach (string str in param.itemlist)
                        {
                            ComboboxRoad cRoad = new ComboboxRoad();
                            cRoad.ID = index;
                            cRoad.Info = str;
                            try
                            {
                                if ((str.ToLower().IndexOf("true") != -1) || (str.ToLower().IndexOf("false") != -1))
                                {
                                    cRoad.Code = (UInt16)((Convert.ToBoolean(cRoad.Info) == true) ? 1 : 0);
                                }
                                else
                                {
                                    cRoad.Code = Convert.ToUInt16(cRoad.Info, 16);
                                }
                            }
                            catch
                            {
                                cRoad.Code = 0;
                            }
                            model.LocationSource.Add(cRoad);
                            index++;
                        }
                        if (model.LocationSource.Count != 0)
                        {
                            UInt16 inx = (UInt16)model.data;
                            if ((inx > model.maxvalue) || (inx < model.minvalue)) inx = 0;
                            model.SelectLocation = model.LocationSource[inx];
                            model.sphydata = model.SelectLocation.Info;
                        }
                    }
                    break;
                default:
                    break;
            }
            if ((model.data > model.maxvalue) || (model.data < model.minvalue))
                model.berror = true;
            else
                model.berror = false;
            busOptions.optionsList.Add(model);
        }

        private static void InitAutomationElement(ref BusOptions busOptions, Parameter param)
        {
            UInt16 iData = 0;
            Double dbData = 0;
            AutomationElement atmelModel = new AutomationElement();

            atmelModel.pmrParent = param;
            atmelModel.u32Guid = param.guid;

            foreach (DictionaryEntry de in param.sfllist["ATTestSelection"].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "Order":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out iData))
                            {
                                atmelModel.u16Order = 0;
                            }
                            else
                            {
                                atmelModel.u16Order = iData;
                            }
                            break;
                        }
                    case "NickName":
                        atmelModel.strNickname = de.Value.ToString();
                        break;
                    case "Catalog":
                        atmelModel.strCatalog = de.Value.ToString();
                        break;
                    case "DefValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out dbData))
                            {
                                atmelModel.dbValue = 0.0;
                            }
                            else
                            {
                                atmelModel.dbValue = Convert.ToDouble(de.Value.ToString());
                            }
                            break;
                        }
                    case "EditType":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out iData))
                            {
                                atmelModel.u16EditType = 0;
                            }
                            else
                            {
                                atmelModel.u16EditType = iData;
                            }
                            break;
                        }
                    case "Format":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out iData))
                            {
                                atmelModel.u16Format = 0;
                            }
                            else
                            {
                                atmelModel.u16Format = iData;
                            }
                            break;
                        }
                    case "MinValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out dbData))
                            {
                                atmelModel.dbMinvalue = 0.0;
                            }
                            else
                            {
                                atmelModel.dbMinvalue = Convert.ToDouble(de.Value.ToString());
                            }
                            break;
                        }
                    case "MaxValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out dbData))
                            {
                                atmelModel.dbMaxvalue = 0.0;
                            }
                            else
                            {
                                atmelModel.dbMaxvalue = Convert.ToDouble(de.Value.ToString());
                            }
                            break;
                        }
                    default:
                        break;
                }
            }

            atmelModel.SynchdbValueToDisplay(param);

            busOptions.AtMationSettingList.Add(atmelModel);
        }

        public static UInt32 LoadAXIS_1File(List<string> strlist, string fullpath, Int32[] databuffer, UInt16 length)
        {
            // We have fixed 6 data+ // as comment
            // Before that we skip all comment first. May save it in the future
            string line, tmp;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            if (!File.Exists(fullpath))
                return LibErrorCode.IDS_ERR_SECTION_PROJECT_CONTENT_FILE_NOTEXIST; //should return error

            int arrayindex = 0;
            using (StreamReader sr = new StreamReader(@fullpath))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    strlist.Add(line);
                    //UInt16 udata = Convert.ToUInt16(line);
                    // string tmp = line.Trim('/');
                    var chars = line.ToCharArray();
                    if (chars.Length < 2)
                        continue;
                    if (chars[0].Equals('/') && chars[1] == '/')
                        continue;

                    string[] s_step1 = line.Split('/');//only s[0] is data what we want.
                    var chars_step1 = s_step1[0].ToCharArray();
                    char[] newSarray_step1 = new char[chars_step1.Length];
                    int i = 0;
                    // skip \t ''
                    foreach (char c in chars_step1)
                    {
                        if (c.Equals('\t'))
                            continue;
                        if (c.Equals(' '))
                            continue;

                        newSarray_step1[i] = c;
                        i++;
                    }
                    newSarray_step1[i] = '\0';

                    string sfinal_step2 = new string(newSarray_step1);
                    string[] s_final_array = sfinal_step2.Split(',');//only s[0] is data what we want.
                    Int32 numValue = 4000;
                    //     arrayindex = 0;
                    foreach (string sstmp in s_final_array)
                    {
                        if (Int32.TryParse(sstmp, out numValue))
                        {
                            databuffer[arrayindex] = numValue;
                            arrayindex++;
                            /*
                            Byte[] bytearray = BitConverter.GetBytes(numValue);
                            databuffer[arrayindex] = bytearray[0];
                            databuffer[arrayindex + 1] = bytearray[1];
                            arrayindex += 2;
                             * */
                        }
                    }
                    //  string[] words = line.Split("\\");
                }
            }
            return ret;
        }

        public static UInt32 LoadAXIS_1File(string fullpath, Byte[] databuffer, UInt16 length)
        {
            // We have fixed 6 data+ // as comment
            // Before that we skip all comment first. May save it in the future
            string line, tmp;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            if (!File.Exists(fullpath))
                return LibErrorCode.IDS_ERR_SECTION_PROJECT_CONTENT_FILE_NOTEXIST; //should return error

            int arrayindex = 0;
            using (StreamReader sr = new StreamReader(@fullpath))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    //UInt16 udata = Convert.ToUInt16(line);
                    // string tmp = line.Trim('/');
                    var chars = line.ToCharArray();
                    if (chars.Length < 2)
                        continue;
                    if (chars[0].Equals('/') && chars[1] == '/')
                        continue;

                    string[] s_step1 = line.Split('/');//only s[0] is data what we want.
                    var chars_step1 = s_step1[0].ToCharArray();
                    char[] newSarray_step1 = new char[chars_step1.Length];
                    int i = 0;
                    // skip \t ''
                    foreach (char c in chars_step1)
                    {
                        if (c.Equals('\t'))
                            continue;
                        if (c.Equals(' '))
                            continue;

                        newSarray_step1[i] = c;
                        i++;
                    }
                    newSarray_step1[i] = '\0';

                    string sfinal_step2 = new string(newSarray_step1);
                    string[] s_final_array = sfinal_step2.Split(',');//only s[0] is data what we want.
                    Int32 numValue = 4000;
                    foreach (string sstmp in s_final_array)
                    {
                        if (Int32.TryParse(sstmp, out numValue))
                        {
                            Byte[] bytearray = BitConverter.GetBytes(numValue);
                            databuffer[arrayindex] = bytearray[0];
                            databuffer[arrayindex + 1] = bytearray[1];
                            arrayindex += 2;
                        }
                    }
                    //  string[] words = line.Split("\\");
                }
            }
            return ret;
        }

        public static UInt32 LoadIntelHexFile(string fullpath, Byte[] firmwarebuffer, UInt16 datalength)
        {
            int pos;
            //double dval = 0.0;
            string line, tmp;
            //UInt32 selfid;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            Byte length = 0, type = 0, checksum = 0, btmp = 0;
            UInt16 uaddress = 0;
            Byte[] databuffer;//, firmwarebuffer;
            databuffer = new Byte[32];
            if (!File.Exists(fullpath))
                return LibErrorCode.IDS_ERR_SECTION_PROJECT_CONTENT_FILE_NOTEXIST_FIRMWARE; //should return error
            // firmwarebuffer = new Byte[0x8000];
            // char[] bin;

            // Clear firmware buffer first.
            for (uaddress = 0; uaddress < datalength; uaddress++)
            {
                firmwarebuffer[uaddress] = 0;
            }

            try
            {
                // Create an instance of StreamReader to read from a file.
                // The using statement also closes the StreamReader.
                using (StreamReader sr = new StreamReader(@fullpath))
                {
                    // Read and display lines from the file until the end of 
                    // the file is reached.
                    while ((line = sr.ReadLine()) != null)
                    {
                        checksum = 0;

                        // First char should be ":"
                        pos = line.IndexOf(':');
                        if (pos == -1) continue;
                        //remove it
                        line = line.Remove(0, 1);
                        // then, next 2 char are length
                        tmp = line.Substring(0, 2);
                        //length = Convert.ToUInt32(tmp, 16);
                        length = Convert.ToByte(tmp, 16);
                        checksum += length;
                        line = line.Remove(0, 2);
                        // Tehn, next 4 char are address offset
                        tmp = line.Substring(0, 4);
                        uaddress = Convert.ToUInt16(tmp, 16);

                        checksum += (Byte)uaddress;
                        checksum += (Byte)(uaddress >> 8);

                        line = line.Remove(0, 4);
                        // then, next 1 char are type "00" means data, "01" means end of file
                        tmp = line.Substring(0, 2);
                        type = Convert.ToByte(tmp, 16);
                        checksum += type;
                        line = line.Remove(0, 2);
                        if (type != 0)
                            continue;
                        // The data according to length. up to 16 (dec)
                        // line in here should be have only data with last check sum.
                        for (int i = 0; i < length; i++)
                        {
                            tmp = line.Substring(0, 2);
                            btmp = Convert.ToByte(tmp, 16);
                            checksum += btmp;
                            databuffer[i] = btmp;
                            line = line.Remove(0, 2);
                        }
                        // the last 1 char is checksum.
                        tmp = line.Substring(0, 2);
                        btmp = Convert.ToByte(tmp, 16);
                        checksum += btmp;
                        // Do checksum calculation for hex file in each line.
                        //  byte checksum = 0;
                        if (checksum == 0)
                        {
                            for (btmp = 0; btmp < length; btmp++)
                            {
                                //databuffer = new Byte[32];
                                firmwarebuffer[uaddress + btmp] = databuffer[btmp];
                            }
                        }
                        else
                        {
                            return LibErrorCode.IDS_ERR_SECTION_PROJECT_CONTENT_FILE_FIRMWARE_CHECKSUM_ERROR;
                        }




                    }
                }
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
            return ret;

        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr _lopen(string lpPathName, int iReadWrite);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int OF_READWRITE = 2;

        private const int OF_SHARE_DENY_NONE = 0x40;

        private static readonly IntPtr HFILE_ERROR = new IntPtr(-1);

        public static UInt32 FileIsOpen(string fileFullName)
        {
            if (!File.Exists(fileFullName))
            {
                return LibErrorCode.IDS_ERR_SECTION_SIMULATION_FILE_LOST;
            }
            IntPtr handle = _lopen(fileFullName, OF_READWRITE | OF_SHARE_DENY_NONE);
            if (handle == HFILE_ERROR)
            {
                return LibErrorCode.IDS_ERR_SECTION_SIMULATION_FILE_OPENED;
            }
            CloseHandle(handle);
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }

        /// <summary>
        /// 将字典类型序列化为json字符串
        /// </summary>
        /// <typeparam name="TKey">字典key</typeparam>
        /// <typeparam name="TValue">字典value</typeparam>
        /// <param name="dict">要序列化的字典数据</param>
        /// <returns>json字符串</returns>
        public static string SerializeDictionaryToJsonString<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            if (dict.Count == 0)
                return "";

            string jsonStr = JsonConvert.SerializeObject(dict);
            return jsonStr;
        }

        /// <summary>
        /// 将json字符串反序列化为字典类型
        /// </summary>
        /// <typeparam name="TKey">字典key</typeparam>
        /// <typeparam name="TValue">字典value</typeparam>
        /// <param name="jsonStr">json字符串</param>
        /// <returns>字典数据</returns>
        public static Dictionary<TKey, TValue> DeserializeStringToDictionary<TKey, TValue>(string jsonStr)
        {
            if (string.IsNullOrEmpty(jsonStr))
                return new Dictionary<TKey, TValue>();

            Dictionary<TKey, TValue> jsonDict = JsonConvert.DeserializeObject<Dictionary<TKey, TValue>>(jsonStr);

            return jsonDict;

        }
    }
}
