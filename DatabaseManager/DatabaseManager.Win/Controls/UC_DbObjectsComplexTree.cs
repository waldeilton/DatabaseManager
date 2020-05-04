﻿using DatabaseConverter.Core;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DatabaseManager.Controls
{
    public delegate void ShowDbObjectContentHandler(DatabaseObjectDisplayInfo content);

    public partial class UC_DbObjectsComplexTree : UserControl, IObserver<FeedbackInfo>
    {
        private DatabaseType databaseType;
        private ConnectionInfo connectionInfo;
        private DbInterpreterOption simpleInterpreterOption = new DbInterpreterOption() { ObjectFetchMode = DatabaseObjectFetchMode.Simple };

        public ShowDbObjectContentHandler OnShowContent;
        public DatabaseInterpreter.Utility.FeedbackHandler OnFeedback;

        public DatabaseType DatabaseType => this.databaseType;

        public UC_DbObjectsComplexTree()
        {
            InitializeComponent();

            TreeView.CheckForIllegalCrossThreadCalls = false;
            Form.CheckForIllegalCrossThreadCalls = false;
        }

        public async Task LoadTree(DatabaseType dbType, ConnectionInfo connectionInfo)
        {
            this.databaseType = dbType;
            this.connectionInfo = connectionInfo;

            this.tvDbObjects.Nodes.Clear();

            DbInterpreter dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, simpleInterpreterOption);

            List<Database> databases = await dbInterpreter.GetDatabasesAsync();

            foreach (Database database in databases)
            {
                TreeNode node = DbObjectsTreeHelper.CreateTreeNode(database, true);

                this.tvDbObjects.Nodes.Add(node);
            }

            if (this.tvDbObjects.Nodes.Count == 1)
            {
                this.tvDbObjects.SelectedNode = this.tvDbObjects.Nodes[0];
            }
        }

        public void ClearNodes()
        {
            this.tvDbObjects.Nodes.Clear();
        }

        private void tvDbObjects_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (e.Node.Name == DbObjectsTreeHelper.FakeNodeName)
                {
                    return;
                }

                this.tvDbObjects.SelectedNode = e.Node;

                this.SetMenuItemVisible(e.Node);

                this.contextMenuStrip1.Show(Cursor.Position);
            }
        }

        private bool CanRefresh(TreeNode node)
        {
            return (node.Level <= 3) && !this.IsOnlyHasFakeChild(node);
        }

        private bool CanDelete(TreeNode node)
        {
            return node.Level == 2 || (node.Level == 4 && !(node.Tag is TableColumn));
        }

        private void SetMenuItemVisible(TreeNode node)
        {
            bool isDatabase = node.Level == 0;
            bool isView = node.Tag is DatabaseInterpreter.Model.View;
            bool isTable = node.Tag is Table;
            bool isScriptObject = node.Tag is ScriptDbObject;

            this.tsmiNewQuery.Visible = isDatabase;
            this.tsmiNewView.Visible = node.Name == nameof(DbObjectTreeFolderType.Views) || isView;
            this.tsmiNewFunction.Visible = node.Name == nameof(DbObjectTreeFolderType.Functions) || node.Tag is Function;
            this.tsmiNewProcedure.Visible = node.Name == nameof(DbObjectTreeFolderType.Procedures) || node.Tag is Procedure;
            this.tsmiNewTrigger.Visible = node.Name == nameof(DbObjectTreeFolderType.Triggers) || node.Tag is TableTrigger;
            this.tsmiAlter.Visible = isScriptObject;
            this.tsmiRefresh.Visible = this.CanRefresh(node);
            this.tsmiGenerateScripts.Visible = isDatabase || isTable || isScriptObject;
            this.tsmiConvert.Visible = isDatabase;          
            this.tsmiEmptyDatabase.Visible = isDatabase;
            this.tsmiDelete.Visible = this.CanDelete(node);
            this.tsmiViewData.Visible = isTable;
            this.tsmiTranslate.Visible = isTable || isScriptObject;
            this.tsmiMore.Visible = isDatabase;
        }

        private ConnectionInfo GetConnectionInfo(string database)
        {
            ConnectionInfo info = ObjectHelper.CloneObject<ConnectionInfo>(this.connectionInfo);
            info.Database = database;
            return info;
        }

        public ConnectionInfo GetCurrentConnectionInfo()
        {
            TreeNode node = this.tvDbObjects.SelectedNode;

            if (node != null)
            {
                TreeNode dbNode = this.GetDatabaseNode(node);
                ConnectionInfo connectionInfo = this.GetConnectionInfo(dbNode.Name);

                return connectionInfo;
            }

            return null;
        }

        private bool IsOnlyHasFakeChild(TreeNode node)
        {
            if (node.Nodes.Count == 1 && node.Nodes[0].Name == DbObjectsTreeHelper.FakeNodeName)
            {
                return true;
            }
            return false;
        }

        private TreeNode GetDatabaseNode(TreeNode node)
        {
            while (!(node.Tag is Database))
            {
                return this.GetDatabaseNode(node.Parent);
            }
            return node;
        }

        private DbInterpreter GetDbInterpreter(string database, bool isSimpleMode = true)
        {
            ConnectionInfo connectionInfo = this.GetConnectionInfo(database);

            DbInterpreterOption option = isSimpleMode ? simpleInterpreterOption : new DbInterpreterOption() { ObjectFetchMode = DatabaseObjectFetchMode.Details };
            option.ThrowExceptionWhenErrorOccurs = false;

            DbInterpreter dbInterpreter = DbInterpreterHelper.GetDbInterpreter(this.databaseType, connectionInfo, option);
            return dbInterpreter;
        }

        private async Task AddDbObjectNodes(TreeNode parentNode, string database, DatabaseObjectType databaseObjectType = DatabaseObjectType.None, bool createFolderNode = true)
        {
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database);

            SchemaInfo schemaInfo = databaseObjectType == DatabaseObjectType.None ? new SchemaInfo() :
                                    await dbInterpreter.GetSchemaInfoAsync(new SchemaInfoFilter() { DatabaseObjectType = databaseObjectType });

            this.ClearNodes(parentNode);

            this.AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.Table, schemaInfo.Tables, createFolderNode, true);
            this.AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.View, schemaInfo.Views, createFolderNode);
            this.AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.Function, schemaInfo.Functions, createFolderNode);
            this.AddTreeNodes(parentNode, databaseObjectType, DatabaseObjectType.Procedure, schemaInfo.Procedures, createFolderNode);
        }

        private TreeNodeCollection AddTreeNodes<T>(TreeNode node, DatabaseObjectType types, DatabaseObjectType type, List<T> dbObjects, bool createFolderNode = true, bool createFakeNode = false)
            where T : DatabaseObject
        {
            TreeNode targetNode = node;

            if (types.HasFlag(type))
            {
                if (createFolderNode)
                {
                    targetNode = node.AddDbObjectFolderNode(dbObjects);
                }
                else
                {
                    targetNode = node.AddDbObjectNodes(dbObjects);
                }
            }

            if (createFakeNode && targetNode != null)
            {
                foreach (TreeNode child in targetNode.Nodes)
                {
                    child.Nodes.Add(DbObjectsTreeHelper.CreateFakeNode());
                }
            }

            return node.Nodes;
        }

        private void AddTableFakeNodes(TreeNode tableNode, Table table)
        {
            this.ClearNodes(tableNode);

            tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Columns), nameof(DbObjectTreeFolderType.Columns), true));
            tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Triggers), nameof(DbObjectTreeFolderType.Triggers), true));
            tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Indexes), nameof(DbObjectTreeFolderType.Indexes), true));
            tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Keys), nameof(DbObjectTreeFolderType.Keys), true));
            tableNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Constraints), nameof(DbObjectTreeFolderType.Constraints), true));
        }

        private void AddDatabaseFakeNodes(TreeNode databaseNode, Database database)
        {
            this.ClearNodes(databaseNode);

            databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Tables), nameof(DbObjectTreeFolderType.Tables), true));
            databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Views), nameof(DbObjectTreeFolderType.Views), true));
            databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Functions), nameof(DbObjectTreeFolderType.Functions), true));
            databaseNode.Nodes.Add(DbObjectsTreeHelper.CreateFolderNode(nameof(DbObjectTreeFolderType.Procedures), nameof(DbObjectTreeFolderType.Procedures), true));
        }

        private async Task AddTableObjectNodes(TreeNode treeNode, Table table, DatabaseObjectType databaseObjectType)
        {
            string nodeName = treeNode.Name;
            string database = this.GetDatabaseNode(treeNode).Name;
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database, false);

            dbInterpreter.Subscribe(this);

            SchemaInfo schemaInfo = await Task.Run(() => dbInterpreter.GetSchemaInfoAsync(new SchemaInfoFilter() { Strict = true, DatabaseObjectType = databaseObjectType, TableNames = new string[] { table.Name } }));

            this.ClearNodes(treeNode);

            #region Columns           
            if (nodeName == nameof(DbObjectTreeFolderType.Columns))
            {
                foreach (TableColumn column in schemaInfo.TableColumns)
                {
                    string text = this.GetColumnText(dbInterpreter, table, column);
                    bool isPrimaryKey = schemaInfo.TablePrimaryKeys.Any(item => item.ColumnName == column.Name);
                    bool isForeignKey = schemaInfo.TableForeignKeys.Any(item => item.ColumnName == column.Name);
                    string imageKeyName = isPrimaryKey ? nameof(TablePrimaryKey) : (isForeignKey ? nameof(TableForeignKey) : nameof(TableColumn));

                    TreeNode node = DbObjectsTreeHelper.CreateTreeNode(column.Name, text, imageKeyName);
                    node.Tag = column;

                    treeNode.Nodes.Add(node);
                }
            }
            #endregion

            if (nodeName == nameof(DbObjectTreeFolderType.Triggers))
            {
                treeNode.AddDbObjectNodes(schemaInfo.TableTriggers);
            }

            #region Indexes
            if (nodeName == nameof(DbObjectTreeFolderType.Indexes) && schemaInfo.TableIndexes.Any())
            {
                ILookup<string, TableIndex> indexLookup = schemaInfo.TableIndexes.ToLookup(item => item.Name);

                foreach (var kp in indexLookup)
                {
                    string indexName = kp.Key;
                    bool isUnique = kp.Any(item => item.IsUnique);
                    string strColumns = string.Join(",", kp.Select(item => item.ColumnName));
                    string content = isUnique ? $"(Unique, {strColumns})" : $"({strColumns})";

                    string text = $"{indexName}{content}";
                    string imageKeyName = nameof(TableIndex);

                    TreeNode node = DbObjectsTreeHelper.CreateTreeNode(indexName, text, imageKeyName);
                    node.Tag = kp.First();

                    treeNode.Nodes.Add(node);
                }
            }
            #endregion
            if (nodeName == nameof(DbObjectTreeFolderType.Keys))
            {
                if (schemaInfo.TablePrimaryKeys.Any() || schemaInfo.TablePrimaryKeys.Any())
                {
                    foreach (TablePrimaryKey key in schemaInfo.TablePrimaryKeys)
                    {
                        TreeNode node = DbObjectsTreeHelper.CreateTreeNode(key);
                        treeNode.Nodes.Add(node);
                    }

                    foreach (TableForeignKey key in schemaInfo.TableForeignKeys)
                    {
                        TreeNode node = DbObjectsTreeHelper.CreateTreeNode(key);
                        treeNode.Nodes.Add(node);
                    }
                }
            }

            #region Constraints
            if (nodeName == nameof(DbObjectTreeFolderType.Constraints) && schemaInfo.TableConstraints.Any())
            {
                foreach (TableConstraint constraint in schemaInfo.TableConstraints)
                {
                    TreeNode node = DbObjectsTreeHelper.CreateTreeNode(constraint);
                    treeNode.Nodes.Add(node);
                }
            }
            #endregion          

            this.Feedback("");
        }

        private string GetColumnText(DbInterpreter dbInterpreter, Table table, TableColumn column)
        {
            string text = dbInterpreter.ParseColumn(table, column).Replace(dbInterpreter.QuotationLeftChar.ToString(), "").Replace(dbInterpreter.QuotationRightChar.ToString(), "");

            return $"{column.Name} ({text.Replace(column.Name + " ", "").ToLower()})";
        }

        private async void tvDbObjects_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;

            if (!this.IsOnlyHasFakeChild(node))
            {
                return;
            }

            this.tvDbObjects.BeginInvoke(new Action(() => this.LoadChildNodes(node)));
        }

        private void ClearNodes(TreeNode node)
        {
            node.Nodes.Clear();
        }

        private void ShowLoading(TreeNode node)
        {
            string loadingImageKey = "Loading.gif";
            string loadingText = "loading...";

            if (this.IsOnlyHasFakeChild(node))
            {
                node.Nodes[0].ImageKey = loadingImageKey;
                node.Nodes[0].Text = loadingText;
            }
            else
            {
                node.Nodes.Add(DbObjectsTreeHelper.CreateTreeNode("Loading", loadingText, loadingImageKey));
            }
        }

        private async Task LoadChildNodes(TreeNode node)
        {
            this.ShowLoading(node);

            object tag = node.Tag;

            if (tag is Database)
            {
                Database database = tag as Database;

                this.AddDatabaseFakeNodes(node, database);
            }
            else if (tag is Table)
            {
                Table table = tag as Table;
                this.AddTableFakeNodes(node, table);
            }
            else if (tag == null)
            {
                string name = node.Name;

                TreeNode parentNode = node.Parent;

                if (parentNode.Tag is Database)
                {
                    string databaseName = parentNode.Name;
                    DatabaseObjectType databaseObjectType = DbObjectsTreeHelper.GetDbObjectTypeByFolderName(name);

                    if (databaseObjectType != DatabaseObjectType.None)
                    {
                        await this.AddDbObjectNodes(node, databaseName, databaseObjectType, false);
                    }
                }
                else if (parentNode.Tag is Table)
                {
                    DatabaseObjectType databaseObjectType = DatabaseObjectType.None;
                    switch (name)
                    {
                        case nameof(DbObjectTreeFolderType.Columns):
                            databaseObjectType = DatabaseObjectType.TableColumn | DatabaseObjectType.TablePrimaryKey | DatabaseObjectType.TableForeignKey;
                            break;
                        case nameof(DbObjectTreeFolderType.Triggers):
                            databaseObjectType = DatabaseObjectType.TableTrigger;
                            break;
                        case nameof(DbObjectTreeFolderType.Indexes):
                            databaseObjectType = DatabaseObjectType.TableIndex;
                            break;
                        case nameof(DbObjectTreeFolderType.Keys):
                            databaseObjectType = DatabaseObjectType.TablePrimaryKey | DatabaseObjectType.TableForeignKey;
                            break;
                        case nameof(DbObjectTreeFolderType.Constraints):
                            databaseObjectType = DatabaseObjectType.TableConstraint;
                            break;
                    }

                    await this.AddTableObjectNodes(node, parentNode.Tag as Table, databaseObjectType);
                }
            }
        }

        private async void tsmiRefresh_Click(object sender, EventArgs e)
        {
            await this.RefreshNode();
        }

        private async Task RefreshNode()
        {
            if (!this.IsValidSelectedNode())
            {
                return;
            }

            TreeNode node = this.GetSelectedNode();

            if (this.CanRefresh(node))
            {
                await this.LoadChildNodes(node);
            }
        }

        private bool IsValidSelectedNode()
        {
            TreeNode node = this.GetSelectedNode();

            return node != null;
        }

        private TreeNode GetSelectedNode()
        {
            return this.tvDbObjects.SelectedNode;
        }

        private async void tsmiGenerateScripts_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.CREATE);
        }

        private async void GenerateScripts(ScriptAction scriptAction)
        {
            if (!this.IsValidSelectedNode())
            {
                return;
            }

            TreeNode node = this.GetSelectedNode();

            await this.GenerateScripts(node, scriptAction);
        }

        private async Task GenerateScripts(TreeNode node, ScriptAction scriptAction)
        {
            object tag = node.Tag;

            if (tag is Database)
            {
                Database database = tag as Database;

                frmGenerateScripts frmGenerateScripts = new frmGenerateScripts(this.databaseType, this.GetConnectionInfo(database.Name));
                frmGenerateScripts.ShowDialog();
            }
            else if (tag is DatabaseObject)
            {
                string databaseName = this.GetDatabaseNode(node).Name;

                await this.GenerateObjectScript(databaseName, tag as DatabaseObject, scriptAction);
            }
        }

        private async Task GenerateObjectScript(string database, DatabaseObject dbObj, ScriptAction scriptAction)
        {
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database, false);

            ScriptGenerator scriptGenerator = new ScriptGenerator(dbInterpreter);

            string script = await scriptGenerator.Generate(dbObj, scriptAction);          

            this.ShowContent(new DatabaseObjectDisplayInfo() { Name = dbObj.Name, DatabaseType = this.databaseType, DatabaseObject = dbObj, Content = script, ConnectionInfo = dbInterpreter.ConnectionInfo });
        }

        private void tsmiConvert_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedNode())
            {
                return;
            }

            TreeNode node = this.GetSelectedNode();

            this.ConvertDatabase(node);
        }

        private void ConvertDatabase(TreeNode node)
        {
            Database database = node.Tag as Database;

            frmConvert frmConvert = new frmConvert(this.databaseType, this.GetConnectionInfo(database.Name));
            frmConvert.ShowDialog();
        }

        private async void tsmiClearData_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedNode())
            {
                return;
            }

            if (MessageBox.Show("Are you sure to clear all data of the database?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                TreeNode node = this.GetSelectedNode();

                await this.ClearData(node.Name);
            }
        }

        private async Task ClearData(string database)
        {
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database);
            DbManager dbManager = new DbManager(dbInterpreter);

            dbManager.Subscribe(this);
            dbInterpreter.Subscribe(this);

            await Task.Run(() => dbManager.ClearData());

            if (!dbInterpreter.HasError)
            {
                MessageBox.Show("Data has been cleared.");
            }
        }

        private void Feedback(FeedbackInfo info)
        {
            if (this.OnFeedback != null)
            {
                this.OnFeedback(info);
            }
        }

        private void Feedback(string message)
        {
            this.Feedback(new FeedbackInfo() { Message = message });
        }

        private async void tsmiEmptyDatabase_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedNode())
            {
                return;
            }

            if (MessageBox.Show("Are you sure to delelte all objects of the database?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                frmDbObjectTypeSelector selector = new frmDbObjectTypeSelector();

                if (selector.ShowDialog() == DialogResult.OK)
                {
                    TreeNode node = this.GetSelectedNode();

                    await Task.Run(() => this.EmptyDatabase(node.Name, selector.DatabaseObjectType));

                    await this.LoadChildNodes(node);

                    this.Feedback("");
                }
            }
        }

        private async Task EmptyDatabase(string database, DatabaseObjectType databaseObjectType)
        {
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database);
            DbManager dbManager = new DbManager(dbInterpreter);

            dbInterpreter.Subscribe(this);
            dbManager.Subscribe(this);

            await dbManager.EmptyDatabase(databaseObjectType);

            if (!dbInterpreter.HasError)
            {
                MessageBox.Show("Seleted database objects have been deleted.");
            }
        }

        private async void tvDbObjects_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                await this.RefreshNode();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                this.DeleteNode();
            }
        }

        private void DeleteNode()
        {
            if (!this.IsValidSelectedNode())
            {
                return;
            }

            TreeNode node = this.GetSelectedNode();

            if (this.CanDelete(node))
            {
                if (MessageBox.Show("Are you sure to delete this object?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    this.DropDbObject(node);
                }
            }
        }

        private async void DropDbObject(TreeNode node)
        {
            string database = this.GetDatabaseNode(node).Name;
            DatabaseObject dbObject = node.Tag as DatabaseObject;

            DbInterpreter dbInterpreter = this.GetDbInterpreter(database);
            dbInterpreter.Subscribe(this);

            await dbInterpreter.Drop(dbObject);

            if (!dbInterpreter.HasError)
            {
                node.Parent.Nodes.Remove(node);
            }
        }

        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            this.DeleteNode();
        }

        private void tsmiViewData_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedNode())
            {
                return;
            }

            TreeNode node = this.GetSelectedNode();

            this.ViewData(node);
        }

        private void ViewData(TreeNode node)
        {
            string database = this.GetDatabaseNode(node).Name;
            Table table = node.Tag as Table;

            this.ShowContent(new DatabaseObjectDisplayInfo() { Name = table.Name, DatabaseType = this.databaseType, DatabaseObject = table, DisplayType = DatabaseObjectDisplayType.Data, ConnectionInfo = this.GetConnectionInfo(database) });
        }

        public void OnNext(FeedbackInfo value)
        {
            this.Feedback(value);
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        private void tvDbObjects_AfterExpand(object sender, TreeViewEventArgs e)
        {
            this.Feedback("");
        }

        private void tsmiTranslate_MouseEnter(object sender, EventArgs e)
        {
            this.tsmiTranslate.DropDownItems.Clear();

            var dbTypes = DbInterpreterHelper.GetDisplayDatabaseTypes();

            foreach (var dbType in dbTypes)
            {
                if ((int)dbType != (int)this.databaseType)
                {
                    ToolStripMenuItem item = new ToolStripMenuItem(dbType.ToString());
                    item.Click += TranslateItem_Click;

                    this.tsmiTranslate.DropDownItems.Add(item);
                }
            }
        }

        private async void TranslateItem_Click(object sender, EventArgs e)
        {
            DatabaseType dbType = ManagerUtil.GetDatabaseType((sender as ToolStripMenuItem).Text);

            if (!this.IsValidSelectedNode())
            {
                return;
            }

            TreeNode node = this.GetSelectedNode();
            this.tvDbObjects.SelectedNode = node;

            await this.Translate(node, dbType);
        }

        private async Task Translate(TreeNode node, DatabaseType targetDbType)
        {
            object tag = node.Tag;

            ConnectionInfo connectionInfo = this.GetConnectionInfo((this.GetDatabaseNode(node).Tag as Database).Name);

            if (tag is DatabaseObject)
            {
                DbManager dbManager = new DbManager();
                dbManager.Subscribe(this);

                await dbManager.Translate(this.databaseType, targetDbType, tag as DatabaseObject, connectionInfo, this.DbConverter_OnTranslated);
            }
        }

        private void DbConverter_OnTranslated(DatabaseType dbType, DatabaseObject dbObject, TranslateResult result)
        {
            DatabaseObjectDisplayInfo info = new DatabaseObjectDisplayInfo() { Name = dbObject.Name, DatabaseType = dbType, DatabaseObject = dbObject, Content = result.Data?.ToString(), ConnectionInfo = null, Error = result.Error };

            this.ShowContent(info);
        }

        private void tvDbObjects_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                TreeNode treeNode = e.Item as TreeNode;

                if (treeNode != null && treeNode.Tag is DatabaseObject)
                {
                    string text = treeNode.Text;
                    int index = text.IndexOf('(');

                    if (index > 0)
                    {
                        text = text.Substring(0, index);
                    }

                    DbInterpreter dbInterpreter = this.GetDbInterpreter(this.GetDatabaseNode(treeNode).Name);

                    string[] items = text.Trim().Split('.');

                    items[items.Length - 1] = dbInterpreter.GetQuotedString(items[items.Length - 1]);

                    DoDragDrop(string.Join(".", items), DragDropEffects.Move);
                }
            }
        }

        public DatabaseObjectDisplayInfo GetDisplayInfo()
        {
            TreeNode node = this.tvDbObjects.SelectedNode;

            DatabaseObjectDisplayInfo info = new DatabaseObjectDisplayInfo() { DatabaseType = this.DatabaseType };

            if (node != null)
            {
                if (node.Tag is DatabaseObject dbObject)
                {
                    info.Name = dbObject.Name;
                    info.DatabaseObject = dbObject;
                }

                TreeNode databaseNode = this.GetDatabaseNode(node);

                if (databaseNode != null)
                {
                    info.ConnectionInfo = this.GetConnectionInfo(databaseNode.Name);
                }
                else
                {
                    info.ConnectionInfo = this.connectionInfo;
                }
            }

            return info;
        }

        private void tsmiNewQuery_Click(object sender, EventArgs e)
        {
            DatabaseObjectDisplayInfo info = new DatabaseObjectDisplayInfo() { IsNew = true, DisplayType = DatabaseObjectDisplayType.Script, DatabaseType = this.DatabaseType };

            info.ConnectionInfo = this.GetConnectionInfo(this.tvDbObjects.SelectedNode.Name); ;

            this.ShowContent(info);
        }

        private void ShowContent(DatabaseObjectDisplayInfo info)
        {
            if (this.OnShowContent != null)
            {
                this.OnShowContent(info);
            }
        }

        private void tsmiNewView_Click(object sender, EventArgs e)
        {
            this.DoScript(DatabaseObjectType.View, ScriptAction.CREATE);
        }

        private void tsmiNewFunction_Click(object sender, EventArgs e)
        {
            this.DoScript(DatabaseObjectType.Function, ScriptAction.CREATE);
        }

        private void tsmiNewProcedure_Click(object sender, EventArgs e)
        {
            this.DoScript(DatabaseObjectType.Procedure, ScriptAction.CREATE);
        }

        private void tsmiNewTrigger_Click(object sender, EventArgs e)
        {
            this.DoScript(DatabaseObjectType.TableTrigger, ScriptAction.CREATE);
        }

        private void DoScript(DatabaseObjectType databaseObjectType, ScriptAction scriptAction)
        {
            DbInterpreter dbInterpreter = DbInterpreterHelper.GetDbInterpreter(this.databaseType, new ConnectionInfo(), new DbInterpreterOption());

            ScriptTemplate scriptTemplate = new ScriptTemplate(dbInterpreter);          

            DatabaseObjectDisplayInfo displayInfo = this.GetDisplayInfo();
            displayInfo.IsNew = true;
            displayInfo.Content = scriptTemplate.GetTemplateContent(databaseObjectType, scriptAction);
            displayInfo.ScriptAction = scriptAction;

            this.ShowContent(displayInfo);
        }

        private async void tsmiAlter_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.ALTER);
        }
    }
}
