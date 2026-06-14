namespace CodeSource.Text
{
   public enum CsvDuplicateModeEnum
   {
      Ignore,
      Rename,
      Disallow
   }

   public enum NewLineModeEnum
   {
      Auto,
      Any,
      Cr,
      CrLf,
      Lf
   }

   internal enum TokenTypeEnum
   {
      Comma,
      Comment,
      NewLine,
      Invalid,
      Unknown,
      Value,
      EOF
   }
}