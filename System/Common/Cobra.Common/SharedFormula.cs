using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace Cobra.Common
{
    public class SharedFormula
    {
        public static UInt16 MAKEWORD(byte LoByte, byte HiByte)
        {
            return (ushort)(LoByte + (HiByte << 8));
        }

        public static uint MAKEDWORD(UInt16 LoWord, UInt16 HiWord)
        {
            return (uint)(LoWord + (HiWord << 16));
        }

        /// <summary>
        /// The return value is the high-order double word of the specified value.
        /// </summary>
        /// <param name="pDWord"></param>
        /// <returns></returns>
        public static int HiDword(long pDWord)
        {
            return ((int)(((pDWord) >> 32) & 0xFFFFFFFF));
        }

        /// <summary>
        /// The return value is the low-order word of the specified value.
        /// </summary>
        /// <param name="pDWord">The value</param>
        /// <returns></returns>
        public static int LoDword(long pDWord)
        {
            return ((int)pDWord);
        }

        /// <summary>
        /// The return value is the high-order word of the specified value.
        /// </summary>
        /// <param name="pDWord"></param>
        /// <returns></returns>
        public static short HiWord(int pDWord)
        {
            return ((short)(((pDWord) >> 16) & 0xFFFF));
        }

        /// <summary>
        /// The return value is the low-order word of the specified value.
        /// </summary>
        /// <param name="pDWord">The value</param>
        /// <returns></returns>
        public static short LoWord(int pDWord)
        {
            return ((short)pDWord);
        }

        /// <summary>
        /// The return value is the high-order byte of the specified value.
        /// </summary>
        /// <param name="pWord">The value</param>
        /// <returns></returns>
        public static byte HiByte(UInt16 pWord)
        {
            return ((byte)(((UInt16)(pWord) >> 8) & 0xFF));
        }

        /// <summary>
        /// The return value is the low-order byte of the specified value.
        /// </summary>
        /// <param name="pWord">The value</param>
        /// <returns></returns>
        public static byte LoByte(UInt16 pWord)
        {
            return ((byte)pWord);
        }

        public static string ToHexString(byte[] bytes) // 0xae00cf => "AE00CF "
        {
            string hexString = string.Empty;
            if (bytes != null)
            {
                StringBuilder strB = new StringBuilder();
                strB.Append("0x");
                for (int i = 0; i < bytes.Length; i++)
                    strB.Append(bytes[i].ToString("X2"));
                hexString = strB.ToString();

            } return hexString;

        }

        public static int FindProximalValFromList(AsyncObservableCollection<string> list, double dval)
        {
            Double[] array = new Double[list.Count];

            for (int i = 0; i < list.Count; i++)
                Double.TryParse(list[i], out array[i]);

            return search(array, dval, 0, array.Length - 1);
        }

        public static int FindProximalValFromList(List<UInt32> list, double dval)
        {
            Double[] array = new Double[list.Count];

            for (int i = 0; i < list.Count; i++)
                array[i] = (double)list[i];

            return search(array, dval, 0, array.Length - 1);
        }

        private static int search(double[] a, double key, int low, int high)
        {
            Int32 idx;
            for (idx = 0; idx < a.Count(); idx++)
            {
                if (a[idx] >= key)
                    break;
            }

            if (idx == 0) return idx;
            if (idx >= a.Count())
                idx--;
            else if ((a[idx] > key) && (a[idx - 1] < key))
            {
                if (Math.Abs(a[idx] - key) > Math.Abs(a[idx + 1] - key))
                    idx--;
            }
            return idx;
        }

        public static double ResistToTemp(double resist, Dictionary<Int32, double> m_TempVals, Dictionary<Int32, double> m_ResistVals)
        {
            Int32 idx;
            for (idx = 0; idx < m_ResistVals.Count; idx++)
            {
                if (m_ResistVals[idx] <= resist)
                    break;
            }

            if (idx == 0)
                return m_TempVals[0];
            else if (idx >= m_ResistVals.Count)
                idx--;
            else if ((m_ResistVals[idx] < resist) && (m_ResistVals[idx - 1] > resist))
            {
                float slope = (float)((float)m_TempVals[idx] - (float)m_TempVals[idx - 1]) / (float)((float)m_ResistVals[idx] - (float)m_ResistVals[idx - 1]);

                return m_TempVals[idx] - ((float)slope * (float)(m_ResistVals[idx] - resist));
            }
            return m_TempVals[idx];
        }

        public static double TempToResist(double temp, Dictionary<Int32, double> m_TempVals, Dictionary<Int32, double> m_ResistVals)
        {
            Int32 idx;

            for (idx = 0; idx < m_TempVals.Count; idx++)
            {
                if (m_TempVals[idx] >= temp)
                    break;
            }

            if (idx == 0)
                return m_ResistVals[0];
            else if (idx >= m_TempVals.Count)
                idx--;
            else if ((m_TempVals[idx] > temp) && (m_TempVals[idx - 1] < temp))
            {
                double slope = (double)((double)m_ResistVals[idx] - (double)m_ResistVals[idx - 1]) / (double)((double)m_TempVals[idx] - (double)m_TempVals[idx - 1]);
                return (double)((double)m_ResistVals[idx] - (double)((double)slope * (double)((double)m_TempVals[idx] - (double)temp)));
            }

            return m_ResistVals[idx];
        }

        /// <summary>
        /// 将十六进制数组转换为ASCII
        /// </summary>
        /// <param name="hexstring">十六进制数组</param>
        /// <returns>返回一条ASCII码</returns>
        public static string HexToASCII(byte[] hexBuffer)
        {
            int int10 = 0;
            string tmp = string.Empty;
            char[] c = new char[hexBuffer.Length];
            for (int i = 0; i < hexBuffer.Length; i++)
            {
                int10 = Convert.ToInt32(hexBuffer[i]);
                c[i] = Convert.ToChar(int10);
            }

            tmp = new string(c);
            return tmp;
        }

        /// <summary>
        /// 16进制字符串转换为二进制数组
        /// </summary>
        /// <param name="hexstring">用空格切割字符串</param>
        /// <returns>返回一个二进制字符串</returns>
        public static byte[] StringToASCII(string hexstring)
        {
            return ASCIIEncoding.ASCII.GetBytes(hexstring) as byte[];
        }

        public static string UInt32ToData(UInt32 value)
        {
            byte Mon, Day;
            UInt16 Year;
            string tempstr = string.Empty;
            Day = (byte)(value & 0x001F);
            Mon = (byte)((value & 0x01E0) >> 5);
            Year = (UInt16)((value & 0xFE00) >> 9);
            Year += 1980;
            return string.Format("{0:D2}-{1:D2}-{2:D4}",Day,Mon,Year);
        }

        public static UInt16 DateToUInt32(string tmp)
        {
            byte Mon, Day;
            UInt16 Year;
            byte Lo, Hi;

            string[] sArray = tmp.Split('-');
            Day = Convert.ToByte(sArray[0]);
            Mon = Convert.ToByte(sArray[1]);
            Year = Convert.ToUInt16(sArray[2]);
            Year -= 1980;

            Lo = (byte)((byte)((Mon & 0x07) << 5) | (byte)(Day & 0x1F));
            Hi = (byte)((byte)((Year & 0x7F) << 1) | (byte)((Mon & 0x08)>>3));
            return SharedFormula.MAKEWORD(Lo, Hi);
        }


        /// <summary>
        /// 删除文件夹及其内容
        /// </summary>
        /// <param name="dir"></param>
        public static void DeleteFolder(string dir, ObservableCollection<string> skpdir = null)
        {
            foreach (string d in Directory.GetFileSystemEntries(dir))
            {
                if (File.Exists(d))
                {
                    FileInfo fi = new FileInfo(d);
                    fi.Attributes = fi.Attributes & ~FileAttributes.ReadOnly & ~FileAttributes.Hidden;
                    /*if (fi.Attributes.ToString().IndexOf("ReadOnly") != -1)
                        fi.Attributes = FileAttributes.Normal;*/
                    File.Delete(d);//直接删除其中的文件  
                }
                else
                {
                    DirectoryInfo d1 = new DirectoryInfo(d);
                    if (skpdir != null)
                    {
                        if (skpdir.IndexOf(d1.FullName) != -1)
                            continue;
                    }
                    if (d1.GetFiles().Length + d1.GetDirectories().Length != 0)
                    {
                        DeleteFolder(d1.FullName, skpdir);////递归删除子文件夹
                    }
                    Directory.Delete(d);
                }
            }
        }

        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, ObservableCollection<string> skpdir = null)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = System.IO.Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    if (skpdir != null)
                    {
                        if (skpdir.IndexOf(subdir.FullName) != -1)
                            continue;
                    }
                    string temppath = System.IO.Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs, skpdir);
                }
            }
        }
    }
}
