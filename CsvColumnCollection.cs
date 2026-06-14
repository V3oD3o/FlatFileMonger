using System.Collections;
using System.Collections.Generic;

namespace CodeSource.Text
{
   public class CsvColumnCollection : IEnumerable<string>
   {

      private readonly List<string> _items;
      private readonly Dictionary<string, int> _map;
      
      private CsvDuplicateModeEnum _duplicateColumnMode;

      public CsvColumnCollection()
      {
         _items = new List<string>();
         _map = new Dictionary<string, int>();
         _duplicateColumnMode = CsvDuplicateModeEnum.Disallow;
      }

      public CsvColumnCollection(string columns) : this()
      {
         Add(columns.Split(Ascii.Pipe));
      }

      public CsvColumnCollection(string[] columns) : this()
      {
         Add(columns);
      }

      public int Count
      {
         get
         {
            return _items.Count;
         }
      }

      public string this[int index]
      {
         get
         {
            return _items[index];
         }
      }

      public CsvDuplicateModeEnum DuplicateColumnMode
      {
         get
         {
            return _duplicateColumnMode;
         }
         set
         {
            _duplicateColumnMode = value;
         }
      }

      public int IndexOf(string name)
      {
         return _map.TryGetValue(name, out int index) ? index : -1;
      }

      public int[] IndexOf(params string[] names)
      {
         int count = names != null ? names.Length : 0;
         var result = new int[count];
         for (int i = 0; i < count; i++)
         {
            result[i] = IndexOf(names[i]);
         }
         return result;
      }

      public int IndexOfAny(params string[] names)
      {
         int result = -1;
         if (names != null)
         {
            int count = names.Length;
            for (int i = 0; i < count; i++)
            {
               result = IndexOf(names[i]);
               if (result >= 0)
               {
                  break;
               }
            }
         }
         return result;
      }

      public void Add(string column)
      {
         string name = column;

         switch (_duplicateColumnMode)
         {

            case CsvDuplicateModeEnum.Ignore:
               {
                  if (_map.ContainsKey(name))
                  {
                     return;
                  }

                  break;
               }

            case CsvDuplicateModeEnum.Rename:
               {
                  int num = 0;
                  do
                  {
                     name = AutoColumnName(column, num);
                     num += 1;
                  }
                  while (_map.ContainsKey(name));
                  break;
               }

         }

         int index = _items.Count;
         _items.Add(name);
         _map.Add(name, index);
      }

      private static string AutoColumnName(string column, int num)
      {
         if (string.IsNullOrEmpty(column))
         {
            return "NoName_" + num;
         }

         return (num > 0) 
            ? column + "_" + num 
            : column;
      }

      internal void Add(IEnumerable<string> columns)
      {
         foreach (string column in columns)
            Add(column);
      }

      public string[] ToArray()
      {
         return _items.ToArray();
      }

      #region IEnumerable<string> implementation

      private IEnumerator<string> GetEnumerator()
      {
         return _items.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

      #endregion

   }
}