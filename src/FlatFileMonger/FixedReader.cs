using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Brx.FlatFileMonger
{
   public class FixedReader : IDisposable, IEnumerable<string>
   {
      private readonly FixedSchema _schema;
      private readonly List<string> _fields;
      private readonly ReadOnlyCollection<string> _readOnlyFields;

      private TextReader _reader;
      private int _recordIndex;

      public FixedReader(TextReader reader, FixedSchema schema)
      {
         if (reader is null)
         {
            throw new ArgumentNullException(nameof(reader));
         }

         if (schema is null)
         {
            throw new ArgumentNullException(nameof(schema));
         }

         schema.Validate();

         _reader = reader;
         _schema = schema;
         _fields = new List<string>();
         _readOnlyFields = new ReadOnlyCollection<string>(_fields);
         _recordIndex = -1;
      }

      public FixedReader(string path, FixedSchema schema) 
         : this(new StreamReader(path, schema.Encoding), schema)
      {
      }

      public FixedReader(Stream input, FixedSchema schema) 
         : this(new StreamReader(input, schema.Encoding), schema)
      {
      }

      public ReadOnlyCollection<string> Fields
      {
         get
         {
            return _readOnlyFields;
         }
      }

      public int FieldCount
      {
         get
         {
            return _schema.ColumnCount;
         }
      }

      public string this[int index]
      {
         get
         {
            return _fields[index];
         }
      }

      public int RecordIndex
      {
         get
         {
            return _recordIndex;
         }
      }

      public bool Read()
      {
         _fields.Clear();

         string line = _reader.ReadLine();

         if (line != null)
         {
            int pos = 0;
            int width;
            string value;

            _recordIndex += 1;

            foreach (FixedColumn column in _schema)
            {
               width = column.Width;
               if (width > 0)
               {
                  value = line.Substring(pos, width);
                  pos += width;
               }
               else
               {
                  value = line.Substring(pos);
                  pos += value.Length;
               }
               if (column.TrimValue)
               {
                  value = value.TrimEnd(Ascii.Space);
               }
               _fields.Add(value);
            }

            return true;
         }
         else
         {
            return false;
         }
      }

      public string[] ToArray()
      {
         return _fields.ToArray();
      }

      #region IDisposable Implementation

      public void Dispose()
      {

         Dispose(true);
         GC.SuppressFinalize(this);
      }

      protected void Dispose(bool disposing)
      {
         if (disposing && _reader != null)
         {
            _reader.Close();
         }

         _reader = null;
      }

      ~FixedReader()
      {
         Dispose(false);
      }

      #endregion

      #region IEnumerable<string> implementation

      private IEnumerator<string> GetEnumerator()
      {
         return _fields.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

      #endregion

   }
}