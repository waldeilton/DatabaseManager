﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseInterpreter.Model;
using DatabaseManager.Model;
using DatabaseManager.Core;

namespace DatabaseManager.Controls
{
    public delegate void ColumnMappingSelectHandler(string referenceTableName, List<ForeignKeyColumn> mappings);

    public partial class UC_TableForeignKeys : UserControl
    {
        private bool inited = false;
        private bool loadedData = false;

        public bool Inited => this.inited;
        public bool LoadedData => this.loadedData;
        public Table Table { get; set; }

        public DatabaseType DatabaseType { get; set; }

        public event ColumnMappingSelectHandler OnColumnMappingSelect;

        public GeneateChangeScriptsHandler OnGenerateChangeScripts;

        public UC_TableForeignKeys()
        {
            InitializeComponent();
        }

        private void UC_TableForeignKeys_Load(object sender, EventArgs e)
        {
        }

        public void InitControls(IEnumerable<Table> tables)
        {
            this.colReferenceTable.DataSource = tables;
            this.colReferenceTable.ValueMember = nameof(Table.Name);
            this.colReferenceTable.DisplayMember = nameof(Table.Name);

            if (this.DatabaseType == DatabaseType.Oracle || this.DatabaseType == DatabaseType.MySql)
            {
                this.colComment.Visible = false;
            }

            this.inited = true;
        }

        public void LoadForeignKeys(IEnumerable<TableForeignKeyDesignerInfo> foreignKeyDesignerInfos)
        {
            this.dgvForeignKeys.Rows.Clear();

            foreach (TableForeignKeyDesignerInfo key in foreignKeyDesignerInfos)
            {
                int rowIndex = this.dgvForeignKeys.Rows.Add();

                DataGridViewRow row = this.dgvForeignKeys.Rows[rowIndex];

                row.Cells[this.colKeyName.Name].Value = key.Name;
                row.Cells[this.colReferenceTable.Name].Value = key.ReferencedTableName;
                row.Cells[this.colColumns.Name].Value = this.GetColumnMappingsDisplayText(key.Columns);
                row.Cells[this.colUpdateCascade.Name].Value = key.UpdateCascade;
                row.Cells[this.colDeleteCascade.Name].Value = key.DeleteCascade;
                row.Cells[this.colComment.Name].Value = key.Comment;

                row.Tag = key;
            }

            this.loadedData = true;

            this.AutoSizeColumns();
            this.dgvForeignKeys.ClearSelection();
        }

        public List<TableForeignKeyDesignerInfo> GetForeignKeys()
        {
            List<TableForeignKeyDesignerInfo> keyDesingerInfos = new List<TableForeignKeyDesignerInfo>();

            foreach (DataGridViewRow row in this.dgvForeignKeys.Rows)
            {
                TableForeignKeyDesignerInfo key = new TableForeignKeyDesignerInfo();

                string keyName = row.Cells[this.colKeyName.Name].Value?.ToString();

                if (!string.IsNullOrEmpty(keyName))
                {
                    TableForeignKeyDesignerInfo tag = row.Tag as TableForeignKeyDesignerInfo;

                    key.OldName = tag?.OldName;
                    key.Name = keyName;
                    key.Columns = tag?.Columns;
                    key.ReferencedTableName = DataGridViewHelper.GetCellStringValue(row, this.colReferenceTable.Name);
                    key.UpdateCascade = DataGridViewHelper.GetCellBoolValue(row, this.colUpdateCascade.Name);
                    key.DeleteCascade = DataGridViewHelper.GetCellBoolValue(row, this.colDeleteCascade.Name);
                    key.Comment = DataGridViewHelper.GetCellStringValue(row, this.colComment.Name);

                    row.Tag = key;

                    keyDesingerInfos.Add(key);
                }
            }

            return keyDesingerInfos;
        }

        private string GetColumnMappingsDisplayText(IEnumerable<ForeignKeyColumn> columns)
        {
            return string.Join(",", columns.Select(item => $"{item.ColumnName}=>{item.ReferencedColumnName}"));
        }

        private void dgvForeignKeys_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                this.DeleteRow();
            }
        }

        private void DeleteRow()
        {
            DataGridViewRow row = DataGridViewHelper.GetSelectedRow(this.dgvForeignKeys);

            if (row != null && !row.IsNewRow)
            {
                this.dgvForeignKeys.Rows.RemoveAt(row.Index);
            }
        }

        private void tsmiDeleteForeignKey_Click(object sender, EventArgs e)
        {
            this.DeleteRow();
        }

        private void dgvForeignKeys_SizeChanged(object sender, EventArgs e)
        {
            this.AutoSizeColumns();
        }

        private void AutoSizeColumns()
        {
            DataGridViewHelper.AutoSizeLastColumn(this.dgvForeignKeys);
        }

        private void dgvForeignKeys_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
        }

        private void dgvForeignKeys_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == this.colColumns.Index)
            {
                DataGridViewRow row = this.dgvForeignKeys.Rows[e.RowIndex];

                string keyName = DataGridViewHelper.GetCellStringValue(row, this.colKeyName.Name);
                string referenceTableName = DataGridViewHelper.GetCellStringValue(row, this.colReferenceTable.Name);

                if (!string.IsNullOrEmpty(keyName) && !string.IsNullOrEmpty(referenceTableName))
                {
                    if (this.OnColumnMappingSelect != null)
                    {
                        this.OnColumnMappingSelect(referenceTableName, (row.Tag as TableForeignKeyDesignerInfo)?.Columns);
                    }
                }
            }
        }

        public void SetRowColumns(IEnumerable<ForeignKeyColumn> mappings)
        {
            DataGridViewCell cell = this.dgvForeignKeys.CurrentCell;

            if (cell != null)
            {
                cell.Value = this.GetColumnMappingsDisplayText(mappings);

                TableForeignKeyDesignerInfo keyDesignerInfo = cell.OwningRow.Tag as TableForeignKeyDesignerInfo;

                if (keyDesignerInfo == null)
                {
                    keyDesignerInfo = new TableForeignKeyDesignerInfo();
                }

                keyDesignerInfo.Columns = mappings.ToList();

                cell.OwningRow.Tag = keyDesignerInfo;
            }
        }

        public void OnSaved()
        {
            for (int i = 0; i < this.dgvForeignKeys.RowCount; i++)
            {
                DataGridViewRow row = this.dgvForeignKeys.Rows[i];

                TableForeignKeyDesignerInfo keyDesingerInfo = row.Tag as TableForeignKeyDesignerInfo;

                if (keyDesingerInfo != null && !string.IsNullOrEmpty(keyDesingerInfo.Name))
                {
                    keyDesingerInfo.OldName = keyDesingerInfo.Name;
                }
            }
        }

        public void EndEdit()
        {
            this.dgvForeignKeys.EndEdit();
            this.dgvForeignKeys.CurrentCell = null;
        }

        private void dgvForeignKeys_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            DataGridViewRow row = this.dgvForeignKeys.Rows[e.RowIndex];

            if (e.ColumnIndex == this.colReferenceTable.Index)
            {
                string referencedTableName = DataGridViewHelper.GetCellStringValue(row, this.colReferenceTable.Name);
                string keyName = DataGridViewHelper.GetCellStringValue(row, this.colKeyName.Name);

                if (!string.IsNullOrEmpty(referencedTableName) && string.IsNullOrEmpty(keyName))
                {
                    row.Cells[this.colKeyName.Name].Value = IndexManager.GetForeignKeyDefaultName(this.Table.Name, referencedTableName);
                }
            }
        }

        private void dgvForeignKeys_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                DataGridViewRow row = DataGridViewHelper.GetSelectedRow(this.dgvForeignKeys);

                if (row != null)
                {
                    bool isEmptyNewRow = row.IsNewRow && DataGridViewHelper.IsEmptyRow(row);

                    this.tsmiDeleteForeignKey.Enabled = !isEmptyNewRow;
                }
                else
                {
                    this.tsmiDeleteForeignKey.Enabled = false;
                }

                this.contextMenuStrip1.Show(this.dgvForeignKeys, e.Location);
            }
        }

        private void dgvForeignKeys_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            this.dgvForeignKeys.EndEdit();
            this.dgvForeignKeys.CurrentCell = null;
            this.dgvForeignKeys.Rows[e.RowIndex].Selected = true;
        }

        private void tsmiGenerateChangeScripts_Click(object sender, EventArgs e)
        {
            if (this.OnGenerateChangeScripts != null)
            {
                this.OnGenerateChangeScripts();
            }
        }
    }
}
