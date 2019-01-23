﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.IO;
using System.Data;
using System.ComponentModel;
using O2Micro.Cobra.EM;
using O2Micro.Cobra.Common;
using System.Windows.Controls.Primitives;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
//using System.Windows.Threading;
//using System.Threading;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using Excel = Microsoft.Office.Interop.Excel;

namespace O2Micro.Cobra.ProductionPanel
{
    public partial class MainControl
    {
        private string BoardFileName = "";

        private void LoadBoardFileButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Board Config File";
            openFileDialog.Filter = "Board Config file (*.board)|*.board||";
            openFileDialog.DefaultExt = "board";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                //if (CheckFile(openFileDialog.FileName))
                {
                    BoardFilePath.Text = openFileDialog.FileName;
                    BoardFilePath.IsEnabled = true;
                    BoardFileName = openFileDialog.SafeFileName;
                }
                //else
                {
                    //System.Windows.MessageBox.Show("File illegal.");
                }
            }
        }
        private void LoadFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Excel File";
            openFileDialog.Filter = "Excel file (*.xlsx)|*.xlsx||";
            openFileDialog.DefaultExt = "xlsx";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == true)
            {
                VerificationFilePath.Text = openFileDialog.FileName;
            }

        }

        private void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            if (VerificationFilePath.Text.ToString() == "File Path")
            {
                System.Windows.MessageBox.Show("请先选择目录!");
                return;
            }
            string filename = VerificationFilePath.Text.ToString();
            if (BoardFilePath.Text != "")
            {
                if (LibErrorCode.IDS_ERR_SUCCESSFUL != LoadFile(BoardFilePath.Text, MainControl.ViewModelTypy.BOARD))
                {
                    System.Windows.MessageBox.Show("Load board file failed!");
                    return;
                }
                boardviewmodel.WriteDevice();
            }

            //cfgviewmodel.WriteDevice();

            var excelApp = new Excel.Application();
            Excel.Workbook excelWKB = null;
            Excel._Worksheet excelSHEET = null;
            try
            {
                excelWKB = excelApp.Workbooks.Open(filename);
            }
            catch (Exception c)
            {
                System.Windows.MessageBox.Show(c.ToString());
            }
            foreach (var p in cfgviewmodel.sfl_parameterlist)
            {
                string relatedname = "";
                foreach (Excel._Worksheet st in excelWKB.Sheets)
                {
                    string fulltargetname = st.Name.Replace(' ', '/');
                    string targetname = "";
                    if (fulltargetname.StartsWith("("))
                    {
                        targetname = fulltargetname.Remove(0, fulltargetname.IndexOf(')') + 1);
                        if (targetname == p.nickname)
                        {
                            excelSHEET = st;
                            relatedname = fulltargetname.Substring(fulltargetname.IndexOf('(') + 1, fulltargetname.IndexOf(')') - fulltargetname.IndexOf('(')-1);
                            break;
                        }
                    }
                    else
                    {
                        if (fulltargetname == p.nickname)
                        {
                            excelSHEET = st;
                            break;
                        }
                    }
                }
                try
                {
                    if (excelSHEET != null)
                    {
                        int colcnt = excelSHEET.UsedRange.Columns.Count;
                        int rowcnt = excelSHEET.UsedRange.Rows.Count;
                        if (relatedname == "") //普通参数
                        {
                            for (int row = 2; row <= rowcnt; row++)
                            {
                                #region excel cell中的数据转到SFLViewModel中去
                                string tmp = ((Excel.Range)excelSHEET.Cells[row, 1]).Text.ToString();
                                if (tmp == "")
                                    break;
                                double dval = 0.0;
                                if (p.brange)//为正常录入浮点数
                                {
                                    switch (p.format)
                                    {
                                        case 0: //Int     
                                        case 1: //float1
                                        case 2: //float2
                                        case 3: //float3
                                        case 4: //float4
                                            {
                                                if (!Double.TryParse(tmp, out dval))
                                                    dval = 0.0;
                                                break;
                                            }
                                        case 5: //Hex
                                        case 6: //Word
                                            {
                                                try
                                                {
                                                    dval = (Double)Convert.ToInt32(tmp, 16);
                                                }
                                                catch (Exception ex)
                                                {
                                                    dval = 0.0;
                                                    break;
                                                }
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                    p.data = dval;
                                }
                                else
                                    p.sphydata = tmp;
                                #endregion

                                //WriteDevice(ref p);
                                #region WriteDevice SFLViewModel转到Parameter中去
                                if (p.berror && (p.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                                    return;

                                p.IsWriteCalled = true;

                                Parameter param = p.parent;
                                if (p.brange)
                                    param.phydata = p.data;
                                else
                                    param.sphydata = p.sphydata;

                                p.IsWriteCalled = false;
                                #endregion


                                #region 调用DEM API
                                msg.owner = this;
                                msg.gm.sflname = ProductionSFLDBName;
                                var list = new AsyncObservableCollection<Parameter>();
                                list.Add(p.parent);
                                msg.task_parameterlist.parameterlist = list;
                                msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
                                parent.AccessDevice(ref m_Msg);
                                while (msg.bgworker.IsBusy)
                                    System.Windows.Forms.Application.DoEvents();
                                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                    return;
                                #endregion

                                #region 新建一列放入计算值
                                excelSHEET.Cells[row, 6] = p.parent.hexdata;
                                #endregion

                                #region 新建一列放入比较值
                                string strAnswer = ((Excel.Range)excelSHEET.Cells[row, 2]).Text.ToString();
                                UInt16 answer = Convert.ToUInt16(strAnswer, 2);
                                excelSHEET.Cells[row, 7] = p.parent.hexdata - answer;
                                #endregion
                            }
                        }
                        else    //依赖参数
                        {
                            for (int row = 2; row <= rowcnt; row++)
                            {
                                #region 第一列
                                //获取TH参数的Name
                                string p1name = relatedname;
                                //根据Name获取参数
                                SFLParameterModel p1 = cfgviewmodel.GetParameterByName(p1name);
                                #region excel cell中的数据转到SFLViewModel中去
                                string tmp = ((Excel.Range)excelSHEET.Cells[row, 2]).Text.ToString();
                                double dval = 0.0;
                                if (p1.brange)//为正常录入浮点数
                                {
                                    switch (p1.format)
                                    {
                                        case 0: //Int     
                                        case 1: //float1
                                        case 2: //float2
                                        case 3: //float3
                                        case 4: //float4
                                            {
                                                if (!Double.TryParse(tmp, out dval))
                                                    dval = 0.0;
                                                break;
                                            }
                                        case 5: //Hex
                                        case 6: //Word
                                            {
                                                try
                                                {
                                                    dval = (Double)Convert.ToInt32(tmp, 16);
                                                }
                                                catch (Exception ex)
                                                {
                                                    dval = 0.0;
                                                    break;
                                                }
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                    p1.data = dval;
                                }
                                else
                                    p1.sphydata = tmp;
                                #endregion

                                //WriteDevice(ref p1);
                                #region WriteDevice SFLViewModel转到Parameter中去
                                if (p1.berror && (p1.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                                    return;

                                p1.IsWriteCalled = true;

                                Parameter param1 = p1.parent;
                                if (p1.brange)
                                    param1.phydata = p1.data;
                                else
                                    param1.sphydata = p1.sphydata;

                                p1.IsWriteCalled = false;
                                #endregion
                                #endregion
                                #region 第二列
                                #region excel cell中的数据转到SFLViewModel中去
                                tmp = ((Excel.Range)excelSHEET.Cells[row, 1]).Text.ToString();
                                dval = 0.0;
                                if (p.brange)//为正常录入浮点数
                                {
                                    switch (p.format)
                                    {
                                        case 0: //Int     
                                        case 1: //float1
                                        case 2: //float2
                                        case 3: //float3
                                        case 4: //float4
                                            {
                                                if (!Double.TryParse(tmp, out dval))
                                                    dval = 0.0;
                                                break;
                                            }
                                        case 5: //Hex
                                        case 6: //Word
                                            {
                                                try
                                                {
                                                    dval = (Double)Convert.ToInt32(tmp, 16);
                                                }
                                                catch (Exception ex)
                                                {
                                                    dval = 0.0;
                                                    break;
                                                }
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                    p.data = dval;
                                }
                                else
                                    p.sphydata = tmp;
                                #endregion

                                //WriteDevice(ref p);
                                #region WriteDevice SFLViewModel转到Parameter中去
                                if (p.berror && (p.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                                    return;

                                p.IsWriteCalled = true;

                                Parameter param = p.parent;
                                if (p.brange)
                                    param.phydata = p.data;
                                else
                                    param.sphydata = p.sphydata;

                                p.IsWriteCalled = false;
                                #endregion
                                #endregion


                                #region 调用DEM API
                                msg.owner = this;
                                msg.gm.sflname = ProductionSFLDBName;
                                var list = new AsyncObservableCollection<Parameter>();
                                list.Add(param1);
                                list.Add(param);
                                msg.task_parameterlist.parameterlist = list;
                                msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
                                parent.AccessDevice(ref m_Msg);
                                while (msg.bgworker.IsBusy)
                                    System.Windows.Forms.Application.DoEvents();
                                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                    return;
                                #endregion

                                #region 新建一列放入计算值
                                excelSHEET.Cells[row, 6] = p.parent.hexdata;
                                #endregion

                                #region 新建一列放入比较值
                                string strAnswer = ((Excel.Range)excelSHEET.Cells[row, 3]).Text.ToString();
                                UInt16 answer = Convert.ToUInt16(strAnswer, 2);
                                excelSHEET.Cells[row, 7] = p.parent.hexdata - answer;
                                #endregion
                            }
                        }
                    }
                }
                catch (Exception c)
                {
                    System.Windows.MessageBox.Show(c.ToString());
                }
                finally
                {
                    //excelWKB.Close(true);
                    //System.Runtime.InteropServices.Marshal.ReleaseComObject(excelWKB);
                    //excelWKB = null;
                }
            }
            excelApp.Workbooks.Close();
            excelApp.Quit();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
            excelApp = null;
            System.Windows.MessageBox.Show("Done!");
        }
    }
}
