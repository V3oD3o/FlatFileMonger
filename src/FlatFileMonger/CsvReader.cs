using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Brx.FlatFileMonger
{
   public class CsvReader : IDisposable, IEnumerable<string>, ICsvRecordAccessor
   {

      private static Regex _sharedTrailerPattern;

      private static Regex GetTrailerPattern()
      {
         if (_sharedTrailerPattern is null)
         {
            _sharedTrailerPattern = new Regex(@"^TRAILER(?<rowCount>\d+)$");
         }
         return _sharedTrailerPattern;
      }

      private CsvParser _parser;
      private readonly bool _hasHeaderRow;
      private readonly bool _enableSparseRecords;
      private readonly bool _enableLongRecords;
      private readonly CsvDuplicateModeEnum _duplicateColumnMode;
      private readonly string _predefinedSchema;
      private readonly Regex _trailerPattern;
      private readonly string _defaultValue;

      private CsvColumnCollection _columns;
      private readonly List<string> _fields;
      private readonly List<string> _comments;

      private int _fieldCount;
      private int _recordIndex;

      private CsvFormatException _lastError;

      public CsvReader(TextReader reader) : this(reader, CsvFormatOptions.Default)
      {
      }

      public CsvReader(TextReader reader, CsvFormatOptions options)
      {
         if (reader is null)
         {
            throw new ArgumentNullException("reader");
         }

         if (options is null)
         {
            throw new ArgumentNullException("options");
         }

         options.Validate();

         _parser = new CsvParser(reader, options);
         _fields = new List<string>();
         _comments = new List<string>();
         _fieldCount = -1; // not yet known
         _recordIndex = -1;
         _hasHeaderRow = options.HasHeaderRow;
         _enableSparseRecords = options.EnableSparseRecords;
         _enableLongRecords = options.EnableLongRecords;
         _duplicateColumnMode = options.DuplicateColumnMode;
         _predefinedSchema = options.Schema;
         _trailerPattern = GetTrailerPattern();
         _defaultValue = options.DefaultValue;
      }

      public CsvReader(Stream stream) : this(stream, CsvFormatOptions.Default)
      {
      }

      public CsvReader(Stream stream, CsvFormatOptions options) : this(new StreamReader(stream, options.Encoding), options)
      {
      }

      public CsvReader(string path) : this(path, CsvFormatOptions.Default)
      {
      }

      public CsvReader(string path, CsvFormatOptions options) : this(new StreamReader(path, options.Encoding), options)
      {
      }

      public List<string> Comments
      {
         get
         {
            return _comments;
         }
      }

      public CsvColumnCollection Columns
      {
         get
         {
            return _columns;
         }
      }

      public List<string> Fields
      {
         get
         {
            return _fields;
         }
      }

      public int FieldCount
      {
         get
         {
            return _fieldCount;
         }
         set
         {
            if (_fieldCount != -1)
            {
               throw new InvalidOperationException();
            }

            _fieldCount = value;
         }
      }

      public string this[int index]
      {
         get
         {
            if (index < 0)
            {
               return null;
            }
            else
            {
               return _fields[index];
            }
         }
      }

      public string[] this[int[] indices]
      {
         get
         {
            int hib = indices.Length - 1;
            var result = new string[hib + 1];
            for (int i = 0, loopTo = hib; i <= loopTo; i++)
               result[i] = this[indices[i]];
            return result;
         }
      }

      public string[] this[int index0, params int[] indices]
      {
         get
         {
            int hib = indices.Length;
            var result = new string[hib + 1];
            result[0] = this[index0];
            for (int i = 1, loopTo = hib; i <= loopTo; i++)
               result[i] = this[indices[i - 1]];
            return result;
         }
      }

      public string this[string name]
      {
         get
         {
            return this[_columns.IndexOf(name)];
         }
      }

      public string[] this[string[] names]
      {
         get
         {
            int hib = names.Length - 1;
            var result = new string[hib + 1];
            for (int i = 0, loopTo = hib; i <= loopTo; i++)
               result[i] = this[names[i]];
            return result;
         }
      }

      public string[] this[string name0, params string[] names]
      {
         get
         {
            int hib = names.Length;
            var result = new string[hib + 1];
            result[0] = this[name0];
            for (int i = 1, loopTo = hib; i <= loopTo; i++)
               result[i] = this[names[i - 1]];
            return result;
         }
      }

      public int RecordIndex
      {
         get
         {
            return _recordIndex;
         }
      }

      public bool HasError
      {
         get
         {
            return _lastError != null;
         }
      }

      public CsvFormatException LastError
      {
         get
         {
            return _lastError;
         }
      }

      public bool ReadHeader()
      {
         if (_columns != null)
         {
            // header already read
            return true;
         }
         else if (_hasHeaderRow)
         {
            if (_recordIndex < 0)
            {
               // read column headers from first row if needed
               var header = new List<string>();
               if (ReadRecord(header))
               {
                  _columns = new CsvColumnCollection
                  {
                     DuplicateColumnMode = _duplicateColumnMode
                  };
                  if (!string.IsNullOrEmpty(_predefinedSchema))
                  {
                     // header is overridden 
                     _columns.Add(_predefinedSchema.Split('|'));
                  }
                  else
                  {
                     _columns.Add(header);
                  }
                  return true;
               }
            }
         }
         else if (!string.IsNullOrEmpty(_predefinedSchema))
         {
            // read column names from options
            _columns = new CsvColumnCollection
            {
               DuplicateColumnMode = _duplicateColumnMode
            };
            _columns.Add(_predefinedSchema.Split(Ascii.Pipe));
            return true;
         }
         else
         {
            return true;
         }

         return false;
      }

      public bool Read()
      {
         // check for error condition, try to resync
         if (!Resync())
         {
            return false;
         }

         // read column headers from first row if needed
         if (!ReadHeader())
         {
            return false;
         }

         // initialize record storage
         _fields.Clear();

         // read next record
         _recordIndex += 1;

         return ReadRecord(_fields);
      }

      public bool TryRead()
      {
         try
         {
            return Read();
         }
         catch (CsvFormatException ex)
         {
            _lastError = ex;
            return true;
         }
      }

      public void Close()
      {
         Dispose();
      }

      private bool Resync()
      {
         if (_lastError != null)
         {
            _lastError = null;
            do
            {
               bool exitDo = false;
               switch (_parser.TokenType)
               {

                  case TokenTypeEnum.EOF:
                     {
                        return false;
                     }

                  case TokenTypeEnum.NewLine:
                     {
                        _parser.SkipToken();
                        exitDo = true;
                        break;
                     }

                  default:
                     {
                        _parser.SkipToken();
                        break;
                     }
               }

               if (exitDo)
               {
                  break;

               }
            }
            while (true);
         }

         return true;
      }

      private bool ReadRecord(List<string> fields)
      {
         // skip empty lines and comments (move to content)
         do
         {
            bool exitDo = false;
            switch (_parser.TokenType)
            {

               case TokenTypeEnum.Comment:
                  {
                     // store comment line
                     _comments.Add(_parser.TokenValue);
                     _parser.SkipToken();
                     break;
                  }

               case TokenTypeEnum.NewLine:
               case TokenTypeEnum.Unknown:
                  {
                     _parser.SkipToken();
                     break;
                  }

               case TokenTypeEnum.EOF:
                  {
                     // return false if EOF found
                     return false;
                  }

               default:
                  {
                     // content found, exit loop
                     exitDo = true;
                     break;
                  }
            }

            if (exitDo)
            {
               break;

            }
         }
         while (true);

         // read field values
         do
         {
            bool exitDo1 = false;
            switch (_parser.TokenType)
            {

               case TokenTypeEnum.Value:
                  {
                     // explicit field value found
                     fields.Add(_parser.TokenValue);
                     _parser.SkipToken();
                     break;
                  }

               case TokenTypeEnum.Comma:
               case TokenTypeEnum.NewLine:
               case TokenTypeEnum.EOF:
                  {
                     // delimiter, newline or EOF found, assume empty field value
                     fields.Add(_defaultValue);
                     break;
                  }

               default:
                  {
                     // invalid data found, exit loop
                     exitDo1 = true;
                     break;
                  }
            }

            if (exitDo1)
            {
               break;

            }

            if ((_fieldCount == -1 || fields.Count < _fieldCount || _enableLongRecords) && _parser.TokenType == TokenTypeEnum.Comma)
            {
               // skip delimiter if field count is unknown or not reached
               _parser.SkipToken();
            }
            else
            {
               // otherwise exit loop
               break;
            }
         }
         while (true);

         // check for invalid token
         if (_parser.TokenType == TokenTypeEnum.Invalid)
         {
            throw new CsvFormatException("Syntax error", _parser);
         }

         if (fields.Count == 1)
         {
            var m = _trailerPattern.Match(fields[0]);
            if (m.Success)
            {
               if (Convert.ToInt32(m.Groups["rowCount"].Value) != _recordIndex)
               {
                  throw new CsvTrailerException("Invalid TRAILER found", _parser);
               }
               else
               {
                  _comments.Add(fields[0]);
                  fields.Clear();
                  return false;
               }
            }
         }

         if (_fieldCount == -1)
         {
            // field count is being autodetected from current record
            _fieldCount = fields.Count;
         }
         else if (fields.Count < _fieldCount)
         {
            if (_enableSparseRecords)
            {
               do
                  fields.Add(null);
               while (fields.Count != _fieldCount);
            }
            else
            {
               // not enough fields, sparse records not enabled
               throw new CsvFormatException("Missing column", _parser);
            }
         }

         if (_parser.TokenType == TokenTypeEnum.NewLine)
         {
            // skip newline token
            _parser.SkipToken();
         }
         else if (_parser.TokenType != TokenTypeEnum.EOF)
         {
            // invalid token found (other than NewLine or EOF)
            throw new CsvFormatException("Record too long", _parser);
         }

         return true;
      }

      public string[] ToArray()
      {
         return _fields.ToArray();
      }

      #region IDisposable Implementation

      public void Dispose()
      {

         Dispose(true);
         GC.SuppressFinalize(this);
      }

      protected void Dispose(bool disposing)
      {
         if (disposing && _parser != null)
         {
            _parser.Dispose();
         }

         _parser = null;
      }

      ~CsvReader()
      {
         Dispose(false);
      }

      #endregion

      #region IEnumerable<string> implementation

      IEnumerator<string> IEnumerable<string>.GetEnumerator()
      {
         return _fields.GetEnumerator();
      }

      public IEnumerator GetEnumerator()
      {
         return ((IEnumerable<string>)this).GetEnumerator();
      }

      #endregion

      #region ICsvRecordAccessor implementation

      public string GetValue(int index)
      {

         return this[index];
      }

      public void SetValue(int index, string value)
      {

         throw new NotSupportedException();
      }

      #endregion
   }
}