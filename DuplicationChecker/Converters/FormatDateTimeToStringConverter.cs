using System;
using System.Globalization;
using System.Windows.Data;

namespace FileDuplicationChecker.Converters;

public class FormatDateTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return "";
        if(value is DateTime)
        {
            DateTime dateTime = (DateTime)value;
            return dateTime.ToString("yyyyMMdd HH:mm:ss");
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
