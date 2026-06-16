using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CodeSource.Text
{
   public delegate bool CsvDataPreprocessor(DataTable table, CsvRecord data, int index, object state);
   public delegate void CsvDataPostprocessor(DataTable table, DataRow data, object state);

   public class CsvDataSet : DataSet
   {
      private readonly Dictionary<string, CsvFormatOptions> _formats;

      public CsvDataSet() : base()
      {
         _formats = new Dictionary<string, CsvFormatOptions>();
      }

      public CsvFormatOptions GetFormat(string name)
      {
         return _formats[name];
      }

      public void DefineFormat(string name, CsvFormatOptions options)
      {
         _formats[name] = options;
      }

      public void RemoveFormat(string name)
      {
         _formats.Remove(name);
      }

      public DataColumn ResolveColumn(string column)
      {
         return ResolveColumn(new ColumnRef(column, true));
      }

      public DataColumn ResolveColumn(string tableName, string columnName)
      {
         var table = Tables[tableName];
         if (table is null)
         {
            return null;
         }
         else
         {
            return table.Columns[columnName];
         }
      }

      public DataColumn ResolveColumn(string tableName, string relationName, string columnName)
      {
         var table = Tables[tableName];
         if (table is null)
         {
            return null;
         }
         else
         {
            return ResolveColumn(table, relationName, columnName);
         }
      }

      public DataColumn ResolveColumn(DataTable from, string relationName, string columnName)
      {
         var relation = Relations[relationName];
         if (relation is null)
         {
            return null;
         }
         else if (ReferenceEquals(relation.ParentTable, from))
         {
            return relation.ChildTable.Columns[columnName];
         }
         else
         {
            return relation.ParentTable.Columns[columnName];
         }
      }

      private DataColumn ResolveColumn(ColumnRef column)
      {
         return ResolveColumn(column.TableName, column.ColumnName);
      }

      private DataColumn ResolveColumn(DataTable from, ColumnRef columnRef)
      {
         if (columnRef.HasTableName)
         {
            return ResolveColumn(from, columnRef.TableName, columnRef.ColumnName);
         }
         else
         {
            return from.Columns[columnRef.ColumnName];
         }
      }

      public DataTable CreateTable(string name, Uri templateUri)
      {
         var doc = new XmlDocument();
         doc.Load(templateUri.AbsoluteUri);
         return CreateTable(name, doc.DocumentElement);
      }

      public DataTable CreateTable(string name, string templateResourceName)
      {
         return CreateTable(name, templateResourceName, Assembly.GetCallingAssembly());
      }

      public DataTable CreateTable(string name, string templateResourceName, Assembly assembly)
      {
         var doc = new XmlDocument();
         doc.Load(assembly.GetManifestResourceStream(templateResourceName));
         return CreateTable(name, doc.DocumentElement);
      }

      public DataTable CreateTable(string name, XmlElement template)
      {
         DataColumn column;
         Type dataType;
         var primaryKey = new List<DataColumn>();
         var table = new DataTable(name);
         table.Locale = Locale;

         foreach (XmlElement columnDef in template.SelectNodes("column"))
         {
            dataType = GetDataType(columnDef.GetAttribute("type"));
            column = table.Columns.Add(columnDef.GetAttribute("name"), dataType);

            if (columnDef.HasAttribute("primaryKey"))
            {
               primaryKey.Add(column);
            }
         }

         if (primaryKey.Count > 0)
         {
            table.PrimaryKey = primaryKey.ToArray();
         }

         Tables.Add(table);

         return table;
      }

      public static Type GetDataType(string dataType)
      {
         if (dataType is null)
            return typeof(string);

         if (dataType.IndexOf('.') >= 0)
            return Type.GetType(dataType, true, true);

         StringComparer cmp = StringComparer.OrdinalIgnoreCase;
         if (cmp.Equals("DateTime", dataType) || cmp.Equals("date", dataType))
            return typeof(DateTime);

         if (cmp.Equals("Integer", dataType) || cmp.Equals("int", dataType))
            return typeof(int);

         if (cmp.Equals("Boolean", dataType) || cmp.Equals("bool", dataType))
            return typeof(bool);

         return Type.GetType("System." + dataType, true, true);
      }

      public DataTable CreateTable(string name, DataTable from, string columns)
      {
         return CreateTable(name, from, ParseColumnList(columns, true));
      }

      private DataTable CreateTable(string name, DataTable from, ColumnRef[] columns)
      {
         if (from is null)
         {
            throw new ArgumentNullException("from");
         }

         DataTable table;
         DataColumn column;

         if (columns is null || columns.Length == 0)
         {
            table = from.Clone();
            table.TableName = name;
         }
         else
         {
            table = new DataTable(name);
            table.Locale = Locale;

            foreach (ColumnRef info in columns)
            {
               column = ResolveColumn(from, info);
               if (column is null)
               {
                  throw new ArgumentException("Column not found: " + info.ToString());
               }
               CreateColumn(table, info, column.DataType);
            }
         }

         Tables.Add(table);

         return table;
      }

      public DataColumn CreateColumn(DataTable table, string columnName, Type dataType, object defaultValue)
      {
         var column = table.Columns.Add(columnName, dataType);
         foreach (DataRow row in table.Rows)
            row[column] = defaultValue;
         return column;
      }

      private DataColumn CreateColumn(DataTable table, ColumnRef info, Type dataType)
      {
         Type internalDataType;
         string columnName = info.HasAlias ? info.ColumnAlias : info.ColumnName;

         switch (info.Function)
         {
            case AggregateFunctionEnum.Count:
               internalDataType = typeof(int);
               break;

            case AggregateFunctionEnum.Sum:
               if (ReferenceEquals(dataType, typeof(int)) || ReferenceEquals(dataType, typeof(decimal)))
               {
                  internalDataType = dataType;
               }
               else
               {
                  throw new NotSupportedException($"DataType '{dataType.Name}' is not supported by aggregate function '{info.Function}'");
               }
               break;

            case AggregateFunctionEnum.None:
               internalDataType = dataType;
               break;

            default:
               throw new NotSupportedException("Unknown aggregate function: " + ((int)info.Function).ToString());
         }

         return table.Columns.Add(columnName, internalDataType);
      }

      public DataRelation CreateRelation(string name, string parentColumn, string childColumn)
      {
         return CreateRelation(name, parentColumn, childColumn, false);
      }

      public DataRelation CreateRelation(string name, string parentColumn, string childColumn, bool createConstraints)
      {
         var parent = new ColumnRef(parentColumn, true);
         var child = new ColumnRef(childColumn, true);

         if (!parent.HasTableName)
         {
            throw new ArgumentException(nameof(parentColumn));
         }

         if (!child.HasTableName)
         {
            throw new ArgumentException(nameof(childColumn));
         }

         return CreateRelation(name, ResolveColumn(parent), ResolveColumn(child), createConstraints);
      }

      public DataRelation CreateRelation(string name, string parentTable, string parentColumn, string childTable, string childColumn)
      {
         return CreateRelation(name, parentTable, parentColumn, childTable, childColumn, false);
      }

      public DataRelation CreateRelation(string name, string parentTable, string parentColumn, string childTable, string childColumn, bool createConstraints)
      {
         return CreateRelation(name, ResolveColumn(parentTable, parentColumn), ResolveColumn(childTable, childColumn), createConstraints);
      }

      public DataRelation CreateRelation(string name, DataColumn parentColumn, DataColumn childColumn)
      {
         return CreateRelation(name, parentColumn, childColumn, false);
      }

      public DataRelation CreateRelation(string name, DataColumn parentColumn, DataColumn childColumn, bool createConstraints)
      {
         var result = new DataRelation(name, parentColumn, childColumn, createConstraints);
         Relations.Add(result);
         return result;
      }

      public void InsertInto(DataTable table, string from, string columns)
      {
         InsertInto(table, Tables[from], columns, null, null);
      }

      public void InsertInto(DataTable table, DataTable from, string columns)
      {
         InsertInto(table, from, columns, null, null);
      }

      public void InsertInto(DataTable table, string from, string columns, string where, string order)
      {
         InsertInto(table, Tables[from], columns, where, order);
      }

      public void InsertInto(DataTable table, DataTable from, string columns, string where, string order)
      {
         InsertInto(table, from, ParseColumnList(columns, true), where, order);
      }

      public int DeleteFrom(string table, string where, params object[] args)
      {
         return DeleteFrom(table, string.Format(where, args));
      }

      public int DeleteFrom(string table, string where)
      {
         return DeleteFrom(Tables[table], where);
      }

      public int DeleteFrom(DataTable table, string where, params object[] args)
      {
         return DeleteFrom(table, string.Format(where, args));
      }

      public int DeleteFrom(DataTable table, string where)
      {
         DataRow[] rows = table.Select(where);
         foreach (DataRow row in rows)
            table.Rows.Remove(row);
         return rows.Length;
      }

      private abstract class ColumnBinding
      {
         public abstract object Bind(DataRow sourceRow);

         public DataRow ToSingleRow(object source)
         {
            if (source is null)
            {
               return null;
            }
            else if (source is Array)
            {
               DataRow[] rows = (DataRow[])source;
               switch (rows.Length)
               {
                  case 0: 
                     return null;
                  
                  case 1: 
                     return rows[0];

                  default:
                     throw new NotSupportedException();
               }
            }
            else
            {
               return (DataRow)source;
            }
         }

         public DataRow[] ToRowArray(object source)
         {
            if (source is null)
            {
               return new DataRow[] { };
            }
            else if (source is Array)
            {
               return (DataRow[])source;
            }
            else
            {
               return new DataRow[] { (DataRow)source };
            }
         }

         public object Evaluate(object source, DataColumn column, AggregateFunctionEnum function)
         {
            switch (function)
            {
               case AggregateFunctionEnum.None:
               {
                  var row = ToSingleRow(source);
                  if (row is null)
                  {
                     return DBNull.Value;
                  }
                  else
                  {
                     return row[column];
                  }
               }

               case AggregateFunctionEnum.Count:
               {
                  return ToRowArray(source).Length;
               }

               case AggregateFunctionEnum.Sum:
               {
                  DataRow[] rows = ToRowArray(source);
                  if (rows.Length > 0)
                  {
                     dynamic sum = Convert.ChangeType(0, column.DataType);
                     foreach (DataRow row in rows)
                     {
                        if (!row.IsNull(column))
                        {
                           sum += row[column];
                        }
                     }
                     return sum;
                  }
                  else
                  {
                     return DBNull.Value;
                  }
               }

               default:
               {
                  throw new NotSupportedException();
               }
            }
         }
      }

      private sealed class LocalColumnBinding : ColumnBinding
      {

         private DataColumn _column;
         private AggregateFunctionEnum _function;

         public LocalColumnBinding(DataColumn column, AggregateFunctionEnum function)
         {
            _column = column;
            _function = function;
         }

         public override object Bind(DataRow sourceRow)
         {
            return Evaluate(sourceRow, _column, _function);
         }
      }

      private sealed class ChildColumnBinding : ColumnBinding
      {

         private DataColumn _column;
         private AggregateFunctionEnum _function;
         private DataRelation _relation;

         public ChildColumnBinding(DataRelation relation, DataColumn column, AggregateFunctionEnum function)
         {
            _column = column;
            _function = function;
            _relation = relation;
         }

         public override object Bind(DataRow sourceRow)
         {
            return Evaluate(sourceRow.GetChildRows(_relation), _column, _function);
         }
      }

      private sealed class ParentColumnBinding : ColumnBinding
      {

         private DataColumn _column;
         private AggregateFunctionEnum _function;
         private DataRelation _relation;

         public ParentColumnBinding(DataRelation relation, DataColumn column, AggregateFunctionEnum function)
         {
            _column = column;
            _function = function;
            _relation = relation;
         }

         public override object Bind(DataRow sourceRow)
         {
            return Evaluate(sourceRow.GetParentRow(_relation), _column, _function);
         }

      }

      private ColumnBinding CreateColumnBinding(DataTable localTable, ColumnRef columnRef)
      {
         if (columnRef.HasTableName)
         {
            // foreign binding
            var relation = Relations[columnRef.TableName];
            // Dim relation As DataRelation = _relationLookup(GetRelationKey(localTable.TableName, columnRef.TableName, columnRef.ColumnName))
            if (ReferenceEquals(relation.ParentTable, localTable))
            {
               return new ChildColumnBinding(relation, relation.ChildTable.Columns[columnRef.ColumnName], columnRef.Function);
            }
            else
            {
               return new ParentColumnBinding(relation, relation.ParentTable.Columns[columnRef.ColumnName], columnRef.Function);
            }
         }
         else
         {
            // local binding
            return new LocalColumnBinding(localTable.Columns[columnRef.ColumnName], columnRef.Function);
         }
      }

      private void InsertInto(DataTable table, DataTable from, ColumnRef[] columns, string where, string order)
      {
         DataRow row;
         DataRow[] sourceRows = from.Select(where, order);

         int lastColumnIndex;
         if (columns is null)
         {
            lastColumnIndex = from.Columns.Count - 1;
         }
         else
         {
            lastColumnIndex = columns.Length - 1;
         }

         var bindings = new ColumnBinding[lastColumnIndex + 1];
         for (int i = 0, loopTo = lastColumnIndex; i <= loopTo; i++)
         {
            if (columns is null)
            {
               bindings[i] = new LocalColumnBinding(from.Columns[i], AggregateFunctionEnum.None);
            }
            else
            {
               bindings[i] = CreateColumnBinding(from, columns[i]);
            }
         }

         foreach (DataRow sourceRow in sourceRows)
         {
            row = table.NewRow();
            for (int i = 0, loopTo1 = lastColumnIndex; i <= loopTo1; i++)
               row[i] = bindings[i].Bind(sourceRow);
            table.Rows.Add(row);
         }
      }

      public object Select(string table, string column, string where)
      {
         return Select(Tables[table], column, where);
      }

      public object Select(string table, string column, string where, params object[] args)
      {
         return Select(Tables[table], column, where, args);
      }

      public object Select(DataTable table, string column, string where)
      {
         return Select(table, new ColumnRef(column, false), where);
      }

      public object Select(DataTable table, string column, string where, params object[] args)
      {
         return Select(table, new ColumnRef(column, false), string.Format(where, args));
      }

      private object Select(DataTable table, ColumnRef column, string where)
      {
         DataRow[] rows = table.Select(where);
         switch (rows.Length)
         {
            case 0:
               {
                  return null;
               }
            case 1:
               {
                  var binding = CreateColumnBinding(table, column);
                  return binding.Bind(rows[0]);
               }

            default:
               {
                  throw new InvalidOperationException("The specified query returned more than one rows");
               }
         }
      }

      public DataRow SelectRow(string table, string where)
      {
         return SelectRow(Tables[table], where);
      }

      public DataRow SelectRow(string table, string where, params object[] args)
      {
         return SelectRow(Tables[table], where, args);
      }

      public DataRow SelectRow(DataTable table, string where, params object[] args)
      {
         return SelectRow(table, string.Format(where, args));
      }

      public DataRow SelectRow(DataTable table, string where)
      {
         DataRow[] rows = table.Select(where);
         switch (rows.Length)
         {
            case 0:
               {
                  return null;
               }
            case 1:
               {
                  return rows[0];
               }

            default:
               {
                  throw new InvalidOperationException($"The specified query returned more than one ({rows.Length}) rows");
               }
         }
      }

      public DataTable SelectInto(string table, string from, string columns)
      {
         return SelectInto(table, Tables[from], columns, null, null);
      }

      public DataTable SelectInto(string table, DataTable from, string columns)
      {
         return SelectInto(table, from, columns, null, null);
      }

      public DataTable SelectInto(string table, string from, string columns, string where, string order)
      {
         return SelectInto(table, Tables[from], columns, where, order);
      }

      public DataTable SelectInto(string table, DataTable from, string columns, string where, string order)
      {
         return SelectInto(table, from, ParseColumnList(columns, true), where, order);
      }

      private DataTable SelectInto(string table, DataTable from, ColumnRef[] columns, string where, string order)
      {
         var result = CreateTable(table, from, columns);
         InsertInto(result, from, columns, where, order);
         return result;
      }

      public void JoinInto(string table, string columns, string where)
      {
         JoinInto(Tables[table], columns, where);
      }

      public void JoinInto(DataTable table, string columns, string where)
      {
         JoinInto(table, ParseColumnList(columns, true), where);
      }

      private void JoinInto(DataTable table, ColumnRef[] columnRefs, string where)
      {
         if (columnRefs != null && columnRefs.Length > 0)
         {
            int hib = columnRefs.Length - 1;
            ColumnRef info;
            DataColumn column;
            var columns = new DataColumn[hib + 1];
            var bindings = new ColumnBinding[hib + 1];

            for (int i = 0, loopTo = hib; i <= loopTo; i++)
            {
               info = columnRefs[i];
               column = ResolveColumn(table, info);
               if (column is null)
               {
                  throw new ArgumentException("Column not found: " + info.ToString());
               }
               columns[i] = CreateColumn(table, info, column.DataType);
               bindings[i] = CreateColumnBinding(table, info);
            }

            foreach (DataRow row in table.Select(where))
            {
               for (int i = 0, loopTo1 = hib; i <= loopTo1; i++)
                  row[columns[i]] = bindings[i].Bind(row);
            }
         }
      }

      public DataTable LoadTable(string tableName, string primaryKey, string input)
      {
         return LoadTable(tableName, primaryKey, new CsvReader(input));
      }

      public DataTable LoadTable(string tableName, string primaryKey, string input, string formatName)
      {
         return LoadTable(tableName, primaryKey, new CsvReader(input, (CsvFormatOptions)_formats[formatName]));
      }

      public DataTable LoadTable(string tableName, string primaryKey, string input, CsvFormatOptions format)
      {
         return LoadTable(tableName, primaryKey, new CsvReader(input, format));
      }

      public DataTable LoadTable(string tableName, string primaryKey, TextReader input)
      {
         return LoadTable(tableName, primaryKey, new CsvReader(input));
      }

      public DataTable LoadTable(string tableName, string primaryKey, TextReader input, string formatName)
      {
         return LoadTable(tableName, primaryKey, new CsvReader(input, (CsvFormatOptions)_formats[formatName]));
      }

      public DataTable LoadTable(string tableName, string primaryKey, TextReader input, CsvFormatOptions format)
      {
         return LoadTable(tableName, primaryKey, new CsvReader(input, format));
      }

      public DataTable LoadTable(string tableName, string primaryKey, CsvReader input)
      {
         return LoadTable(tableName, primaryKey, null, input, null, null);
      }

      public DataTable LoadTable(string tableName, string primaryKey, string extraColumns, CsvReader input, CsvDataPreprocessor preprocessor, object preprocessorState)
      {
         var result = new DataTable(tableName);

         try
         {
            if (!input.ReadHeader())
            {
               throw new CsvNoHeaderException();
            }

            foreach (string columnName in input.Columns)
               result.Columns.Add(columnName);

            if (!string.IsNullOrEmpty(extraColumns))
            {
               foreach (ColumnRef column in ParseColumnList(extraColumns, false))
               {
                  input.Columns.Add(column.ColumnName);
                  if (column.HasAlias)
                  {
                     // in this case alias contains the datatype of the column
                     Type dataType;
                     if (column.ColumnAlias.IndexOf('.') > 0)
                     {
                        dataType = Type.GetType(column.ColumnAlias);
                     }
                     else
                     {
                        dataType = Type.GetType("System." + column.ColumnAlias);
                     }
                     result.Columns.Add(column.ColumnName, dataType);
                  }
                  else
                  {
                     result.Columns.Add(column.ColumnName);
                  }
               }
            }

            if (!string.IsNullOrEmpty(primaryKey))
            {
               var pk = new List<DataColumn>();
               DataColumn column;
               foreach (ColumnRef info in ParseColumnList(primaryKey, false))
               {
                  column = result.Columns[info.ColumnName];
                  if (column is null)
                  {
                     input.Columns.Add(info.ColumnName);
                     column = result.Columns.Add(info.ColumnName);
                  }
                  pk.Add(column);
               }
               result.PrimaryKey = pk.ToArray();
            }

            string[] buffer = (string[])Array.CreateInstance(typeof(string), result.Columns.Count);

            while (input.TryRead())
            {
               try
               {
                  if (input.HasError)
                  {
                     Trace.WriteLine("ERROR: " + input.LastError.Message);
                  }
                  else
                  {
                     int overlappingCount = Math.Min(buffer.Length, input.Fields.Count);
                     input.Fields.CopyTo(0, buffer, 0, overlappingCount);
                     if (buffer.Length > overlappingCount)
                     {
                        Array.Clear(buffer, overlappingCount, buffer.Length - overlappingCount);
                     }
                     var rec = new CsvRecord(input.Columns, buffer);
                     if (preprocessor is null || preprocessor.Invoke(result, rec, input.RecordIndex, preprocessorState))
                     {
                        result.Rows.Add(buffer);
                     }
                  }
               }
               catch (Exception ex)
               {
                  Trace.WriteLine(ex.Message);
               }
            }

            Tables.Add(result);
         }
         finally
         {
            input.Dispose();
         }

         return result;
      }

      public DataTable LoadTable(DataTable table, string input, string formatName)
      {
         return LoadTable(table, new CsvReader(input, (CsvFormatOptions)_formats[formatName]));
      }

      public DataTable LoadTable(DataTable table, CsvReader input)
      {
         return LoadTable(table, input, null, null, null);
      }

      public DataTable LoadTable(DataTable table, CsvReader input, CsvDataPreprocessor preprocessor, object state)
      {
         return LoadTable(table, input, preprocessor, null, state);
      }

      public DataTable LoadTable(DataTable table, CsvReader input, CsvDataPostprocessor postprocessor, object state)
      {
         return LoadTable(table, input, null, postprocessor, state);
      }

      public DataTable LoadTable(DataTable table, CsvReader input, CsvDataPreprocessor preprocessor, CsvDataPostprocessor postprocessor, object state)
      {
         try
         {
            if (!input.ReadHeader())
            {
               throw new CsvNoHeaderException();
            }

            string[] buffer = (string[])Array.CreateInstance(typeof(string), table.Columns.Count);

            while (input.TryRead())
            {
               try
               {
                  if (input.HasError)
                  {
                     Trace.WriteLine("ERROR: " + input.LastError.Message);
                  }
                  else
                  {
                     int overlappingCount = Math.Min(buffer.Length, input.Fields.Count);
                     input.Fields.CopyTo(0, buffer, 0, overlappingCount);
                     if (buffer.Length > overlappingCount)
                     {
                        Array.Clear(buffer, overlappingCount, buffer.Length - overlappingCount);
                     }
                     var rec = new CsvRecord(input.Columns, buffer);
                     if (preprocessor is null || preprocessor.Invoke(table, rec, input.RecordIndex, state))
                     {
                        var row = table.Rows.Add(buffer);
                        if (postprocessor != null)
                        {
                           postprocessor.Invoke(table, row, state);
                        }
                     }
                  }
               }
               catch (Exception ex)
               {
                  Trace.WriteLine(ex.Message);
               }
            }
         }
         finally
         {
            input.Dispose();
         }

         return table;
      }

      public void SaveTable(string tableName, CsvWriter output)
      {
         SaveTable(tableName, output, true);
      }

      public void SaveTable(string tableName, CsvWriter output, bool closeOutput)
      {
         SaveTable(Tables[tableName], output, closeOutput);
      }

      public void SaveTable(DataTable table, CsvWriter output)
      {
         SaveTable(table, output, true);
      }

      public void SaveTable(DataTable table, CsvWriter output, bool closeOutput)
      {
         try
         {
            var headers = new List<string>();
            foreach (DataColumn column in table.Columns)
            {
               headers.Add(column.ColumnName);
            }
            output.WriteRecord(headers);

            foreach (DataRow row in table.Rows)
            {
               output.WriteRecord(row.ItemArray);
            }
         }
         finally
         {
            if (closeOutput)
            {
               output.Close();
            }
         }
      }

      private enum AggregateFunctionEnum
      {
         None,
         Count,
         Sum
      }

      private class ColumnRef
      {
         public string TableName;
         public string ColumnName;
         public string ColumnAlias;
         public AggregateFunctionEnum Function;

         public ColumnRef()
         {

         }

         public ColumnRef(string tableName, string columnName)
         {
            TableName = tableName;
            ColumnName = columnName;
         }

         public ColumnRef(string tableName, string columnName, string columnAlias)
         {
            TableName = tableName;
            ColumnName = columnName;
            ColumnAlias = columnAlias;
         }

         public ColumnRef(string definition, bool allowTableName)
         {
            var parser = GetColumnParser(allowTableName);
            var mat = parser.Match(definition);
            if (mat.Success)
            {
               Function = ParseAggregateFunction(mat.Groups["function"].Value);
               ColumnName = mat.Groups["column"].Value;
               ColumnAlias = mat.Groups["alias"].Value;
               if (allowTableName)
               {
                  TableName = mat.Groups["qualifier"].Value;
               }
            }
            else
            {
               throw new ArgumentException(nameof(definition));
            }
         }

         public bool HasAlias
         {
            get
            {
               return !string.IsNullOrEmpty(ColumnAlias);
            }
         }

         public bool HasTableName
         {
            get
            {
               return !string.IsNullOrEmpty(TableName);
            }
         }

         public bool IsAggregate
         {
            get
            {
               return Function != AggregateFunctionEnum.None;
            }
         }

         public override string ToString()
         {
            var sb = new StringBuilder();

            if (IsAggregate)
            {
               sb.Append(Function.ToString());
               sb.Append('(');
            }

            sb.Append('[');

            if (HasTableName)
            {
               sb.Append(TableName);
               sb.Append("].[");
            }

            sb.Append(ColumnName);

            if (HasAlias)
            {
               sb.Append("] [");
               sb.Append(ColumnAlias);
            }

            sb.Append(']');

            if (IsAggregate)
            {
               sb.Append(')');
            }

            return sb.ToString();
         }

         public string ToString(bool useQuotedIdentifiers)
         {
            if (useQuotedIdentifiers)
            {
               return ToString();
            }
            else
            {
               var sb = new StringBuilder();

               if (IsAggregate)
               {
                  sb.Append(Function.ToString());
                  sb.Append('(');
               }

               if (HasTableName)
               {
                  sb.Append(TableName);
                  sb.Append('.');
               }

               sb.Append(ColumnName);

               if (HasAlias)
               {
                  sb.Append(' ');
                  sb.Append(ColumnAlias);
               }

               if (IsAggregate)
               {
                  sb.Append(')');
               }

               return sb.ToString();
            }
         }

      }

      private static Regex _rxColumnNameParser;
      private static Regex _rxColumnQNameParser;

      private static Regex GetColumnParser(bool allowQualifiedNames)
      {
         if (allowQualifiedNames)
         {
            if (_rxColumnQNameParser is null)
            {
               _rxColumnQNameParser = CreateNameParserRegex(@"^\s*((?<function>\w+)\(\s*)?({0}\.)?{1}(?(function)\s*\)|)(\s+{2})?\s*$", "qualifier", "column", "alias");
            }
            return _rxColumnQNameParser;
         }
         else
         {
            if (_rxColumnNameParser is null)
            {
               _rxColumnNameParser = CreateNameParserRegex(@"^\s*((?<function>\w+)\(\s*)?{0}(?(function)\s*\)|)(\s+{1})?\s*$", "column", "alias");
            }
            return _rxColumnNameParser;
         }
      }

      private static Regex CreateNameParserRegex(string format, params string[] names)
      {
         for (int i = 0, loopTo = names.Length - 1; i <= loopTo; i++)
            names[i] = string.Format(@"(\[(?<{0}>[^\]]+)]|(?<{0}>[^[\s\.,]+))", names[i]);
         return new Regex(string.Format(format, names));
      }

      private static AggregateFunctionEnum ParseAggregateFunction(string function)
      {
         if (string.IsNullOrEmpty(function))
            return AggregateFunctionEnum.None;
         
         return (AggregateFunctionEnum)Enum.Parse(typeof(AggregateFunctionEnum), function, true);
      }

      private static ColumnRef[] ParseColumnList(string columnNames, bool allowQualifiedNames)
      {
         if (string.IsNullOrEmpty(columnNames) || columnNames == "*")
         {
            return null;
         }
         else
         {
            Match mat;
            ColumnRef col;
            string[] names = columnNames.Split(',');
            var parser = GetColumnParser(allowQualifiedNames);
            ColumnRef[] result = new ColumnRef[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
               mat = parser.Match(names[i]);
               if (mat.Success)
               {
                  col = new ColumnRef();
                  col.Function = ParseAggregateFunction(mat.Groups["function"].Value);
                  col.ColumnName = mat.Groups["column"].Value;
                  col.ColumnAlias = mat.Groups["alias"].Value;
                  if (allowQualifiedNames)
                  {
                     col.TableName = mat.Groups["qualifier"].Value;
                  }
                  result[i] = col;
               }
               else
               {
                  throw new ArgumentException($"columnNames[{i}]");
               }
            }

            return result;
         }
      }
   }
}