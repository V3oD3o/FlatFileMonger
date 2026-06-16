namespace Brx.FlatFileMonger
{
   public class CsvTrailerException : CsvFormatException
   {
      internal CsvTrailerException(string message, CsvParser parser) : base(message, parser.LineNo, parser.CharPos)
      {
      }
   }
}