using System;
using System.Collections.Generic;
using System.IO;

namespace Brx.FlatFileMonger
{
   public class CsvWriter : IDisposable
   {
      private TextWriter _output;

      private readonly char _delimiter;
      private readonly char _quoteChar;
      private readonly string _newLineString;
      private readonly bool _alwaysQuoteValues;
      private readonly bool _enableSparseRecords;
      private readonly bool _enableLongRecords;
      private readonly bool _appendMode;

      private bool _isInRecord;
      private int _recordIndex;
      private int _recordFieldCount;
      private int _schemaFieldCount;
      private int _commentCount;

      public CsvWriter(TextWriter output) 
         : this(output, CsvFormatOptions.Default)
      {
      }

      public CsvWriter(TextWriter output, CsvFormatOptions options)
      {
         if (output is null)
         {
            throw new ArgumentNullException(nameof(output));
         }

         if (options is null)
         {
            throw new ArgumentNullException(nameof(options));
         }

         options.Validate();

         _output = output;
         _delimiter = options.Delimiter;
         _quoteChar = options.QuoteChar;
         _newLineString = options.NewLineString;
         _alwaysQuoteValues = options.AlwaysQuoteValues;
         _enableSparseRecords = options.EnableSparseRecords;
         _enableLongRecords = options.EnableLongRecords;

         _isInRecord = false;
         _recordIndex = -1;
         _recordFieldCount = 0;
         _schemaFieldCount = -1; // not yet known
         _commentCount = 0;
         _appendMode = false;
      }

      public CsvWriter(Stream output) 
         : this(output, CsvFormatOptions.Default)
      {
      }

      public CsvWriter(Stream output, CsvFormatOptions options) 
         : this(new StreamWriter(output, options.Encoding), options)
      {
      }

      public CsvWriter(string path) 
         : this(path, CsvFormatOptions.Default)
      {
      }

      public CsvWriter(string path, CsvFormatOptions options) 
         : this(path, options, false)
      {
      }

      public CsvWriter(string path, CsvFormatOptions options, bool append) 
         : this(new StreamWriter(path, append, options.Encoding), options)
      {
         _appendMode = append;
      }

      public int FieldCount
      {
         get
         {
            return _schemaFieldCount;
         }
         set
         {
            if (_schemaFieldCount != -1)
            {
               throw new InvalidOperationException();
            }

            _schemaFieldCount = value;
         }
      }

      public int RecordIndex
      {
         get
         {
            return _recordIndex;
         }
      }

      public void Close()
      {
         Dispose(true);
      }

      public void WriteHeader(string columns)
      {
         WriteRecord(columns.Split('|'));
      }

      public void WriteRecord(params object[] fields)
      {
         WriteBeginRecord();
         WriteFields(fields);
         WriteEndRecord();
      }

      public void WriteRecord(List<object> fields)
      {
         WriteBeginRecord();
         WriteFields(fields);
         WriteEndRecord();
      }

      public void WriteBeginRecord()
      {
         if (_isInRecord)
         {
            throw new InvalidOperationException();
         }

         WriteBeginNewLine();

         _isInRecord = true;
         _recordIndex += 1;
         _recordFieldCount = 0;
      }

      public void WriteFields(params object[] fields)
      {
         foreach (object fld in fields)
         {
            WriteField(fld);
         }
      }

      public void WriteFields(List<object> fields)
      {
         foreach (object fld in fields)
         {
            WriteField(fld);
         }
      }

      public void WriteField(object value)
      {
         if (!_isInRecord)
         {
            throw new InvalidOperationException();
         }

         if (!_enableLongRecords && _schemaFieldCount != -1 && _recordFieldCount >= _schemaFieldCount)
         {
            throw new CsvFormatException("Record too long");
         }

         if (_recordFieldCount > 0)
         {
            _output.Write(_delimiter);
         }

         char ch;
         char[] chArray;
         string str = Convert.ToString(value);
         if (str is null)
         {
            // replace nothing with empty string
            str = string.Empty;
         }

         if (str.IndexOf(_quoteChar) >= 0)
         {
            // value contains quote char -> quote + escape
            _output.Write(_quoteChar);
            chArray = str.ToCharArray();
            for (int i = 0, loopTo = chArray.Length - 1; i <= loopTo; i++)
            {
               ch = chArray[i];
               if (ch == _quoteChar)
               {
                  _output.Write(ch);
               }
               _output.Write(ch);
            }
            _output.Write(_quoteChar);
         }
         else if (_alwaysQuoteValues || str.IndexOfAny(new char[] { _delimiter, Ascii.Cr, Ascii.Lf }) >= 0)
         {
            // always quote is true or value contains special char -> quote
            _output.Write(_quoteChar);
            _output.Write(str);
            _output.Write(_quoteChar);
         }
         else if (str.Length > 0)
         {
            // nothing special, just write the value
            _output.Write(str);
         }

         _recordFieldCount += 1;
      }

      public void WriteEndRecord()
      {
         if (!_isInRecord)
         {
            throw new InvalidOperationException();
         }

         if (_schemaFieldCount == -1)
         {
            if (_recordFieldCount > 0)
            {
               // field count is being autodetected from current record
               _schemaFieldCount = _recordFieldCount;
            }
         }
         else if (!_enableSparseRecords)
         {
            while (_recordFieldCount < _schemaFieldCount)
               WriteField(null);
         }

         _isInRecord = false;
      }

      public void WriteComment(string text)
      {
         if (_isInRecord)
         {
            throw new InvalidOperationException();
         }

         if (text != null && text.Length > 0)
         {
            var reader = new StringReader(text);
            string line = reader.ReadLine();
            while (!(line is null))
            {
               WriteBeginNewLine();
               _output.Write(Ascii.Hash);
               _output.Write(line);
               _commentCount += 1;
               line = reader.ReadLine();
            }
         }
         else
         {
            WriteBeginNewLine();
            _output.Write(Ascii.Hash);
            _commentCount += 1;
         }
      }

      private void WriteBeginNewLine()
      {
         if (_recordIndex + _commentCount >= 0 || _appendMode)
         {
            _output.Write(_newLineString);
         }
      }

      #region IDisposable Implementation

      public void Dispose()
      {
         Dispose(true);
         GC.SuppressFinalize(this);
      }

      protected void Dispose(bool disposing)
      {
         if (disposing && _output != null)
         {
            _output.Close();
         }

         _output = null;
      }

      ~CsvWriter()
      {
         Dispose(false);
      }

      #endregion
   }
}