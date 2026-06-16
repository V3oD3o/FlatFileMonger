using System;
using System.IO;
using System.Text;
using System.Globalization;

namespace CodeSource.Text
{
   internal class CsvParser : IDisposable
   {
      private readonly char _delimiter;
      private readonly char _quoteChar;
      private readonly bool _preserveWhiteSpace;

      private CharReader _input;
      private int _lineNo;
      private int _linePos;
      private int _tokenPos;
      private TokenTypeEnum _tokenType;
      private NewLineModeEnum _newLineMode;
      private string _tokenValue;

      public CsvParser(TextReader reader) : this(reader, CsvFormatOptions.Default)
      {
      }

      public CsvParser(TextReader input, CsvFormatOptions options)
      {
         _input = new CharReader(input);
         _lineNo = 1;
         _linePos = 0;
         _tokenPos = 0;
         _delimiter = options.Delimiter;
         _quoteChar = options.QuoteChar;
         _newLineMode = options.NewLineMode;
         _preserveWhiteSpace = options.PreserveWhiteSpace;
         SetNextToken(TokenTypeEnum.Unknown);
      }

      public TokenTypeEnum TokenType
      {
         get
         {
            return _tokenType;
         }
      }

      public string TokenValue
      {
         get
         {
            return _tokenValue;
         }
      }

      public NewLineModeEnum NewLineMode
      {
         get
         {
            return _newLineMode;
         }
      }

      public bool PreserveWhiteSpace
      {
         get
         {
            return _preserveWhiteSpace;
         }
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
            return _tokenPos - _linePos + 1;
         }
      }

      public bool SkipToken()
      {
         switch (_tokenType)
         {

            case TokenTypeEnum.EOF:
               {
                  // if EOF already has been reached, return false
                  return false;
               }

            case TokenTypeEnum.Invalid:
               {
                  // skip the rest of the line containing the invalid token
                  SkipToNewLine();
                  break;
               }

         }

         if (!_preserveWhiteSpace)
         {
            SkipWhiteSpace();
         }

         // capture line and character position
         _tokenPos = _input.Position;
         if (_tokenType == TokenTypeEnum.NewLine)
         {
            _lineNo += 1;
            _linePos = _tokenPos;
         }

         if (_input.EOF)
         {
            // if we have no more characters left, set EOF as the next token, and return true
            SetNextToken(TokenTypeEnum.EOF);
         }
         else
         {
            switch (_tokenType)
            {

               case TokenTypeEnum.Comma:
                  {
                     if (TryParse(TokenTypeEnum.Comma, TokenTypeEnum.NewLine, TokenTypeEnum.Value) == TokenTypeEnum.Invalid)
                     {
                        SetNextToken(TokenTypeEnum.Invalid);
                     }

                     break;
                  }

               case TokenTypeEnum.Comment:
               case TokenTypeEnum.Invalid:
                  {
                     if (TryParse(TokenTypeEnum.NewLine) == false)
                     {
                        SetNextToken(TokenTypeEnum.Invalid);
                     }

                     break;
                  }

               case TokenTypeEnum.NewLine:
               case TokenTypeEnum.Unknown:
                  {
                     if (TryParse(TokenTypeEnum.Comment, TokenTypeEnum.Comma, TokenTypeEnum.NewLine, TokenTypeEnum.Value) == TokenTypeEnum.Invalid)
                     {
                        SetNextToken(TokenTypeEnum.Invalid);
                     }

                     break;
                  }

               case TokenTypeEnum.Value:
                  {
                     if (TryParse(TokenTypeEnum.Comma, TokenTypeEnum.NewLine) == TokenTypeEnum.Invalid)
                     {
                        SetNextToken(TokenTypeEnum.Invalid);
                     }

                     break;
                  }

               default:
                  {
                     throw new InvalidOperationException();
                  }

            }
         }

         return true;
      }

      public void SkipToken(TokenTypeEnum token)
      {
         if (_tokenType != token)
         {
            throw new InvalidOperationException();
         }

         SkipToken();
      }

      public bool SkipTokenIf(TokenTypeEnum token)
      {
         return _tokenType == token && SkipToken();
      }

      private bool TryParse(TokenTypeEnum token)
      {
         switch (token)
         {

            case TokenTypeEnum.Comma:
               {
                  if (_input.SkipCharIf(_delimiter))
                  {
                     SetNextToken(TokenTypeEnum.Comma);
                     return true;
                  }

                  break;
               }

            case TokenTypeEnum.Comment:
               {
                  if (_input.SkipCharIf(Ascii.Hash))
                  {
                     SetNextToken(TokenTypeEnum.Comment, ParseCommentValue());
                     return true;
                  }

                  break;
               }

            case TokenTypeEnum.NewLine:
               {
                  if (ParseNewLine())
                  {
                     SetNextToken(TokenTypeEnum.NewLine);
                     return true;
                  }

                  break;
               }

            case TokenTypeEnum.Value:
               {
                  if (_input.NextChar == _quoteChar)
                  {
                     SetNextToken(TokenTypeEnum.Value, ParseQuotedValue());
                  }
                  else
                  {
                     SetNextToken(TokenTypeEnum.Value, ParsePlainValue());
                  }
                  return true;
               }

            default:
               {
                  throw new ArgumentException();
               }

         }

         return false;
      }

      private TokenTypeEnum TryParse(params TokenTypeEnum[] tokens)
      {
         foreach (TokenTypeEnum token in tokens)
         {
            if (TryParse(token))
            {
               // allowed token found
               return token;
            }
         }

         // non of the allowed tokens found
         return TokenTypeEnum.Invalid;
      }

      private void SetNextToken(TokenTypeEnum token)
      {
         _tokenType = token;
         _tokenValue = null;
      }

      private void SetNextToken(TokenTypeEnum token, string value)
      {
         _tokenType = token;
         _tokenValue = value;
      }

      private bool ParseNewLine()
      {
         switch (_newLineMode)
         {

            case NewLineModeEnum.Cr:
               {
                  return _input.SkipCharIf(Ascii.Cr);
               }

            case NewLineModeEnum.CrLf:
               {
                  return _input.SkipCharIf(Ascii.Cr) && _input.SkipCharIf(Ascii.Lf);
               }

            case NewLineModeEnum.Lf:
               {
                  return _input.SkipCharIf(Ascii.Lf);
               }

            case NewLineModeEnum.Any:
               {
                  if (_input.SkipCharIf(Ascii.Cr))
                  {
                     _input.SkipCharIf(Ascii.Lf);
                     return true;
                  }
                  else
                  {
                     return _input.SkipCharIf(Ascii.Lf);
                  }
               }

            case NewLineModeEnum.Auto:
               {
                  if (_input.SkipCharIf(Ascii.Cr))
                  {
                     if (_input.SkipCharIf(Ascii.Lf))
                     {
                        _newLineMode = NewLineModeEnum.CrLf;
                     }
                     else
                     {
                        _newLineMode = NewLineModeEnum.Cr;
                     }
                     return true;
                  }
                  else if (_input.SkipCharIf(Ascii.Lf))
                  {
                     _newLineMode = NewLineModeEnum.Lf;
                     return true;
                  }
                  else
                  {
                     return false;
                  }
               }

            default:
               {
                  throw new ArgumentException();
               }

         }
      }

      private bool SkipToNewLine()
      {
         while (!_input.EOF)
         {
            switch (_input.NextChar)
            {
               case Ascii.Cr:
               case Ascii.Lf:
                  {
                     return true;
                  }

               default:
                  {
                     _input.SkipChar();
                     break;
                  }
            }
         }

         return false;
      }

      private bool SkipWhiteSpace()
      {
         bool result = false;

         while (!_input.EOF)
         {
            if (IsWhiteSpace(_input.NextChar))
            {
               _input.SkipChar();
               result = true;
            }
            else
            {
               break;
            }
         }

         return result;
      }

      public bool IsWhiteSpace(char ch)
      {
         if (ch == _delimiter || ch == _quoteChar)
         {
            return false;
         }
         else if (ch <= 0xFF)
         {
            return ch == Ascii.Space || ch == Ascii.Tab;
         }
         else
         {
            return char.GetUnicodeCategory(ch) == UnicodeCategory.SpaceSeparator;
         }
      }

      private string ParseCommentValue()
      {
         char ch;
         var sb = new StringBuilder();

         while (!_input.EOF)
         {
            ch = _input.NextChar;
            bool exitDo = false;
            switch (ch)
            {
               case Ascii.Cr:
               case Ascii.Lf:
                  {
                     // newline found, stop parsing
                     exitDo = true;
                     break;
                  }

               default:
                  {
                     sb.Append(ch);
                     break;
                  }
            }

            if (exitDo)
            {
               break;
            }
            _input.SkipChar();
         }

         return sb.ToString();
      }

      private string ParsePlainValue()
      {
         char ch;
         var sb = new StringBuilder();
         int trimPos = -1;

         while (!_input.EOF)
         {
            ch = _input.NextChar;
            
            if (ch == _delimiter || ch == _quoteChar || ch == Ascii.Cr || ch == Ascii.Lf)
            {
               // delimiter, quote or newline found, stop parsing
               break;
            }

            if (!_preserveWhiteSpace && IsWhiteSpace(ch))
            {
               // whitespace char, store trim position if the previous char was non-whitespace
               if (trimPos < 0)
               {
                  // store trim position
                  trimPos = sb.Length;
               }
            }
            else
            {
               // non-whitespace char, reset trim position
               trimPos = -1;
            }
            sb.Append(ch);

            _input.SkipChar();
         }

         if (trimPos >= 0)
         {
            // right trim whitespace if needed
            sb.Length = trimPos;
         }

         return sb.ToString();
      }

      private string ParseQuotedValue()
      {
         var sb = new StringBuilder();

         // skip the starting quote
         _input.SkipChar(_quoteChar);

         while (!_input.EOF)
         {
            switch (_input.NextChar)
            {
               case var ch when ch == _quoteChar:
                  {
                     // quote char found, skip it and check for escape
                     _input.SkipChar();
                     if (_input.EOF || _input.NextChar != _quoteChar)
                     {
                        // closing quote, return parsed value
                        return sb.ToString();
                     }
                     else
                     {
                        // escaped quote, append to value, and continue parsing
                        sb.Append(_quoteChar);
                        _input.SkipChar();
                     }

                     break;
                  }

               case Ascii.Cr:
                  {
                     // carriage return char found
                     sb.Append(Ascii.Cr);
                     _input.SkipChar();
                     if (_input.SkipCharIf(Ascii.Lf))
                     {
                        // line feed char found
                        sb.Append(Ascii.Lf);
                     }
                     // capture line position
                     _lineNo += 1;
                     _linePos = _input.Position;
                     break;
                  }

               case Ascii.Lf:
                  {
                     // line feed char found
                     sb.Append(Ascii.Lf);
                     _input.SkipChar();
                     // capture line position
                     _lineNo += 1;
                     _linePos = _input.Position;
                     break;
                  }

               default:
                  {
                     // normal char (not quote and not newline)
                     sb.Append(_input.NextChar);
                     _input.SkipChar();
                     break;
                  }

            }
         }

         return sb.ToString();
      }

      #region IDisposable Implementation

      public void Dispose()
      {

         Dispose(true);
         GC.SuppressFinalize(this);
      }

      protected void Dispose(bool disposing)
      {
         if (disposing && _input != null)
         {
            _input.Close();
         }

         _input = null;
      }

      ~CsvParser()
      {
         Dispose(false);
      }

      #endregion
   }
}