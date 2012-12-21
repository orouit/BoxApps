using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Core.BoxHelper;
using System.Diagnostics;

namespace BoxFormLoginDemo
{
    public partial class MainForm : Form
    {
        // You must enter here your own BOX API key
        const string API_KEY = "BOX_API_KEY";
        
        // The instance of the BoxProvider
        private BoxProvider boxProvider;

        public MainForm()
        {
            InitializeComponent();

            try
            {
                if (Properties.Settings.Default.RememberToken)
                {
                    boxProvider = new BoxProvider(API_KEY, Properties.Settings.Default.AuthenticationToken);
                    btnForget.Enabled = true;
                    labelStatus.Text = "You are reconnected to Box with your previous credentials";
                }
                else
                {
                    boxProvider = new BoxProvider(API_KEY);
                    boxProvider.GetTicketSucceeded += PerformBoxLogin;
                    boxProvider.GetTicketFailed += GetTicketFailed;

                    // This will request a Ticket from box and call the appropriate event handler
                    boxProvider.StartAuthenticationEx();
                    btnForget.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private void GetTicketFailed()
        {
            MessageBox.Show("Failed to get the Ticket from Box, verify you API Key");
        }

        private void PerformBoxLogin(string ticket)
        {

            LoginFormEx boxLoginDlg = new LoginFormEx(boxProvider, ticket);
            if (boxLoginDlg.ShowDialog() == DialogResult.OK)
            {
                Properties.Settings.Default.RememberToken = boxLoginDlg.RememberToken;
                if (boxLoginDlg.RememberToken)
                {
                    Properties.Settings.Default.AuthenticationToken = boxProvider.AuthenticationToken;
                    btnForget.Enabled = true;
                }

                Properties.Settings.Default.Save();

                labelStatus.Text = "You are now connected to BOX";
            }
            else
            {
                labelStatus.Text = "Connection to BOX failed";
            }
        }

        private void btnForget_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.AuthenticationToken = string.Empty;
            Properties.Settings.Default.RememberToken = false;
            Properties.Settings.Default.Save();

            labelStatus.Text = "You credentials have been cleared";
        }
    }
}
