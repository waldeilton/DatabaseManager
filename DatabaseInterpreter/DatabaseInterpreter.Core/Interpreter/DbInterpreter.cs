﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseInterpreter.Core
{
    public abstract class DbInterpreter
    {
        #region Field & Property       
        private IObserver<FeedbackInfo> observer;
        protected DbConnector dbConnector;
        protected bool hasError = false;
        protected DateTime minDateTime = new DateTime(1970, 1, 1);
        public const string RowNumberColumnName = "_ROWNUMBER";
        public virtual string UnicodeInsertChar { get; } = "N";
        public virtual string ScriptsDelimiter => ";";
        public abstract string CommentString { get; }
        public bool ShowBuiltinDatabase => SettingManager.Setting.ShowBuiltinDatabase;
        public bool NotCreateIfExists => SettingManager.Setting.NotCreateIfExists;
        public DbObjectNameMode DbObjectNameMode => SettingManager.Setting.DbObjectNameMode;
        public int DataBatchSize => SettingManager.Setting.DataBatchSize;
        public abstract string CommandParameterChar { get; }
        public abstract char QuotationLeftChar { get; }
        public abstract char QuotationRightChar { get; }
        public abstract DatabaseType DatabaseType { get; }
        public abstract bool SupportBulkCopy { get; }
        public virtual List<string> BuiltinDatabases { get; } = new List<string>();
        public bool CancelRequested { get; set; }
        public bool HasError => this.hasError;
        public DbInterpreterOption Option { get; set; } = new DbInterpreterOption();
        public ConnectionInfo ConnectionInfo { get; protected set; }       

        public delegate Task DataReadHandler(Table table, List<TableColumn> columns, List<Dictionary<string, object>> data, DataTable dataTable);
        public event DataReadHandler OnDataRead;

        #endregion

        #region Constructor     

        public DbInterpreter(ConnectionInfo connectionInfo, DbInterpreterOption option)
        {
            this.ConnectionInfo = connectionInfo;
            this.Option = option;
        }
        #endregion

        #region Common Method    

        public DbConnection CreateConnection()
        {
            DbConnection dbConnection = this.dbConnector.CreateConnection();

            if (this.Option.RequireInfoMessage)
            {
                this.SubscribeInfoMessage(dbConnection);
            }

            return dbConnection;
        }

        protected virtual void SubscribeInfoMessage(DbConnection dbConnection) { }
        protected virtual void SubscribeInfoMessage(DbCommand dbCommand) { }

        public async Task OpenConnectionAsync(DbConnection connection)
        {
            if (connection.State == ConnectionState.Closed)
            {
                await connection.OpenAsync();
            }
        }

        public string GetObjectDisplayName(DatabaseObject obj, bool useQuotedString = false)
        {
            if (this.GetType().Name == nameof(SqlServerInterpreter))
            {
                return $"{this.GetString(obj.Owner, useQuotedString)}.{this.GetString(obj.Name, useQuotedString)}";
            }
            return $"{this.GetString(obj.Name, useQuotedString)}";
        }

        private string GetString(string str, bool useQuotedString = false)
        {
            return useQuotedString ? this.GetQuotedString(str) : str;
        }

        public abstract DbConnector GetDbConnector();
        public string GetQuotedObjectName(DatabaseObject obj)
        {
            return this.GetObjectDisplayName(obj, true);
        }
        protected string GetQuotedColumnNames(IEnumerable<TableColumn> columns)
        {
            return string.Join(",", columns.OrderBy(item => item.Order).Select(item => this.GetQuotedString(item.Name)));
        }

        public string GetQuotedString(string str)
        {
            if (this.DbObjectNameMode == DbObjectNameMode.WithQuotation || (str != null && str.Contains(" ")))
            {
                return $"{ this.QuotationLeftChar}{str}{this.QuotationRightChar}";
            }
            else
            {
                return str;
            }
        }

        protected bool IsObjectFectchSimpleMode()
        {
            return this.Option.ObjectFetchMode == DatabaseObjectFetchMode.Simple;
        }

        protected string ReplaceSplitChar(string value)
        {
            return value?.Replace(this.ScriptsDelimiter, ",");
        }

        protected async Task<List<T>> GetDbObjectsAsync<T>(string sql) where T : DatabaseObject
        {
            if (!string.IsNullOrEmpty(sql))
            {
                using (DbConnection dbConnection = this.CreateConnection())
                {
                    return await this.GetDbObjectsAsync<T>(dbConnection, sql);
                }
            }

            return new List<T>();
        }

        protected async Task<List<T>> GetDbObjectsAsync<T>(DbConnection dbConnection, string sql) where T : DatabaseObject
        {
            List<T> objects = new List<T>();

            if (!string.IsNullOrEmpty(sql))
            {
                try
                {
                    DbCommand dbCommand = dbConnection.CreateCommand();

                    dbCommand.CommandText = sql;
                    dbCommand.CommandType = CommandType.Text;

                    await this.OpenConnectionAsync(dbConnection);

                    objects = (await dbConnection.QueryAsync<T>(sql)).ToList();

                    bool isAllOrdersIsZero = !objects.Any(item => item.Order != 0);

                    if (isAllOrdersIsZero)
                    {
                        int i = 1;
                        objects.ForEach(item =>
                        {
                            item.Order = i++;
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (this.Option.ThrowExceptionWhenErrorOccurs)
                    {
                        throw ex;
                    }

                    this.FeedbackError(ExceptionHelper.GetExceptionDetails(ex));
                }
            }

            this.FeedbackInfo($"Got {objects.Count} {StringHelper.GetFriendlyTypeName(typeof(T).Name).ToLower()}(s).");

            return objects;
        }
        #endregion

        #region Feedback
        public void Subscribe(IObserver<FeedbackInfo> observer)
        {
            this.observer = observer;
        }

        public void Feedback(FeedbackInfoType infoType, string message)
        {
            FeedbackInfo info = new FeedbackInfo() { Owner = this, InfoType = infoType, Message = StringHelper.ToSingleEmptyLine(message) };

            if (this.observer != null)
            {
                FeedbackHelper.Feedback(this.observer, info);
            }
        }

        public void FeedbackInfo(string message)
        {
            this.Feedback(FeedbackInfoType.Info, message);
        }

        public void FeedbackError(string message, bool skipError = false)
        {
            if (!skipError)
            {
                this.hasError = true;
            }

            this.Feedback(FeedbackInfoType.Error, message);
        }

        public void FeedbackInfo(OperationState state, DatabaseObject dbObject)
        {
            string message = $"{state.ToString()}{(state == OperationState.Begin ? " to" : "")} generate script for { StringHelper.GetFriendlyTypeName(dbObject.GetType().Name).ToLower() } \"{dbObject.Name}\".";
            this.Feedback(FeedbackInfoType.Info, message);
        }
        #endregion

        #region Schema Informatioin
        #region Database
        public abstract Task<List<Database>> GetDatabasesAsync();
        #endregion

        #region User Defined Type     
        public abstract Task<List<UserDefinedType>> GetUserDefinedTypesAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<UserDefinedType>> GetUserDefinedTypesAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);

        #endregion

        #region Function        
        public abstract Task<List<Function>> GetFunctionsAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<Function>> GetFunctionsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);
        #endregion

        #region Table      
        public abstract Task<List<Table>> GetTablesAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<Table>> GetTablesAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);
        #endregion

        #region Table Column
        public abstract Task<List<TableColumn>> GetTableColumnsAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<TableColumn>> GetTableColumnsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);
        #endregion

        #region Table Primary Key
        public abstract Task<List<TablePrimaryKey>> GetTablePrimaryKeysAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<TablePrimaryKey>> GetTablePrimaryKeysAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);
        #endregion

        #region Table Foreign Key
        public abstract Task<List<TableForeignKey>> GetTableForeignKeysAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<TableForeignKey>> GetTableForeignKeysAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);
        #endregion

        #region Table Index
        public abstract Task<List<TableIndex>> GetTableIndexesAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<TableIndex>> GetTableIndexesAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);

        #endregion

        #region Table Trigger        
        public abstract Task<List<TableTrigger>> GetTableTriggersAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<TableTrigger>> GetTableTriggersAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);
        #endregion

        #region Table Constraint
        public abstract Task<List<TableConstraint>> GetTableConstraintsAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<TableConstraint>> GetTableConstraintsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);

        #endregion

        #region View        
        public abstract Task<List<View>> GetViewsAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<View>> GetViewsAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);
        #endregion     

        #region Procedure        
        public abstract Task<List<Procedure>> GetProceduresAsync(SchemaInfoFilter filter = null);
        public abstract Task<List<Procedure>> GetProceduresAsync(DbConnection dbConnection, SchemaInfoFilter filter = null);
        #endregion

        #region SchemaInfo
        public virtual async Task<SchemaInfo> GetSchemaInfoAsync(SchemaInfoFilter filter)
        {
            if (filter == null)
            {
                filter = new SchemaInfoFilter();
            }

            this.FeedbackInfo("Getting schema info...");

            DatabaseObjectType dbObjectType = filter.DatabaseObjectType;

            SchemaInfo schemaInfo = new SchemaInfo();

            using (DbConnection connection = this.CreateConnection())
            {
                if (this.NeedFetchObjects(DatabaseObjectType.UserDefinedType, filter.UserDefinedTypeNames, filter))
                {
                    schemaInfo.UserDefinedTypes = await this.GetUserDefinedTypesAsync(connection, filter);
                }

                if (this.NeedFetchObjects(DatabaseObjectType.Function, filter.FunctionNames, filter))
                {
                    schemaInfo.Functions = await this.GetFunctionsAsync(connection, filter);
                }

                if (this.NeedFetchObjects(DatabaseObjectType.Table, filter.TableNames, filter))
                {
                    schemaInfo.Tables = await this.GetTablesAsync(connection, filter);
                }

                if (this.NeedFetchObjects(DatabaseObjectType.View, filter.ViewNames, filter))
                {
                    schemaInfo.Views = await this.GetViewsAsync(connection, filter);
                }

                if (this.NeedFetchObjects(DatabaseObjectType.Procedure, filter.ProcedureNames, filter))
                {
                    schemaInfo.Procedures = await this.GetProceduresAsync(connection, filter);
                }

                if (this.NeedFetchTableObjects(DatabaseObjectType.TableColumn, filter, null))
                {
                    schemaInfo.TableColumns = await this.GetTableColumnsAsync(connection, filter);
                }

                if (this.NeedFetchTableObjects(DatabaseObjectType.TablePrimaryKey, filter, null))
                {
                    schemaInfo.TablePrimaryKeys = await this.GetTablePrimaryKeysAsync(connection, filter);
                }

                if ((this.Option.SortObjectsByReference && schemaInfo.Tables.Count > 1) || this.NeedFetchTableObjects(DatabaseObjectType.TableForeignKey, filter, null))
                {
                    schemaInfo.TableForeignKeys = await this.GetTableForeignKeysAsync(connection, filter);
                }

                if (this.NeedFetchTableObjects(DatabaseObjectType.TableIndex, filter, null))
                {
                    schemaInfo.TableIndexes = await this.GetTableIndexesAsync(connection, filter);
                }

                if (this.NeedFetchTableObjects(DatabaseObjectType.TableConstraint, filter, null))
                {
                    schemaInfo.TableConstraints = await this.GetTableConstraintsAsync(connection, filter);
                }

                if (this.NeedFetchTableObjects(DatabaseObjectType.TableTrigger, filter, filter.TableTriggerNames))
                {
                    schemaInfo.TableTriggers = await this.GetTableTriggersAsync(connection, filter);
                }
            }

            if (this.Option.SortObjectsByReference)
            {
                if (schemaInfo.Tables.Count > 1)
                {
                    schemaInfo.Tables = TableReferenceHelper.ResortTables(schemaInfo.Tables, schemaInfo.TableForeignKeys);
                }

                DbObjectHelper.Resort(schemaInfo.Views);
                DbObjectHelper.Resort(schemaInfo.Functions);
                DbObjectHelper.Resort(schemaInfo.Procedures);
            }

            this.FeedbackInfo("End get schema info.");

            return schemaInfo;
        }

        private bool NeedFetchTableObjects(DatabaseObjectType currentObjectType, SchemaInfoFilter filter, string[] childrenNames)
        {
            var filterNames = (filter.TableNames ?? Enumerable.Empty<string>()).Union(childrenNames ?? Enumerable.Empty<string>());

            return this.Option.GetTableAllObjects || this.NeedFetchObjects(currentObjectType, filterNames, filter);
        }

        private bool NeedFetchObjects(DatabaseObjectType currentObjectType, IEnumerable<string> names, SchemaInfoFilter filter)
        {
            bool hasName = names != null && names.Any();

            if (filter.Strict)
            {
                return hasName && filter.DatabaseObjectType.HasFlag(currentObjectType);
            }
            else
            {
                return hasName || filter.DatabaseObjectType.HasFlag(currentObjectType);
            }
        }
        #endregion
        #endregion

        #region Database Operation

        public virtual async Task SetIdentityEnabled(DbConnection dbConnection, TableColumn column, bool enabled) { await Task.Run(() => { }); }

        public Task<int> ExecuteNonQueryAsync(string sql)
        {
            return this.InternalExecuteNonQuery(this.CreateConnection(), new CommandInfo() { CommandText = sql });
        }

        public Task<int> ExecuteNonQueryAsync(CommandInfo commandInfo)
        {
            return this.InternalExecuteNonQuery(this.CreateConnection(), commandInfo, true);
        }

        public Task<int> ExecuteNonQueryAsync(DbConnection dbConnection, string sql, bool disposeConnection = false)
        {
            return this.InternalExecuteNonQuery(dbConnection, new CommandInfo() { CommandText = sql }, disposeConnection);
        }

        public Task<int> ExecuteNonQueryAsync(DbConnection dbConnection, CommandInfo commandInfo)
        {
            return this.InternalExecuteNonQuery(dbConnection, commandInfo, false);
        }

        protected async Task<int> InternalExecuteNonQuery(DbConnection dbConnection, CommandInfo commandInfo, bool disposeConnection = true)
        {
            if (this.CancelRequested || this.hasError)
            {
                return 0;
            }

            DbCommand command = dbConnection.CreateCommand();
            command.CommandType = commandInfo.CommandType;
            command.CommandText = commandInfo.CommandText;
            command.CommandTimeout = SettingManager.Setting.CommandTimeout;

            if (this.Option.RequireInfoMessage)
            {
                this.SubscribeInfoMessage(command);
            }

            if (commandInfo.Transaction != null)
            {
                command.Transaction = commandInfo.Transaction;
            }

            if (commandInfo.Parameters != null)
            {
                foreach (var kp in commandInfo.Parameters)
                {
                    DbParameter dbParameter = command.CreateParameter();
                    dbParameter.ParameterName = kp.Key;
                    dbParameter.Value = kp.Value;

                    command.Parameters.Add(dbParameter);
                }
            }

            Func<Task<int>> exec = async () =>
            {
                bool wasClosed = dbConnection.State == ConnectionState.Closed;

                try
                {
                    if (wasClosed)
                    {
                        await dbConnection.OpenAsync(commandInfo.CancellationToken).ConfigureAwait(false);
                    }

                    int result = await command.ExecuteNonQueryAsync(commandInfo.CancellationToken).ConfigureAwait(false);

                    return result;
                }
                catch (Exception ex)
                {
                    if (!commandInfo.SkipError)
                    {
                        if (dbConnection.State == ConnectionState.Open && command.Transaction != null)
                        {
                            command.Transaction.Rollback();
                        }
                    }

                    if (this.Option.ThrowExceptionWhenErrorOccurs)
                    {
                        throw ex;
                    }

                    this.FeedbackError(ExceptionHelper.GetExceptionDetails(ex), commandInfo.SkipError);

                    return 0;
                }
                finally
                {
                    if (disposeConnection && wasClosed && dbConnection != null && dbConnection.State != ConnectionState.Closed)
                    {
                        dbConnection.Close();
                    }
                }
            };

            if (disposeConnection)
            {
                using (dbConnection)
                {
                    return await exec();
                }
            }
            else
            {
                return await exec();
            }
        }

        public abstract Task BulkCopyAsync(DbConnection connection, DataTable dataTable, BulkCopyInfo bulkCopyInfo);

        protected async Task<object> GetScalarAsync(DbConnection dbConnection, string sql)
        {
            return await dbConnection.ExecuteScalarAsync(sql);
        }

        protected async Task<DbDataReader> GetDataReaderAsync(string sql)
        {
            return await this.CreateConnection().ExecuteReaderAsync(sql);
        }

        protected async Task<DbDataReader> GetDataReaderAsync(DbConnection dbConnection, string sql)
        {
            return await dbConnection.ExecuteReaderAsync(sql);
        }

        public async Task<DataTable> GetDataTableAsync(DbConnection dbConnection, string sql, int? limitCount = null)
        {
            if (limitCount > 0)
            {
                sql = this.AppendLimitClause(sql, limitCount.Value);
            }

            DbDataReader reader = await dbConnection.ExecuteReaderAsync(sql);

            DataTable table = new DataTable();
            table.Load(reader);

            return table;
        }

        protected string AppendLimitClause(string sql, int limitCount)
        {
            sql = sql.TrimEnd(';', '\r', '\n');

            int index = sql.LastIndexOf(')');

            string select = index > 0 ? sql.Substring(index + 1) : sql;

            if (!Regex.IsMatch(select, @"(LIMIT|OFFSET)(.[\n]?)+", RegexOptions.IgnoreCase))
            {
                if (!Regex.IsMatch(select, @"ORDER[\s]+BY", RegexOptions.IgnoreCase))
                {
                    sql += Environment.NewLine + "ORDER BY " + this.GetDefaultOrder();
                }

                if (this.DatabaseType == DatabaseType.SqlServer)
                {
                    if (!Regex.IsMatch(select, @"SELECT[\s]+TOP[\s]+", RegexOptions.IgnoreCase))
                    {
                        sql = sql.Substring(0, index + 1) + Regex.Replace(select, "SELECT", $"SELECT TOP {limitCount} ", RegexOptions.IgnoreCase);
                    }
                }
                else
                {
                    sql += Environment.NewLine + this.GetLimitStatement(0, limitCount);
                }
            }

            return sql;
        }

        public abstract Task SetConstrainsEnabled(bool enabled);
        public abstract Task SetConstrainsEnabled(DbConnection dbConnection, bool enabled);

        public async Task Drop<T>(T dbObjet) where T : DatabaseObject
        {
            await this.Drop<T>(this.CreateConnection(), dbObjet);
        }

        public abstract Task Drop<T>(DbConnection dbConnection, T dbObjet) where T : DatabaseObject;
        #endregion

        #region Generate Scripts     
        public virtual string GetLimitStatement(int limitStart, int limitCount) { return string.Empty; }
        public virtual string GetDefaultOrder() { return string.Empty; }
        public abstract ScriptBuilder GenerateSchemaScripts(SchemaInfo schemaInfo);
        public abstract string ParseColumn(Table table, TableColumn column);
        public abstract string ParseDataType(TableColumn column);

        protected virtual List<Script> GenerateScriptDbObjectScripts<T>(List<T> dbObjects)
            where T : ScriptDbObject
        {
            List<Script> scripts = new List<Script>();

            foreach (T dbObject in dbObjects)
            {
                this.FeedbackInfo(OperationState.Begin, dbObject);

                bool hasNewLine = this.ScriptsDelimiter.Contains(Environment.NewLine);

                scripts.Add(new CreateDbObjectScript<T>(dbObject.Definition.Trim()));

                if (!hasNewLine)
                {
                    scripts.Add(new SpliterScript(this.ScriptsDelimiter));
                }
                else
                {
                    scripts.Add(new NewLineSript());
                    scripts.Add(new SpliterScript(this.ScriptsDelimiter));
                }

                scripts.Add(new NewLineSript());

                this.FeedbackInfo(OperationState.End, dbObject);
            }

            return scripts;
        }

        public abstract Task<long> GetTableRecordCountAsync(DbConnection connection, Table table, string whereClause = "");

        protected async Task<long> GetTableRecordCountAsync(DbConnection connection, string sql)
        {
            return await connection.ExecuteScalarAsync<long>(sql);
        }

        public virtual async Task<string> GenerateDataScriptsAsync(SchemaInfo schemaInfo)
        {
            StringBuilder sb = new StringBuilder();

            if (this.Option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile))
            {
                this.AppendScriptsToFile("", GenerateScriptMode.Data, true);
            }

            int i = 0;
            int pickupIndex = -1;
            if (schemaInfo.PickupTable != null)
            {
                foreach (Table table in schemaInfo.Tables)
                {
                    if (table.Owner == schemaInfo.PickupTable.Owner && table.Name == schemaInfo.PickupTable.Name)
                    {
                        pickupIndex = i;
                        break;
                    }
                    i++;
                }
            }

            i = 0;
            using (DbConnection connection = this.CreateConnection())
            {
                int tableCount = schemaInfo.Tables.Count - (pickupIndex == -1 ? 0 : pickupIndex);
                int count = 0;

                foreach (Table table in schemaInfo.Tables)
                {
                    if (this.CancelRequested)
                    {
                        break;
                    }

                    if (i < pickupIndex)
                    {
                        i++;
                        continue;
                    }

                    count++;

                    string strTableCount = $"({count}/{tableCount})";
                    string tableName = table.Name;

                    List<TableColumn> columns = schemaInfo.TableColumns.Where(item => item.Owner == table.Owner && item.TableName == tableName).OrderBy(item => item.Order).ToList();

                    bool isSelfReference = TableReferenceHelper.IsSelfReference(tableName, schemaInfo.TableForeignKeys);

                    List<TablePrimaryKey> primaryKeys = schemaInfo.TablePrimaryKeys.Where(item => item.Owner == table.Owner && item.TableName == tableName).ToList();
                    string primaryKeyColumns = string.Join(",", primaryKeys.OrderBy(item => item.Order).Select(item => this.GetQuotedString(item.ColumnName)));

                    long total = await this.GetTableRecordCountAsync(connection, table);

                    if (this.Option.DataGenerateThreshold.HasValue && total > this.Option.DataGenerateThreshold.Value)
                    {
                        continue;
                    }

                    int pageSize = this.DataBatchSize;

                    this.FeedbackInfo($"{strTableCount}Table \"{table.Name}\":record count is {total}.");

                    Dictionary<long, List<Dictionary<string, object>>> dictPagedData;

                    if (isSelfReference)
                    {
                        string parentColumnName = schemaInfo.TableForeignKeys.FirstOrDefault(item =>
                            item.Owner == table.Owner
                            && item.TableName == tableName
                            && item.ReferencedTableName == tableName)?.ColumnName;

                        string strWhere = $" WHERE {GetQuotedString(parentColumnName)} IS NULL";
                        dictPagedData = await this.GetSortedPageData(connection, table, primaryKeyColumns, parentColumnName, columns, strWhere);
                    }
                    else
                    {
                        dictPagedData = await this.GetPagedDataListAsync(connection, table, columns, primaryKeyColumns, total, pageSize);
                    }

                    this.FeedbackInfo($"{strTableCount}Table \"{table.Name}\":data read finished.");

                    this.AppendDataScripts(sb, table, columns, dictPagedData);

                    i++;
                }
            }

            var dataScripts = string.Empty;
            try
            {
                dataScripts = sb.ToString();
            }
            catch (OutOfMemoryException ex)
            {
                this.FeedbackError("Exception occurs:" + ex.Message);
            }
            finally
            {
                sb.Clear();
            }
            return dataScripts;
        }

        private async Task<Dictionary<long, List<Dictionary<string, object>>>> GetSortedPageData(DbConnection connection, Table table, string primaryKeyColumns, string parentColumnName, List<TableColumn> columns, string whereClause = "")
        {
            string quotedTableName = this.GetQuotedObjectName(table);

            int pageSize = this.DataBatchSize;

            long total = Convert.ToInt64(await this.GetScalarAsync(connection, $"SELECT COUNT(1) FROM {quotedTableName} {whereClause}"));

            var dictPagedData = await this.GetPagedDataListAsync(connection, table, columns, primaryKeyColumns, total, pageSize, whereClause);

            List<object> parentValues = dictPagedData.Values.SelectMany(item => item.Select(t => t[primaryKeyColumns.Trim(this.QuotationLeftChar, this.QuotationRightChar)])).ToList();

            if (parentValues.Count > 0)
            {
                TableColumn parentColumn = columns.FirstOrDefault(item => item.Owner == table.Owner && item.Name == parentColumnName);

                long parentValuesPageCount = PaginationHelper.GetPageCount(parentValues.Count, this.Option.InQueryItemLimitCount);

                for (long parentValuePageNumber = 1; parentValuePageNumber <= parentValuesPageCount; parentValuePageNumber++)
                {
                    IEnumerable<object> pagedParentValues = parentValues.Skip((int)(parentValuePageNumber - 1) * pageSize).Take(this.Option.InQueryItemLimitCount);
                    whereClause = $" WHERE {GetQuotedString(parentColumnName)} IN ({string.Join(",", pagedParentValues.Select(item => ParseValue(parentColumn, item, true)))})";
                    total = Convert.ToInt64(await this.GetScalarAsync(connection, $"SELECT COUNT(1) FROM {quotedTableName} {whereClause}"));

                    if (total > 0)
                    {
                        Dictionary<long, List<Dictionary<string, object>>> dictChildPagedData = await this.GetSortedPageData(connection, table, primaryKeyColumns, parentColumnName, columns, whereClause);

                        foreach (var kp in dictChildPagedData)
                        {
                            long pageNumber = dictPagedData.Keys.Max(item => item);
                            dictPagedData.Add(pageNumber + 1, kp.Value);
                        }
                    }
                }
            }

            return dictPagedData;
        }

        private async Task<Dictionary<long, List<Dictionary<string, object>>>> GetPagedDataListAsync(DbConnection connection, Table table, List<TableColumn> columns, string primaryKeyColumns, long total, int pageSize, string whereClause = "")
        {
            var dictPagedData = new Dictionary<long, List<Dictionary<string, object>>>();

            long pageCount = PaginationHelper.GetPageCount(total, pageSize);

            for (long pageNumber = 1; pageNumber <= pageCount; pageNumber++)
            {
                if (this.CancelRequested)
                {
                    break;
                }

                DataTable dataTable = await this.GetPagedDataTableAsync(connection, table, columns, primaryKeyColumns, total, pageSize, pageNumber, whereClause);

                List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();

                foreach (DataRow row in dataTable.Rows)
                {
                    var dicField = new Dictionary<string, object>();

                    for (var i = 0; i < dataTable.Columns.Count; i++)
                    {
                        DataColumn column = dataTable.Columns[i];
                        string columnName = column.ColumnName;

                        if (columnName == RowNumberColumnName)
                        {
                            continue;
                        }

                        TableColumn tableColumn = columns.FirstOrDefault(item => item.Name == columnName);

                        object value = row[i];

                        if (this.IsBytes(value))
                        {
                            if (this.Option.TreatBytesAsNullForReading)
                            {
                                value = null;
                            }
                        }

                        dicField.Add(columnName, value);
                    }

                    rows.Add(dicField);
                }

                dictPagedData.Add(pageNumber, rows);                

                if (this.OnDataRead != null && !this.CancelRequested && !this.hasError)
                {
                    await this.OnDataRead(table, columns, rows, dataTable);
                }
            }

            return dictPagedData;
        }

        public async Task<DataTable> GetPagedDataTableAsync(DbConnection connection, Table table, List<TableColumn> columns, string orderColumns, long total, int pageSize, long pageNumber, string whereClause = "")
        {
            string quotedTableName = this.GetQuotedObjectName(table);
            string columnNames = this.GetQuotedColumnNames(columns);

            string pagedSql = this.GetSqlForPagination(quotedTableName, columnNames, orderColumns, whereClause, pageNumber, pageSize);

            DataTable dt = await this.GetDataTableAsync(connection, pagedSql);

            if (dt.Columns.OfType<DataColumn>().Any(item => item.ColumnName == RowNumberColumnName))
            {
                dt.Columns.Remove(RowNumberColumnName);
            }

            return dt;
        }

        public async Task<(long, DataTable)> GetPagedDataTableAsync(Table table, string orderColumns, int pageSize, long pageNumber, string whereClause = "")
        {
            using (DbConnection connection = this.CreateConnection())
            {
                long total = await this.GetTableRecordCountAsync(connection, table, whereClause);

                List<TableColumn> columns = await this.GetTableColumnsAsync(connection, new SchemaInfoFilter() { TableNames = new string[] { table.Name } });

                DataTable dt = await this.GetPagedDataTableAsync(this.CreateConnection(), table, columns, orderColumns, total, pageSize, pageNumber, whereClause);

                return (total, dt);
            }
        }

        protected abstract string GetSqlForPagination(string tableName, string columnNames, string orderColumns, string whereClause, long pageNumber, int pageSize);


        public virtual Dictionary<string, object> AppendDataScripts(StringBuilder sb, Table table, List<TableColumn> columns, Dictionary<long, List<Dictionary<string, object>>> dictPagedData)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            bool appendString = this.Option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToString);
            bool appendFile = this.Option.ScriptOutputMode.HasFlag(GenerateScriptOutputMode.WriteToFile);

            List<string> excludeColumnNames = new List<string>();

            if (this.Option.TableScriptsGenerateOption.GenerateIdentity && !this.Option.InsertIdentityValue)
            {
                excludeColumnNames.AddRange(columns.Where(item => item.IsIdentity).Select(item => item.Name));
            }

            excludeColumnNames.AddRange(columns.Where(item => item.IsComputed).Select(item => item.Name));

            foreach (var kp in dictPagedData)
            {
                StringBuilder sbFilePage = new StringBuilder(Environment.NewLine);

                string tableName = this.GetQuotedObjectName(table);
                string insert = $"{this.GetBatchInsertPrefix()} {tableName}({this.GetQuotedColumnNames(columns.Where(item => !excludeColumnNames.Contains(item.Name)))})VALUES";

                if (appendString)
                {
                    sb.AppendLine(insert);
                }

                if (appendFile)
                {
                    sbFilePage.AppendLine(insert);
                }

                int rowCount = 0;
                foreach (var row in kp.Value)
                {
                    rowCount++;

                    var rowValues = this.GetRowValues(row, rowCount - 1, columns, excludeColumnNames, kp.Key, false, out var insertParameters);

                    string values = $"({string.Join(",", rowValues.Select(item => item))})";

                    if (insertParameters != null)
                    {
                        foreach (var para in insertParameters)
                        {
                            parameters.Add(para.Key, para.Value);
                        }
                    }

                    string beginChar = this.GetBatchInsertItemBefore(tableName, rowCount == 1);
                    string endChar = this.GetBatchInsertItemEnd(rowCount == kp.Value.Count);

                    values = $"{beginChar}{values}{endChar}";

                    if (this.Option.RemoveEmoji)
                    {
                        values = StringHelper.RemoveEmoji(values);
                    }

                    if (appendString)
                    {
                        sb.AppendLine(values);
                    }

                    if (appendFile)
                    {
                        var fileRowValues = this.GetRowValues(row, rowCount - 1, columns, excludeColumnNames, kp.Key, true, out var _);
                        string fileValues = $"({string.Join(",", fileRowValues.Select(item => item))})";

                        sbFilePage.AppendLine($"{beginChar}{fileValues}{endChar}");
                    }
                }

                if (appendString)
                {
                    sb.AppendLine();
                }

                if (appendFile)
                {
                    sbFilePage.AppendLine();

                    this.AppendScriptsToFile(sbFilePage.ToString(), GenerateScriptMode.Data);
                }
            }

            return parameters;
        }

        protected virtual string GetBatchInsertPrefix()
        {
            return "INSERT INTO";
        }

        protected virtual string GetBatchInsertItemBefore(string tableName, bool isFirstRow)
        {
            return "";
        }

        protected virtual string GetBatchInsertItemEnd(bool isAllEnd)
        {
            return (isAllEnd ? ";" : ",");
        }

        public string GetScriptOutputFilePath(GenerateScriptMode generateScriptMode)
        {
            string fileName = $"{this.ConnectionInfo.Database}_{this.GetType().Name.Replace("Interpreter", "")}_{DateTime.Today.ToString("yyyyMMdd")}_{generateScriptMode.ToString()}.sql";
            string filePath = Path.Combine(this.Option.ScriptOutputFolder, fileName);
            return filePath;
        }

        public virtual void AppendScriptsToFile(string content, GenerateScriptMode generateScriptMode, bool clearAll = false)
        {
            if (generateScriptMode == GenerateScriptMode.Schema)
            {
                content = StringHelper.ToSingleEmptyLine(content);
            }

            string filePath = this.GetScriptOutputFilePath(generateScriptMode);

            string directoryName = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            if (!clearAll)
            {
                File.AppendAllText(filePath, content, Encoding.UTF8);
            }
            else
            {
                File.WriteAllText(filePath, content, Encoding.UTF8);
            }
        }

        private List<object> GetRowValues(Dictionary<string, object> row, int rowIndex, List<TableColumn> columns, List<string> excludeColumnNames, long pageNumber, bool isAppendToFile, out Dictionary<string, object> parameters)
        {
            parameters = new Dictionary<string, object>();

            List<object> values = new List<object>();

            foreach (TableColumn column in columns)
            {
                if (!excludeColumnNames.Contains(column.Name))
                {
                    object value = this.ParseValue(column, row[column.Name]);
                    bool isBytes = this.IsBytes(value);

                    if (!isAppendToFile)
                    {
                        if ((isBytes && !this.Option.TreatBytesAsNullForExecuting) || this.NeedInsertParameter(column, value))
                        {
                            string parameterName = $"P{pageNumber}_{rowIndex}_{column.Name}";

                            string parameterPlaceholder = this.CommandParameterChar + parameterName;

                            parameters.Add(parameterPlaceholder, value);

                            value = parameterPlaceholder;
                        }
                        else if (isBytes && this.Option.TreatBytesAsNullForExecuting)
                        {
                            value = null;
                        }
                    }
                    else
                    {
                        if (isBytes)
                        {
                            if (this.Option.TreatBytesAsHexStringForFile)
                            {
                                value = this.GetBytesConvertHexString(value, column.DataType);
                            }
                            else
                            {
                                value = null;
                            }
                        }
                    }

                    values.Add(value);
                }
            }

            return values;
        }

        public bool IsBytes(object value)
        {
            return (value != null && value.GetType() == typeof(byte[]));
        }

        protected virtual bool NeedInsertParameter(TableColumn column, object value)
        {
            return false;
        }

        protected virtual string GetBytesConvertHexString(object value, string dataType)
        {
            return null;
        }

        private object ParseValue(TableColumn column, object value, bool byteAsString = false)
        {
            if (value != null)
            {
                Type type = value.GetType();
                bool needQuotated = false;
                string strValue = "";

                if (type == typeof(DBNull))
                {
                    return "NULL";
                }
                else if (type == typeof(Byte[]))
                {
                    if (((Byte[])value).Length == 16) //GUID
                    {
                        if (this.GetType() == typeof(SqlServerInterpreter) && column.DataType.ToLower() == "uniqueidentifier")
                        {
                            needQuotated = true;
                            strValue = new Guid((byte[])value).ToString();
                        }
                        else if (this.GetType() == typeof(MySqlInterpreter) && column.DataType == "char" && column.MaxLength == 36)
                        {
                            needQuotated = true;
                            strValue = new Guid((byte[])value).ToString();
                        }
                        else if (byteAsString && this.GetType() == typeof(OracleInterpreter) && column.DataType.ToLower() == "raw" && column.MaxLength == 16)
                        {
                            needQuotated = true;
                            strValue = StringHelper.GuidToRaw(new Guid((byte[])value).ToString());
                        }
                        else
                        {
                            return value;
                        }
                    }
                    else
                    {
                        return value;
                    }
                }

                bool oracleSemicolon = false;

                switch (type.Name)
                {
                    case nameof(Guid):
                        needQuotated = true;
                        if (this.GetType() == typeof(OracleInterpreter) && column.DataType.ToLower() == "raw" && column.MaxLength == 16)
                        {
                            strValue = StringHelper.GuidToRaw(value.ToString());
                        }
                        else
                        {
                            strValue = value.ToString();
                        }
                        break;
                    case nameof(String):
                        needQuotated = true;
                        strValue = value.ToString();
                        if (this.GetType() == typeof(OracleInterpreter))
                        {
                            if (strValue.Contains(";"))
                            {
                                oracleSemicolon = true;
                            }
                        }
                        break;
                    case nameof(DateTime):
                    case nameof(DateTimeOffset):
                    case nameof(MySql.Data.Types.MySqlDateTime):
                        if (this.GetType() == typeof(OracleInterpreter))
                        {
                            if (type.Name == nameof(MySql.Data.Types.MySqlDateTime))
                            {
                                DateTime dateTime = ((MySql.Data.Types.MySqlDateTime)value).GetDateTime();

                                strValue = this.GetOracleDatetimeConvertString(dateTime);
                            }
                            else if (type.Name == nameof(DateTime))
                            {
                                DateTime dateTime = Convert.ToDateTime(value);

                                strValue = this.GetOracleDatetimeConvertString(dateTime);
                            }
                            else if (type.Name == nameof(DateTimeOffset))
                            {
                                DateTimeOffset dtOffset = DateTimeOffset.Parse(value.ToString());
                                int millisecondLength = dtOffset.Millisecond.ToString().Length;
                                string strMillisecond = millisecondLength == 0 ? "" : $".{"f".PadLeft(millisecondLength, 'f')}";
                                string format = $"yyyy-MM-dd HH:mm:ss{strMillisecond}";

                                string strDtOffset = dtOffset.ToString(format) + $"{dtOffset.Offset.Hours}:{dtOffset.Offset.Minutes}";

                                strValue = $@"TO_TIMESTAMP_TZ('{strDtOffset}','yyyy-MM-dd HH24:MI:ssxff TZH:TZM')";
                            }
                        }
                        else if (this.GetType() == typeof(MySqlInterpreter))
                        {
                            if (type.Name == nameof(DateTimeOffset))
                            {
                                DateTimeOffset dtOffset = DateTimeOffset.Parse(value.ToString());

                                strValue = $"'{dtOffset.DateTime.Add(dtOffset.Offset).ToString("yyyy-MM-dd HH:mm:ss.ffffff")}'";
                            }
                        }

                        if (string.IsNullOrEmpty(strValue))
                        {
                            needQuotated = true;
                            strValue = value.ToString();
                        }
                        break;
                    case nameof(Boolean):
                        strValue = value.ToString() == "True" ? "1" : "0";
                        break;
                    case nameof(TimeSpan):
                        if (this.GetType() == typeof(OracleInterpreter))
                        {
                            return value;
                        }
                        else
                        {
                            needQuotated = true;

                            if (column.DataType.IndexOf("datetime", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                DateTime dateTime = this.minDateTime.AddSeconds(TimeSpan.Parse(value.ToString()).TotalSeconds);

                                strValue = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                            }
                            else
                            {
                                strValue = value.ToString();
                            }
                        }
                        break;
                    case "SqlHierarchyId":
                    case "SqlGeography":
                    case "SqlGeometry":
                        needQuotated = true;
                        strValue = value.ToString();
                        break;
                    default:
                        if (string.IsNullOrEmpty(strValue))
                        {
                            strValue = value.ToString();
                        }
                        break;
                }

                if (needQuotated)
                {
                    strValue = $"{this.UnicodeInsertChar}'{ValueHelper.TransferSingleQuotation(strValue)}'";

                    if (oracleSemicolon)
                    {
                        strValue = strValue.Replace(";", $"'{OracleInterpreter.CONNECT_CHAR}{OracleInterpreter.SEMICOLON_FUNC}{OracleInterpreter.CONNECT_CHAR}'");
                    }

                    return strValue;
                }
                else
                {
                    return strValue;
                }
            }
            else
            {
                return null;
            }
        }

        private string GetOracleDatetimeConvertString(DateTime dateTime)
        {
            int millisecondLength = dateTime.Millisecond.ToString().Length;
            string strMillisecond = millisecondLength == 0 ? "" : $".{"f".PadLeft(millisecondLength, 'f')}";
            string format = $"yyyy-MM-dd HH:mm:ss{strMillisecond}";

            return $"TO_TIMESTAMP('{dateTime.ToString(format)}','yyyy-MM-dd hh24:mi:ssxff')";
        }

        protected virtual string GetUnicodeInsertChar()
        {
            return this.UnicodeInsertChar;
        }

        protected virtual string GetColumnDefaultValue(TableColumn column)
        {
            bool isChar = DataTypeHelper.IsCharType(column.DataType);
            if (isChar && !column.DefaultValue.Trim('(', ')').StartsWith("'"))
            {
                return $"'{column.DefaultValue}'";
            }
            return column.DefaultValue?.Trim();
        }

        protected virtual string GetColumnComputeExpression(TableColumn column)
        {
            string computeExpression = column.ComputeExp.Trim();

            if (!computeExpression.StartsWith("(") && !computeExpression.EndsWith(")"))
            {
                computeExpression = $"({computeExpression})";
            }

            return computeExpression;
        }

        public virtual bool IsNoLengthDataType(string dataType)
        {
            IEnumerable<DataTypeSpecification> dataTypeSpecs = DataTypeManager.GetDataTypeSpecifications(this.DatabaseType);

            return dataTypeSpecs.Any(item => item.Name.ToUpper() == dataType.ToUpper() && string.IsNullOrEmpty(item.Args));
        }

        public DataTypeSpecification GetDataTypeSpecification(string dataType)
        {
            return DataTypeManager.GetDataTypeSpecifications(this.DatabaseType).FirstOrDefault(item => item.Name.ToLower() == dataType.ToLower().Trim());
        }

        public string GetDataTypePrecisionScale(TableColumn column, string dataType)
        {
            DataTypeSpecification dataTypeSpecification = this.GetDataTypeSpecification(dataType);

            if (dataTypeSpecification != null)
            {
                long precision = column.Precision.HasValue ? column.Precision.Value : 0;
                int scale = column.Scale.HasValue ? column.Scale.Value : 0;

                if (dataTypeSpecification.Args.Contains(","))
                {
                    if (precision > 0)
                    {
                        return $"({precision},{scale})";
                    }
                }
                else if (dataTypeSpecification.Args == "scale")
                {
                    ArgumentRange? range = DataTypeManager.GetArgumentRange(dataTypeSpecification, "scale");

                    if (range.HasValue)
                    {
                        if (scale > range.Value.Max)
                        {
                            scale = range.Value.Max;
                        }
                    }

                    return $"({scale})";
                }
            }

            return string.Empty;
        }
        #endregion
    }
}
