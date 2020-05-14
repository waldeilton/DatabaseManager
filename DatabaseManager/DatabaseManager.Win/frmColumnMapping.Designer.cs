﻿namespace DatabaseManager
{
    partial class frmColumnMapping
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.gbReferenceTable = new System.Windows.Forms.GroupBox();
            this.gbTable = new System.Windows.Forms.GroupBox();
            this.panelReferenceTable = new System.Windows.Forms.Panel();
            this.panelTable = new System.Windows.Forms.Panel();
            this.gbReferenceTable.SuspendLayout();
            this.gbTable.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(365, 360);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(269, 360);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // gbReferenceTable
            // 
            this.gbReferenceTable.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.gbReferenceTable.Controls.Add(this.panelReferenceTable);
            this.gbReferenceTable.Location = new System.Drawing.Point(6, 12);
            this.gbReferenceTable.Name = "gbReferenceTable";
            this.gbReferenceTable.Size = new System.Drawing.Size(215, 336);
            this.gbReferenceTable.TabIndex = 5;
            this.gbReferenceTable.TabStop = false;
            this.gbReferenceTable.Text = "Reference Table";
            // 
            // gbTable
            // 
            this.gbTable.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbTable.Controls.Add(this.panelTable);
            this.gbTable.Location = new System.Drawing.Point(227, 12);
            this.gbTable.Name = "gbTable";
            this.gbTable.Size = new System.Drawing.Size(218, 336);
            this.gbTable.TabIndex = 6;
            this.gbTable.TabStop = false;
            this.gbTable.Text = "Table";
            // 
            // panelReferenceTable
            // 
            this.panelReferenceTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelReferenceTable.Location = new System.Drawing.Point(3, 17);
            this.panelReferenceTable.Name = "panelReferenceTable";
            this.panelReferenceTable.Size = new System.Drawing.Size(209, 316);
            this.panelReferenceTable.TabIndex = 0;
            // 
            // panelTable
            // 
            this.panelTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTable.Location = new System.Drawing.Point(3, 17);
            this.panelTable.Name = "panelTable";
            this.panelTable.Size = new System.Drawing.Size(212, 316);
            this.panelTable.TabIndex = 1;
            // 
            // frmColumnMapping
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(452, 392);
            this.Controls.Add(this.gbTable);
            this.Controls.Add(this.gbReferenceTable);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.MaximizeBox = false;
            this.Name = "frmColumnMapping";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Column Mapping";
            this.Load += new System.EventHandler(this.frmColumnMapping_Load);
            this.gbReferenceTable.ResumeLayout(false);
            this.gbTable.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.GroupBox gbReferenceTable;
        private System.Windows.Forms.GroupBox gbTable;
        private System.Windows.Forms.Panel panelReferenceTable;
        private System.Windows.Forms.Panel panelTable;
    }
}