namespace Brx.FlatFileMonger
{
   public interface ICsvRecordAccessor
   {
      string GetValue(int index);
      void SetValue(int index, string value);
   }
}