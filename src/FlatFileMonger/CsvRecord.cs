using System;
using System.Collections;
using System.Collections.Generic;

namespace CodeSource.Text
{
   public class CsvRecord : CsvSchema, ICloneable, IEnumerable<string>, ICsvRecordAccessor
   {
      private CsvColumnCollection _columns;
      private string[] _fields;

      public CsvRecord(CsvReader reader) : this(reader.Columns, reader.ToArray())
      {
      }

      public CsvRecord(CsvColumnCollection schema, string[] data)
      {
         if (schema is null)
         {
            throw new ArgumentNullException("schema");
         }

         if (data is null)
         {
            throw new ArgumentNullException("data");
         }

         BindColumns(schema, this);

         _columns = schema;
         _fields = data;
      }

      public CsvColumnCollection Columns
      {
         get
         {
            return _columns;
         }
      }

      public string[] Fields
      {
         get
         {
            return _fields;
         }
      }

      public int FieldCount
      {
         get
         {
            return _fields.Length;
         }
      }

      public string this[int index]
      {
         get
         {
            if (index < 0)
            {
               return null;
            }
            else
            {
               return _fields[index];
            }
         }
         set
         {
            _fields[index] = value;
         }
      }

      public string[] this[int[] indices]
      {
         get
         {
            int hib = indices.Length - 1;
            var result = new string[hib + 1];
            for (int i = 0, loopTo = hib; i <= loopTo; i++)
               result[i] = this[indices[i]];
            return result;
         }
      }

      public string[] this[int index0, params int[] indices]
      {
         get
         {
            int hib = indices.Length;
            var result = new string[hib + 1];
            result[0] = this[index0];
            for (int i = 1, loopTo = hib; i <= loopTo; i++)
               result[i] = this[indices[i - 1]];
            return result;
         }
      }

      public string this[string name]
      {
         get
         {
            return this[_columns.IndexOf(name)];
         }
         set
         {
            this[_columns.IndexOf(name)] = value;
         }
      }

      public string[] this[string[] names]
      {
         get
         {
            int hib = names.Length - 1;
            var result = new string[hib + 1];
            for (int i = 0, loopTo = hib; i <= loopTo; i++)
               result[i] = this[names[i]];
            return result;
         }
      }

      public string[] this[string name0, params string[] names]
      {
         get
         {
            int hib = names.Length;
            var result = new string[hib + 1];
            result[0] = this[name0];
            for (int i = 1, loopTo = hib; i <= loopTo; i++)
               result[i] = this[names[i - 1]];
            return result;
         }
      }

      public virtual void CopyFrom(CsvReader reader)
      {
         if (!ReferenceEquals(_columns, reader.Columns))
         {
            _columns = reader.Columns;
            BindColumns(_columns, this);
         }
         _fields = reader.ToArray();
      }

      public void CopyTo(object[] values)
      {
         Array.Copy(_fields, values, _fields.Length);
      }

      public void CopyTo(object[] values, int length)
      {
         Array.Copy(_fields, values, length);
      }

      public string Join(string delimiter)
      {
         return string.Join(delimiter, _fields);
      }

      #region ICloneable implementation

      public virtual object Clone()
      {

         return new CsvRecord(_columns, (string[])_fields.Clone());
      }

      #endregion

      #region IEnumerable<string> implementation

      public IEnumerator GetEnumerator()
      {
         return _fields.GetEnumerator();
      }

      IEnumerator<string> IEnumerable<string>.GetEnumerator()
      {
         return ((IEnumerable<string>)_fields).GetEnumerator();
      }

      #endregion

      #region ICsvRecordAccessor implementation

      public string GetValue(int index)
      {

         return this[index];
      }

      public void SetValue(int index, string value)
      {

         this[index] = value;
      }

      #endregion
   }
}