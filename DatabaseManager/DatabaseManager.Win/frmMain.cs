﻿using DatabaseInterpreter.Core;
using DatabaseInterpreter.Utility;
using DatabaseManager.Controls;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DatabaseManager
{
    public partial class frmMain : Form, IObserver<FeedbackInfo>
    {
        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            this.InitControls();

            TreeView.CheckForIllegalCrossThreadCalls = false;

            FeedbackHelper.EnableLog = SettingManager.Setting.EnableLog;
            LogHelper.LogType = SettingManager.Setting.LogType;
            FeedbackHelper.EnableDebug = true;
        }

        private void InitControls()
        {
            this.navigator.OnShowContent += this.ShowDbObjectContent;
            this.navigator.OnFeedback += this.Feedback;
            this.ucContent.OnDataFilter += this.DataFilter;
        }

        private void ShowDbObjectContent(DatabaseObjectDisplayInfo content)
        {
            this.ucContent.ShowContent(content);
        }

        private void DataFilter(object sender)
        {
            UC_DataViewer dataViewer = sender as UC_DataViewer;

            frmDataFilter filter = new frmDataFilter() { Columns = dataViewer.Columns.ToList(), ConditionBuilder = dataViewer.ConditionBuilder };
            if (filter.ShowDialog() == DialogResult.OK)
            {
                dataViewer.FilterData(filter.ConditionBuilder);
            }
        }

        private void Feedback(FeedbackInfo info)
        {
            this.txtMessage.ForeColor = Color.Black;

            if (info.InfoType == FeedbackInfoType.Error)
            {
                if (!info.IgnoreError)
                {
                    MessageBox.Show(info.Message);
                }

                this.txtMessage.Text = info.Message;
                this.txtMessage.BackColor = this.BackColor;
                this.txtMessage.ForeColor = Color.Red;
                this.toolTip1.SetToolTip(this.txtMessage, info.Message);
            }
            else
            {
                this.txtMessage.Text = info.Message;
            }
        }

        private void tsmiSetting_Click(object sender, EventArgs e)
        {
            frmSetting frmSetting = new frmSetting();
            frmSetting.ShowDialog();
        }

        private void btnConvert_Click(object sender, EventArgs e)
        {
            frmConvert frmConvert = new frmConvert();
            frmConvert.ShowDialog();
        }

        private void frmMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                if (FormEventCenter.OnSave != null)
                {
                    FormEventCenter.OnSave();
                }
            }
        }

        public void OnNext(FeedbackInfo value)
        {
            this.Feedback(value);
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        private void tsmiDbConnection_Click(object sender, EventArgs e)
        {
            frmDbConnectionManage frmDbConnectionManage = new frmDbConnectionManage();
            frmDbConnectionManage.ShowDialog();
        }

        private void tsBtnGenerateScripts_Click(object sender, EventArgs e)
        {
            frmGenerateScripts frmGenerateScripts = new frmGenerateScripts();
            frmGenerateScripts.ShowDialog();
        }

        private void tsBtnConvert_Click(object sender, EventArgs e)
        {
            frmConvert frmConvert = new frmConvert();
            frmConvert.ShowDialog();
        }
    }
}
