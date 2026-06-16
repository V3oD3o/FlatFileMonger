namespace CodeSource.Text
{
   public class CsvNoHeaderException : CsvFormatException
   {
      public CsvNoHeaderException() : base("Cannot read CSV header")
      {
      }
   }
}