namespace Brx.FlatFileMonger
{
   public class CsvNoHeaderException : CsvFormatException
   {
      public CsvNoHeaderException() : base("Cannot read CSV header")
      {
      }
   }
}