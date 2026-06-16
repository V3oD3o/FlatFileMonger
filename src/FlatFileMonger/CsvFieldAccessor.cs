using System;
using System.Globalization;

namespace CodeSource.Text
{
   public class CsvFieldAccessor
   {
      private readonly int _index;
      private readonly ICsvRecordAccessor _record;

      internal CsvFieldAccessor(ICsvRecordAccessor record, int index)
      {
         _index = index;
         _record = record;
      }

      public int Index
      {
         get
         {
            return _index;
         }
      }

      public string Value
      {
         get
         {
            return _record.GetValue(_index);
         }
         set
         {
            _record.SetValue(_index, value);
         }
      }

      public bool ToBoolean()
      {
         return Convert.ToBoolean(Value);
      }

      public bool ToBoolean(string trueValue)
      {
         return (Value ?? "") == (trueValue ?? "");
      }

      public int ToInteger()
      {
         return Convert.ToInt32(Value);
      }

      public decimal ToDecimal()
      {
         return Convert.ToDecimal(Value);
      }

      public DateTime ToDateTime()
      {
         return Convert.ToDateTime(Value);
      }

      public DateTime ToDateTime(string format)
      {
         return DateTime.ParseExact(Value, format, CultureInfo.CurrentCulture);
      }
   }
}