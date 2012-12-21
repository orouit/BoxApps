using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Core.BoxHelper
{
    public partial class LoginFormEx : Form
    {
        private BoxProvider boxProvider;
        private bool firstLogin = true;
        private bool authenticated = false;
        private string ticket;
        private string requestToken = string.Empty;

        public LoginFormEx(BoxProvider boxProvider, string ticket)
        {
            InitializeComponent();

            this.boxProvider = boxProvider;
            this.ticket = ticket;
        }

        public bool RememberToken
        {
            get { return checkBoxRemember.Checked; }
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (firstLogin)
            {
                firstLogin = false;
                authenticated = boxProvider.AuthenticateUser(ticket, textBoxUsername.Text, textBoxPassword.Text, out requestToken);
                EnableLoginButton();
                if (authenticated)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            else
            {
                authenticated = boxProvider.AuthenticateUserEx(ticket, textBoxUsername.Text, textBoxPassword.Text, ref requestToken);
                EnableLoginButton();
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void textBoxUsername_TextChanged(object sender, EventArgs e)
        {
            EnableLoginButton();
        }

        private void textBoxPassword_TextChanged(object sender, EventArgs e)
        {
            EnableLoginButton();
        }

        private void EnableLoginButton()
        {
            btnLogin.Enabled = textBoxPassword.Text.Length > 0 && textBoxUsername.Text.Length > 0 && !authenticated;
        }
    }
}
