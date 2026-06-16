using System;

namespace Brx.FlatFileMonger
{
   public class CsvColumnAttribute : Attribute
   {
      public CsvColumnAttribute()
      {
      }

      public CsvColumnAttribute(string name)
      {
         Name = name;
      }

      public string Name { get; set; }
   }
}