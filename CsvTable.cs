using System;
using System.Collections.Generic;

namespace CodeSource.Text
{
   public class CsvTable
   {
      private List<string[]> _rows;
      private CsvColumnCollection _columns;

      private Dictionary<string, string[]> _index;
      private List<string[]> _duplicates;
      private string _keyColumn;
      private int _keyColumnIndex;

      public CsvTable()
      {
         _rows = new List<string[]>();
         _duplicates = new List<string[]>();
         _index = new Dictionary<string, string[]>();
      }

      public CsvColumnCollection Columns
      {
         get
         {
            return _columns;
         }
      }

      public ICollection<string> Keys
      {
         get
         {
            return _index.Keys;
         }
      }

      public string KeyColumn
      {
         get
         {
            return _keyColumn;
         }
      }

      public int KeyColumnIndex
      {
         get
         {
            return _keyColumnIndex;
         }
      }

      public List <string[]> Rows
      {
         get
         {
            return _rows;
         }
      }

      public bool HasDuplicates
      {
         get
         {
            return _duplicates.Count > 0;
         }
      }

      public List<string[]> Duplicates
      {
         get
         {
            return _duplicates;
         }
      }

      public string[] this[string key]
      {
         get
         {
            return _index[key];
         }
      }

      public bool ContainsKey(string key)
      {
         return _index.ContainsKey(key);
      }

      public void Load(string inputFile, string keyColumn)
      {
         Load(inputFile, keyColumn, -1, CsvFormatOptions.Default);
      }

      public void Load(string inputFile, int keyColumnIndex)
      {
         Load(inputFile, null, keyColumnIndex, CsvFormatOptions.Default);
      }

      public void Load(string inputFile, string keyColumn, CsvFormatOptions options)
      {
         Load(inputFile, keyColumn, -1, options);
      }

      public void Load(string inputFile, int keyColumnIndex, CsvFormatOptions options)
      {
         Load(inputFile, null, keyColumnIndex, options);
      }

      private void Load(string inputFile, string keyColumn, int keyColumnIndex, CsvFormatOptions options)
      {
         using (var reader = new CsvReader(inputFile, options))
         {
            if (reader.ReadHeader())
            {
               _columns = reader.Columns;
            }
            else
            {
               throw new CsvNoHeaderException();
            }

            if (!string.IsNullOrEmpty(keyColumn))
            {
               _keyColumn = keyColumn;
               _keyColumnIndex = _columns.IndexOf(keyColumn);
            }
            else
            {
               _keyColumn = _columns[keyColumnIndex];
               _keyColumnIndex = keyColumnIndex;
            }

            if (_keyColumnIndex < 0)
            {
               throw new CsvFormatException("cannot find primary key column");
            }

            _duplicates.Clear();
            _index.Clear();
            _rows.Clear();

            while (reader.Read())
            {
               string[] row = reader.ToArray();
               string key = row[_keyColumnIndex];

               if (_index.ContainsKey(key))
               {
                  _duplicates.Add(row);
               }
               else
               {
                  _rows.Add(row);
                  _index.Add(key, row);
               }
            }
         }
      }

      public void SaveDuplicates(string outputFile)
      {
         var writer = new CsvWriter(outputFile);
         try
         {
            writer.WriteRecord(_columns.ToArray());
            foreach (string[] row in _duplicates)
            {
               string key = row[_keyColumnIndex];
               writer.WriteRecord(_index[key]);
               writer.WriteRecord(row);
            }
         }
         finally
         {
            writer.Close();
         }
      }

   }

}