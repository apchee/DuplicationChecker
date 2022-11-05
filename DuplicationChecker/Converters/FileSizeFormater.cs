using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DuplicationChecker.Converters;

public class FileSizeFormater : IValueConverter
{
    static string[] sizes = { "B", "KB", "MB", "GB", "TB" };
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value.GetType();
        if (value is Single || value is int || value is long || value is uint || value is ulong)
        {
            var v = value.ToString();

            double len = double.Parse(v);
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            string result = String.Format("{0:0.##} {1}", len, sizes[order]);
            return result;
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
