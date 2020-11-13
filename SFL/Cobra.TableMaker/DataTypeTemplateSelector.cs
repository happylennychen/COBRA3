using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;

namespace Cobra.TableMaker
{
    enum editortype
    {
        TextBox_EditType = 0,
        ComboBox_EditType = 1,
    }

    public class DataTypeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextBoxTemplate { get; set; }
        public DataTemplate ComboBoxTemplate { get; set; }
        public DataTemplate CheckBoxTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) 
        {
            UInt16 controlType = (UInt16)editortype.TextBox_EditType;
            if (item != null)
            {
                HeaderItem hi = item as HeaderItem;
                controlType = hi.Type;

                switch (controlType)
                {
                    case (UInt16)editortype.TextBox_EditType:
                        return TextBoxTemplate;
                    case (UInt16)editortype.ComboBox_EditType:
                        return ComboBoxTemplate;
                    default:
                        return TextBoxTemplate;
                }
            }
            return TextBoxTemplate;
        }
    }
}
