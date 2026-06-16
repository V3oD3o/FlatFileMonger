using System;

namespace CodeSource.Text
{
   public class FixedColumn
   {
      private readonly int _width;
      private readonly bool _trimValue;

      internal FixedColumn(int width, bool trimValue)
      {
         if (width < 0)
         {
            throw new ArgumentOutOfRangeException(nameof(width));
         }

         _width = width;
         _trimValue = trimValue;
      }

      public int Width
      {
         get
         {
            return _width;
         }
      }

      public bool TrimValue
      {
         get
         {
            return _trimValue;
         }
      }
   }
}