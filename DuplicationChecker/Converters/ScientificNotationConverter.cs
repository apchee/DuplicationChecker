using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DuplicationChecker.Converters;

public class ScientificNotationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return value;
        string[] values = { "123", "187", "1,10000e+00", "4,00000e+01" };
        if (value is double || value is float || value is int || value is long || value is ulong || value is decimal)
        {
            try
            {
                var fv = String.Format("{0:#,###,###.##} s", value);
                return fv;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
            
        }     
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
