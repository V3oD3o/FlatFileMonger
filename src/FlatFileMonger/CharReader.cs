using System;
using System.IO;

namespace Brx.FlatFileMonger
{
   internal class CharReader : IDisposable
   {

      private TextReader _reader;
      private char _lastChar;
      private char _nextChar;
      private int _position;
      private bool _isEOF;

      public CharReader(TextReader reader)
      {
         _reader = reader;
         _lastChar = Ascii.Null;
         _nextChar = Ascii.Null;
         _position = -1;
         _isEOF = false;
         SkipChar();
      }

      public bool BOF
      {
         get
         {
            return _position <= 0;
         }
      }

      public bool EOF
      {
         get
         {
            return _isEOF;
         }
      }

      public int Position
      {
         get
         {
            return _position;
         }
      }

      public char LastChar
      {
         get
         {
            if (_position <= 0)
            {
               throw new InvalidOperationException();
            }

            return _lastChar;
         }
      }

      public char NextChar
      {
         get
         {
            if (_isEOF)
            {
               throw new InvalidOperationException();
            }

            return _nextChar;
         }
      }

      public void Close()
      {
         Dispose(true);
      }

      public bool SkipChar()
      {
         if (_isEOF)
         {
            return false;
         }
         else
         {
            _position += 1;
            if (_position > 0)
            {
               _lastChar = _nextChar;
            }

            int ch = _reader.Read();
            if (ch < 0)
            {
               _nextChar = Ascii.Null;
               _isEOF = true;
            }
            else
            {
               _nextChar = (char)ch;
            }

            return true;
         }
      }

      public void SkipChar(char ch)
      {
         if (_isEOF || _nextChar != ch)
         {
            throw new ArgumentException();
         }

         SkipChar();
      }

      public bool SkipCharIf(char ch)
      {
         return !_isEOF && _nextChar == ch && SkipChar();
      }

      #region IDisposable Implementation

      public void Dispose()
      {

         Dispose(true);
         GC.SuppressFinalize(this);
      }

      protected void Dispose(bool disposing)
      {
         if (disposing && _reader != null)
         {
            _reader.Close();
         }

         _reader = null;
      }

      ~CharReader()
      {
         Dispose(false);
      }

      #endregion
   }
}