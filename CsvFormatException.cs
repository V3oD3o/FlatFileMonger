using System;

namespace CodeSource.Text
{
   public class CsvFormatException : Exception
   {
      private readonly int _lineNo;
      private readonly int _charPos;

      internal CsvFormatException(string message) : base(message)
      {
      }

      internal CsvFormatException(string message, CsvParser parser) : this(message, parser.LineNo, parser.CharPos)
      {
      }

      internal CsvFormatException(string message, int lineNo, int charPos) : base($"'{message}' at line {lineNo}, position {charPos}")
      {
         _lineNo = lineNo;
         _charPos = charPos;
      }

      public int LineNo
      {
         get
         {
            return _lineNo;
         }
      }

      public int CharPos
      {
         get
         {
            return _charPos;
         }
      }
   }

   public class CsvTrailerException : CsvFormatException
   {
      internal CsvTrailerException(string message, CsvParser parser) : base(message, parser.LineNo, parser.CharPos)
      {
      }
   }

   public class CsvNoHeaderException : CsvFormatException
   {
      public CsvNoHeaderException() : base("Cannot read CSV header")
      {
      }
   }
}