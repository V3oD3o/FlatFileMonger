using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Brx.FlatFileMonger
{
   public sealed class FixedSchema : IEnumerable<FixedColumn>
   {

      private readonly List<FixedColumn> _columns;
      
      private Encoding _encoding;
      private bool _noMoreColumns;

      public FixedSchema()
      {
         _columns = new List<FixedColumn>();
         _encoding = Encoding.ASCII;
         _noMoreColumns = false;
      }

      public FixedSchema(params int[] widths) : this()
      {
         foreach (int width in widths)
         {
            AddColumn(width, true);
         }
      }

      public Encoding Encoding
      {
         get
         {
            return _encoding;
         }
         set
         {
            _encoding = value;
         }
      }

      public int ColumnCount
      {
         get
         {
            return _columns.Count;
         }
      }

      public FixedColumn this[int index]
      {
         get
         {
            return _columns[index];
         }
      }

      public void AddColumn(int width, bool trimValue)
      {
         if (_noMoreColumns)
         {
            throw new InvalidOperationException();
         }

         _columns.Add(new FixedColumn(width, trimValue));

         if (width == 0)
         {
            // only the last column width can be set to auto
            _noMoreColumns = true;
         }
      }

      public void RemoveColumn(int index)
      {
         if (_noMoreColumns && index == _columns.Count - 1)
         {
            // auto-width column is being removed
            _noMoreColumns = false;
         }

         _columns.RemoveAt(index);
      }

      public void Validate()
      {
         if (_columns.Count == 0)
         {
            throw new InvalidDataException("No columns specified");
         }

         if (_encoding is null)
         {
            throw new InvalidDataException("Encoding is null");
         }
      }

      #region IEnumerable<FixedColumn> implementation

      public IEnumerator GetEnumerator()
      {
         return ((IEnumerable<FixedColumn>)this).GetEnumerator();
      }

      IEnumerator<FixedColumn> IEnumerable<FixedColumn>.GetEnumerator()
      {
         return _columns.GetEnumerator();
      }

      #endregion

   }
}