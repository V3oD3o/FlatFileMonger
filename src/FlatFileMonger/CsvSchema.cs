using System;
using System.Reflection;

namespace Brx.FlatFileMonger
{
   public class CsvSchema
   {
      internal CsvSchema()
      {
      }

      public CsvSchema(CsvColumnCollection columns)
      {
         BindColumns(columns, null);
      }

      protected CsvSchema(CsvColumnCollection columns, ICsvRecordAccessor record)
      {
         BindColumns(columns, record);
      }

      protected void BindColumns(CsvColumnCollection columns, ICsvRecordAccessor record)
      {
         foreach (FieldInfo fieldInfo in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
         {
            object[] attributes = fieldInfo.GetCustomAttributes(typeof(CsvColumnAttribute), true);
            if (attributes != null && attributes.Length > 0)
            {
               CsvColumnAttribute columnAttribute = (CsvColumnAttribute)attributes[0];
               string columnName = (columnAttribute.Name is null) ? fieldInfo.Name : columnAttribute.Name;
               int columnIndex = columns.IndexOf(columnName);

               if (ReferenceEquals(fieldInfo.FieldType, typeof(int)))
               {
                  fieldInfo.SetValue(this, columnIndex);
               }
               else if (ReferenceEquals(fieldInfo.FieldType, typeof(CsvFieldAccessor)))
               {
                  fieldInfo.SetValue(this, new CsvFieldAccessor(record, columnIndex));
               }
            }
         }
      }
   }
}