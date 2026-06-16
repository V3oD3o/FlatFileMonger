using System;
using System.Text;

namespace CodeSource.Text
{
   public class CsvFormatOptions
   {
      private static string CrLf = Ascii.Cr.ToString() + Ascii.Lf.ToString();

      public static readonly CsvFormatOptions Default =
         new CsvFormatOptions()
         {
            Encoding = Encoding.ASCII,
            Delimiter = Ascii.Comma,
            QuoteChar = Ascii.Quote,
            HasHeaderRow = true,
            AlwaysQuoteValues = false,
            EnableSparseRecords = false,
            EnableLongRecords = false,
            DuplicateColumnMode = CsvDuplicateModeEnum.Disallow,
            PreserveWhiteSpace = false,
            NewLineMode = NewLineModeEnum.Auto,
            Schema = null,
            DefaultValue = null
         };

      public Encoding Encoding;
      public char Delimiter;
      public char QuoteChar;
      public bool HasHeaderRow;
      public bool AlwaysQuoteValues;
      public bool EnableSparseRecords;
      public bool EnableLongRecords;
      public CsvDuplicateModeEnum DuplicateColumnMode;
      public bool PreserveWhiteSpace;
      public NewLineModeEnum NewLineMode;
      public string Schema;
      public string DefaultValue;

      public string NewLineString
      {
         get
         {
            switch (NewLineMode)
            {
               case NewLineModeEnum.CrLf:
                  return CrLf;
               
               case NewLineModeEnum.Cr:
                  return Ascii.Cr.ToString();
               
               case NewLineModeEnum.Lf:
                  return Ascii.Lf.ToString();

               default:
                  return Environment.NewLine;
            }
         }
      }

      public CsvFormatOptions Clone()
      {
         return (CsvFormatOptions)MemberwiseClone();
      }

      public void Validate()
      {
         if (Encoding is null)
         {
            throw new ArgumentNullException(nameof(Encoding));
         }

         if (Delimiter == QuoteChar)
         {
            throw new ArgumentException("Delimiter and QuoteChar properties have incompatible values");
         }

         if (NewLineString.IndexOf(Delimiter) >= 0)
         {
            throw new ArgumentException("Delimiter and NewLineMode properties have incompatible values");
         }

         if (NewLineString.IndexOf(QuoteChar) >= 0)
         {
            throw new ArgumentException("QuoteChar and NewLineMode properties have incompatible values");
         }
      }
   }
}